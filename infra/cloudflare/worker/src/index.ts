import { DurableObject } from "cloudflare:workers";

type DeploymentMode = "All" | "Split";
type RuntimeMode = "All" | "Api" | "Worker";
type ContainerImage = "api" | "worker";

interface Env {
  APP_MODE: string;
  API_HOSTNAME: string;
  AUTH0_DOMAIN: string;
  AUTH0_CLIENT_ID: string;
  AUTH0_AUDIENCE: string;
  AUTH0_MANAGEMENT_CLIENT_ID: string;
  EMAIL_BASE_URL: string;
  EMAIL_FROM_EMAIL: string;
  EMAIL_FROM_NAME: string;
  BREVO_API_URL: string;
  RULES_QUESTIONS_URL: string;
  APP_CONTAINER: DurableObjectNamespace<AppContainer>;
  WORKER_CONTAINER: DurableObjectNamespace<WorkerContainer>;
  POSTGRES_CONNECTION_STRING?: string;
  REDIS_ENDPOINT?: string;
  PRIVACY_CHALLENGE_HMAC_SECRET?: string;
  CLOUDFLARE_PROXY_ASSERTION_SECRET?: string;
  CLOUDFLARE_DATA_PROTECTION_PFX_BASE64?: string;
  CLOUDFLARE_DATA_PROTECTION_PFX_PASSWORD?: string;
  AUTH0_CLIENT_SECRET?: string;
  AUTH0_MANAGEMENT_CLIENT_SECRET?: string;
  BREVO_API_KEY?: string;
}

const API_PORT = 8080;
const CONTAINER_HOSTNAME = "container";
const API_INSTANCE = "standard-1";
const WORKER_INSTANCE = "standard-1";
// Keep one missed one-minute Cron interval from stopping a background container.
const INACTIVITY_TIMEOUT_MS = 2 * 60 * 1000;
const READINESS_ATTEMPTS = 120;
const READINESS_DELAY_MS = 500;
const API_READY_PATH = "/graphql?query=%7B__typename%7D";

const workerOwnedHeaders = new Set([
  "cf-connecting-ip",
  "forwarded",
  "x-real-ip",
  "x-ref-test-client-ip",
  "x-ref-test-forwarded-host",
  "x-ref-test-forwarded-proto",
  "x-ref-test-proxy-assertion",
]);

abstract class RefTestContainer extends DurableObject<Env> {
  private starting: Promise<void> | undefined;
  private apiReady: Promise<void> | undefined;

  protected abstract get runtimeMode(): RuntimeMode;
  protected abstract readonly imageName: ContainerImage;
  protected abstract readonly apiInstance: boolean;

  protected constructor(ctx: DurableObjectState, env: Env) {
    super(ctx, env);

    const container = ctx.container;
    if (container?.running) {
      void ctx.blockConcurrencyWhile(() =>
        container.setInactivityTimeout(INACTIVITY_TIMEOUT_MS));
    }
  }

  async fetch(request: Request): Promise<Response> {
    if (!this.apiInstance)
      return new Response("This container does not serve HTTP requests.", { status: 404 });

    const sourceUrl = new URL(request.url);
    const apiHostname = getApiHostname(this.env.API_HOSTNAME);
    if (sourceUrl.host.toLowerCase() !== apiHostname)
      return new Response("Misdirected request.", { status: 421 });

    const clientIp = request.headers.get("CF-Connecting-IP");
    if (!clientIp || clientIp.includes(","))
      return new Response("Cloudflare client address is missing or invalid.", { status: 400 });

    await this.ensureApiReady();

    const container = this.getContainer();
    const containerUrl = new URL(request.url);
    containerUrl.protocol = "http:";
    containerUrl.host = CONTAINER_HOSTNAME;

    const forwarded = new Request(containerUrl, request);
    for (const name of Array.from(forwarded.headers.keys())) {
      const normalizedName = name.toLowerCase();
      if (
        normalizedName.startsWith("x-forwarded-") ||
        workerOwnedHeaders.has(normalizedName)
      ) {
        forwarded.headers.delete(name);
      }
    }

    forwarded.headers.set("X-RefTest-Client-IP", clientIp);
    forwarded.headers.set("X-RefTest-Forwarded-Proto", sourceUrl.protocol.slice(0, -1));
    forwarded.headers.set("X-RefTest-Forwarded-Host", apiHostname);
    forwarded.headers.set(
      "X-RefTest-Proxy-Assertion",
      getProxyAssertionSecret(this.env.CLOUDFLARE_PROXY_ASSERTION_SECRET),
    );
    forwarded.headers.delete("host");

    return container.getTcpPort(API_PORT).fetch(forwarded);
  }

  async wake(): Promise<void> {
    if (this.apiInstance) {
      await this.ensureApiReady();
      return;
    }

    await this.ensureRunning();
  }

  private ensureRunning(): Promise<void> {
    if (this.starting === undefined) {
      this.starting = this.startContainer().finally(() => {
        this.starting = undefined;
      });
    }

    return this.starting;
  }

  private ensureApiReady(): Promise<void> {
    if (this.apiReady === undefined) {
      this.apiReady = this.waitForApi().finally(() => {
        this.apiReady = undefined;
      });
    }

    return this.apiReady;
  }

  private async startContainer(): Promise<void> {
    const container = this.getContainer();
    if (!container.running) {
      container.start({
        image: container.images[this.imageName],
        instance: this.apiInstance ? API_INSTANCE : WORKER_INSTANCE,
        enableInternet: true,
        env: buildContainerEnvironment(this.env, this.runtimeMode),
      });
    }

    await container.setInactivityTimeout(INACTIVITY_TIMEOUT_MS);
  }

  private async waitForApi(): Promise<void> {
    await this.ensureRunning();

    const port = this.getContainer().getTcpPort(API_PORT);
    const headers = new Headers({
      "X-RefTest-Client-IP": "127.0.0.1",
      "X-RefTest-Forwarded-Proto": "https",
      "X-RefTest-Forwarded-Host": getApiHostname(this.env.API_HOSTNAME),
      "X-RefTest-Proxy-Assertion": getProxyAssertionSecret(
        this.env.CLOUDFLARE_PROXY_ASSERTION_SECRET,
      ),
    });
    let lastError: unknown;
    for (let attempt = 0; attempt < READINESS_ATTEMPTS; attempt++) {
      try {
        const response = await port.fetch(
          `http://${CONTAINER_HOSTNAME}${API_READY_PATH}`,
          { headers, signal: AbortSignal.timeout(2_000) },
        );
        await response.body?.cancel();
        if (response.ok)
          return;

        lastError = new Error(`API readiness check returned HTTP ${response.status}.`);
      } catch (error) {
        lastError = error;
      }

      if (attempt + 1 < READINESS_ATTEMPTS)
        await scheduler.wait(READINESS_DELAY_MS);
    }

    throw new Error("The API container did not become ready on port 8080.", {
      cause: lastError,
    });
  }

  private getContainer(): NonNullable<DurableObjectState["container"]> {
    const container = this.ctx.container;
    if (!container)
      throw new Error("No Cloudflare Container is configured for this Durable Object.");

    return container;
  }
}

export class AppContainer extends RefTestContainer {
  protected get runtimeMode(): RuntimeMode {
    return getDeploymentMode(this.env.APP_MODE) === "All" ? "All" : "Api";
  }

  protected readonly imageName = "api";
  protected readonly apiInstance = true;
}

export class WorkerContainer extends RefTestContainer {
  protected get runtimeMode(): RuntimeMode {
    return "Worker";
  }

  protected readonly imageName = "worker";
  protected readonly apiInstance = false;
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const mode = getDeploymentMode(env.APP_MODE);
    const apiHostname = getApiHostname(env.API_HOSTNAME);
    if (new URL(request.url).host.toLowerCase() !== apiHostname)
      return new Response("Misdirected request.", { status: 421 });

    const clientIp = request.headers.get("CF-Connecting-IP");
    if (!clientIp || clientIp.includes(","))
      return new Response("Cloudflare client address is missing or invalid.", { status: 400 });

    const apiContainer = env.APP_CONTAINER.getByName(mode === "All" ? "all" : "api");
    return apiContainer.fetch(request);
  },
  async scheduled(_controller: ScheduledController, env: Env): Promise<void> {
    const mode = getDeploymentMode(env.APP_MODE);
    if (mode === "All") {
      await env.APP_CONTAINER.getByName("all").wake();
      return;
    }

    await env.WORKER_CONTAINER.getByName("worker").wake();
  },
} satisfies ExportedHandler<Env>;

function getDeploymentMode(value: string): DeploymentMode {
  if (value === "All" || value === "Split")
    return value;

  throw new Error("APP_MODE must be configured as All or Split.");
}

function getApiHostname(value: string): string {
  const hostname = requiredValue(value, "API_HOSTNAME");
  let parsed: URL;
  try {
    parsed = new URL(`https://${hostname}`);
  } catch {
    throw new Error("API_HOSTNAME must contain only a valid host name.");
  }

  if (
    parsed.username ||
    parsed.password ||
    parsed.host.toLowerCase() !== hostname.toLowerCase() ||
    parsed.pathname !== "/" ||
    parsed.search ||
    parsed.hash
  ) {
    throw new Error("API_HOSTNAME must contain only a valid host name.");
  }

  return parsed.host.toLowerCase();
}

function buildContainerEnvironment(env: Env, mode: RuntimeMode): Record<string, string> {
  const isSplit = mode !== "All";
  const servesApi = mode !== "Worker";
  const values: Record<string, string> = {
    "Hosting__Provider": "Cloudflare",
    "Hosting__Mode": mode,
    "State__Mode": isSplit ? "Shared" : "Local",
    "DatabaseProvider": "PostgreSQL",
    "ConnectionStrings__RefTestManagement": requiredSecret(
      env.POSTGRES_CONNECTION_STRING,
      "POSTGRES_CONNECTION_STRING",
    ),
    "Cloudflare__DataProtection__PfxBase64": requiredSecret(
      env.CLOUDFLARE_DATA_PROTECTION_PFX_BASE64,
      "CLOUDFLARE_DATA_PROTECTION_PFX_BASE64",
    ),
    "Cloudflare__DataProtection__PfxPassword": requiredSecret(
      env.CLOUDFLARE_DATA_PROTECTION_PFX_PASSWORD,
      "CLOUDFLARE_DATA_PROTECTION_PFX_PASSWORD",
    ),
    "Auth0__Domain": requiredValue(env.AUTH0_DOMAIN, "AUTH0_DOMAIN"),
    "Auth0__Audience": requiredValue(env.AUTH0_AUDIENCE, "AUTH0_AUDIENCE"),
    "Auth0__ManagementClientId": requiredValue(
      env.AUTH0_MANAGEMENT_CLIENT_ID,
      "AUTH0_MANAGEMENT_CLIENT_ID",
    ),
    "Auth0__ManagementClientSecret": requiredSecret(
      env.AUTH0_MANAGEMENT_CLIENT_SECRET,
      "AUTH0_MANAGEMENT_CLIENT_SECRET",
    ),
    "EmailConfiguration__BaseUrl": requiredValue(env.EMAIL_BASE_URL, "EMAIL_BASE_URL"),
    "EmailConfiguration__FromEmail": requiredValue(env.EMAIL_FROM_EMAIL, "EMAIL_FROM_EMAIL"),
    "EmailConfiguration__FromName": requiredValue(env.EMAIL_FROM_NAME, "EMAIL_FROM_NAME"),
    "EmailConfiguration__BrevoApiKey": requiredSecret(env.BREVO_API_KEY, "BREVO_API_KEY"),
    "EmailConfiguration__BrevoApiUrl": env.BREVO_API_URL,
  };

  if (isSplit) {
    values["RedisConfiguration__Endpoint"] = requiredSecret(
      env.REDIS_ENDPOINT,
      "REDIS_ENDPOINT",
    );
    values["RedisConfiguration__AuthenticationMode"] = "Password";
    values["PrivacyChallengeConfiguration__RateLimitBackend"] = "Redis";
    values["PrivacyChallengeConfiguration__HmacSecret"] = requiredSecret(
      env.PRIVACY_CHALLENGE_HMAC_SECRET,
      "PRIVACY_CHALLENGE_HMAC_SECRET",
    );
  } else {
    values["PrivacyChallengeConfiguration__RateLimitBackend"] = "Local";
  }

  if (servesApi) {
    values["AllowedHosts"] = `${getApiHostname(env.API_HOSTNAME)};${CONTAINER_HOSTNAME}`;
    values["Auth0__ClientId"] = requiredValue(env.AUTH0_CLIENT_ID, "AUTH0_CLIENT_ID");
    values["Auth0__ClientSecret"] = requiredSecret(
      env.AUTH0_CLIENT_SECRET,
      "AUTH0_CLIENT_SECRET",
    );
    values["Cloudflare__ProxyAssertionSecret"] = getProxyAssertionSecret(
      env.CLOUDFLARE_PROXY_ASSERTION_SECRET,
    );
    values["RulesQuestions__Url"] = env.RULES_QUESTIONS_URL;
  }

  return values;
}

function getProxyAssertionSecret(value: string | undefined): string {
  const secret = requiredSecret(value, "CLOUDFLARE_PROXY_ASSERTION_SECRET");
  if (new TextEncoder().encode(secret).length < 32)
    throw new Error("CLOUDFLARE_PROXY_ASSERTION_SECRET must contain at least 32 UTF-8 bytes.");

  return secret;
}

function requiredSecret(value: string | undefined, name: string): string {
  return requiredValue(value, name);
}

function requiredValue(value: string | undefined, name: string): string {
  if (!value || value.trim().length === 0)
    throw new Error(`${name} must be configured.`);

  return value;
}
