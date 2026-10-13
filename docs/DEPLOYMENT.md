# Deployment guide

The application has provider-neutral process settings and provider-specific infrastructure:

| Target | Combined deployment | Split deployment | Infrastructure/code ownership |
| --- | --- | --- | --- |
| Local | API runs `Hosting:Mode=All` with `State:Mode=Local` | API `Api` plus Worker `Worker`; requires shared Redis | Podman for local services/validation; no cloud resources |
| Azure Container Apps | One API app runs HTTP and background services | API app plus Worker app and manual migration job | Terraform provisions Azure; GitHub Actions deploys OCI images |
| Cloudflare Workers/Containers | `AppContainer` runs API and background services | `AppContainer` API plus `WorkerContainer` | Terraform provisions R2 state/custom domain; Wrangler owns the Worker and images |

`Hosting:Mode`, `Hosting:Provider`, `State:Mode`, and `DatabaseProvider` are independent choices.
See [the configuration reference](CONFIGURATION.md), [the hosting ADR](adr/0003-container-hosting.md),
and [the Terraform runbook](../infra/terraform/README.md).

Azure Terraform selects the database independently for testing and production. `SqlServer` remains
the default; PostgreSQL and MySQL use managed Azure Database Flexible Servers, while SQLite is
restricted to the single API mode with a persistent Azure Files share.

## Azure Container Apps

### Prerequisites and identity

- Create the shared ACR and Azure Blob Terraform state using the operator-run
  `infra/terraform/bootstrap` root. `infra-deploy.yml` manages only the testing/production
  environment roots; it does not bootstrap subscriptions or resource groups.
- Use separate `testing` and `production` GitHub Environments. Configure required reviewers and
  branch restrictions in GitHub settings; workflow YAML cannot create or prove those approvals.
- Configure GitHub OIDC federated credentials for the Azure client ID and the relevant environment
  subject `repo:<owner>/<repo>:environment:testing` or
  `repo:<owner>/<repo>:environment:production`, with the `api://AzureADTokenExchange` audience.
  See Microsoft's
  [GitHub Actions OIDC guidance](https://learn.microsoft.com/azure/developer/github/connect-from-azure-openid-connect).
  Do not store a client secret. The Terraform principal needs the state-storage blob role,
  appropriately scoped resource-group write access, and conditional role-assignment permissions.
  The release principal needs `AcrPush` and the least Azure permissions required to update Container
  Apps and start/read the migration job. SQLite releases also require
  `Microsoft.App/containerapps/stop/action` and `Microsoft.App/containerapps/start/action` on the API
  app for the migration maintenance window. See the RBAC guidance in the
  [Terraform runbook](../infra/terraform/README.md#bootstrap).
- Give each app environment its own selected database, Key Vault, UAMIs, and Terraform state key.
  `SqlServer` uses Entra-only Azure SQL. PostgreSQL/MySQL use managed Flexible Servers with
  provider-specific Key Vault connection strings and seven-day service-managed backups. SQLite uses
  a dedicated persistent Azure Files share, single mode only, with no automated backup. The API,
  optional Worker, and migration job retain separate user-assigned managed identities for Key Vault
  access.

### GitHub Environment configuration

Configure the following **secrets** in both GitHub Environments. The same OIDC identity may be used
for both environments only if its federated credentials and RBAC scopes are intentionally set up
for that arrangement.

| Secret | Purpose |
| --- | --- |
| `AZURE_CLIENT_ID` | Client ID with GitHub OIDC federation |
| `AZURE_TENANT_ID` | Microsoft Entra tenant |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription |
| `AZURE_DATABASE_ADMIN_PASSWORD` | Managed PostgreSQL/MySQL server administrator password; required only when either provider is selected |

Configure the following **variables** per environment. The Terraform workflow validates required
values before requesting an OIDC token. `AZURE_API_IMAGE_DIGEST` and
`AZURE_WORKER_IMAGE_DIGEST` must be complete immutable references such as
`<acr>.azurecr.io/ref-test-management-api@sha256:<64-lowercase-hex>` and
`<acr>.azurecr.io/ref-test-management-worker@sha256:<64-lowercase-hex>`.
These digest variables seed newly created apps. After creation, the Azure release workflows own
image revisions and Terraform ignores changes to those image fields.

| Group | Variables |
| --- | --- |
| Terraform state | `AZURE_STATE_RESOURCE_GROUP_NAME`, `AZURE_STATE_STORAGE_ACCOUNT_NAME`, `AZURE_STATE_CONTAINER_NAME` |
| Azure resource names | `AZURE_RESOURCE_GROUP_NAME`, optional `AZURE_LOCATION`, `AZURE_NAME_PREFIX`, `AZURE_UNIQUE_SUFFIX`, `AZURE_ACR_NAME`, `AZURE_ACR_RESOURCE_GROUP_NAME`, `AZURE_KEY_VAULT_NAME` |
| Database choice | `AZURE_DATABASE_PROVIDER` (`SqlServer`, `PostgreSQL`, `MySQL`, or `SQLite`); `SqlServer` is the default |
| Managed database cost/password | `AZURE_ALLOW_MANAGED_DATABASE_PROVIDERS`, `AZURE_DATABASE_ADMIN_LOGIN`, `AZURE_DATABASE_ADMIN_PASSWORD_VERSION` (PostgreSQL/MySQL only) |
| Azure SQL administrator | `AZURE_SQL_SERVER_NAME`, `AZURE_SQL_ENTRA_ADMIN_LOGIN_USERNAME`, `AZURE_SQL_ENTRA_ADMIN_OBJECT_ID` (`SqlServer` only) |
| Initial container images | `AZURE_API_IMAGE_DIGEST`, `AZURE_WORKER_IMAGE_DIGEST` |
| Topology and trust | `AZURE_DEPLOYMENT_MODE` (`single` or `split`), `AZURE_ALLOW_SPLIT_MODE`, `AZURE_API_MAX_REPLICAS`, `AZURE_API_TRUSTED_PROXY_NETWORKS_JSON` |
| Network rules | `AZURE_SQL_FIREWALL_IP_ADDRESSES_JSON` (server database egress allowlist), `AZURE_KEY_VAULT_FIREWALL_ENABLED`, `AZURE_KEY_VAULT_ALLOWED_IP_ADDRESSES_JSON` |
| Application settings | `AUTH0_DOMAIN`, `AUTH0_CLIENT_ID`, `AUTH0_AUDIENCE`, `AUTH0_MANAGEMENT_CLIENT_ID`, `EMAIL_BASE_URL`, `EMAIL_FROM_EMAIL`, `EMAIL_FROM_NAME`, `RULES_QUESTIONS_URL` |
| Logging | `AZURE_LOG_ANALYTICS_DAILY_QUOTA_GB` |

List variables must be JSON arrays, for example `["203.0.113.10"]`. Do not populate
`AZURE_API_TRUSTED_PROXY_NETWORKS_JSON` with guessed, broad, or catch-all networks. Applications
remain disabled until the actual ACA ingress peer CIDRs are verified. Split mode additionally
requires `AZURE_ALLOW_SPLIT_MODE=true`; keep API replicas at one while GraphQL rate limiting is
process-local. PostgreSQL/MySQL require `AZURE_ALLOW_MANAGED_DATABASE_PROVIDERS=true`; this is an
opt-in, not a spend cap. SQLite requires `AZURE_DEPLOYMENT_MODE=single`.

`azure-container-release.yml` also reads `AZURE_ACR_NAME`, `AZURE_RESOURCE_GROUP_NAME`,
`AZURE_NAME_PREFIX`, `AZURE_UNIQUE_SUFFIX`, and `AZURE_DEPLOYMENT_MODE` from its target GitHub
Environment, plus `AZURE_DATABASE_PROVIDER` (default `SqlServer`). Keep these values aligned with
Terraform; before migration the release workflow verifies the setting against the deployed API and
migration job and fails closed on a mismatch. The complete resource and secret setup is in the
[Azure rollout runbook](../infra/terraform/README.md#environment-rollout).

Container deployment is opt-in through the repository Actions variable
`CONTAINER_DEPLOYMENT_TARGET`:

| Value | Release target |
| --- | --- |
| Unset or `app-service` | Existing App Service workflows remain the default |
| `azure-container-apps` | Alpha releases deploy to ACA testing; stable releases deploy to ACA production |
| `cloudflare-containers` | Alpha releases deploy to Cloudflare testing; stable releases deploy to Cloudflare production |

Set the variable in repository Settings → Secrets and variables → Actions → Variables. The
beta/stable release workflows validate it before publishing. The selected reusable provider
workflow builds and verifies the exact release tag before provider authentication; production still
requires the configured protected Environment approval. Returning to `app-service` (or unsetting
the variable) switches future releases back to the Web App without changing workflow files.
The stable workflow is dispatched from the `release` branch, so ensure that branch contains the
updated selector before changing the variable for a production release. The first promotion after
this workflow change may fast-forward `release` while the current `app-service` target remains in
effect; subsequent stable runs use the selector.

### First environment rollout

1. Build the API and Worker OCI archives with the .NET SDK `PublishContainer` target and push them to
   the bootstrapped ACR. The API image must include the Angular production build in
   `RefTestManagement.Api/wwwroot`. The SDK emits OCI archives; no Dockerfile or Docker daemon is
   required.
2. Set the two image-digest variables above. Run `infra-deploy.yml` from `main` with the target
   environment and `operation=plan`; inspect the result. The default `operation` is `plan`, and the
   `create_applications` checkbox defaults to false.
3. Apply the foundation with `create_applications=false`. Terraform provisions the selected database,
   Key Vault, UAMIs, the ACA environment, and foundational role assignments without creating app
   resources. PostgreSQL/MySQL require the protected `AZURE_DATABASE_ADMIN_PASSWORD` secret and the
   explicit managed-database cost opt-in.
4. For `SqlServer`, as the configured Entra SQL administrator, run
   `infra/terraform/bootstrap/sql-identities.sql` with the API, Worker, and migration identity
   outputs. For PostgreSQL/MySQL, provision separate runtime and migration database users with the
   managed server administrator, then store their TLS connection strings in Key Vault. SQLite needs
   no database-user secret; Terraform creates its dedicated file share. Required Key Vault names and
   identity scopes are listed in the [Terraform runbook](../infra/terraform/README.md#environment-rollout).
5. Before creating public applications, provide the verified ACA ingress peer CIDRs and confirm
   the selected server database and Key Vault egress rules. SQLite does not use a database firewall.
   The Terraform validation deliberately blocks app creation with an empty or unsafe trusted-proxy
   list. If the peer addresses cannot be verified, stop here.
6. Run `infra-deploy.yml` with `operation=apply` and `create_applications=true`. The plan job first
   waits for the target GitHub Environment gate, creates the Terraform plan, and publishes it in the
   run summary. Review that plan, then approve the separate apply job through the same protected
   environment. The apply job verifies the saved plan's SHA-256 and applies that exact plan from the
   same dispatched commit; it does not re-plan. The saved-plan artifact expires after one day.
   After apps exist, an unchecked `create_applications` input is overridden to true when the API
   app is present in Terraform state, so routine workflow use cannot destroy the apps. Direct
   Terraform runs must keep `create_applications=true`; setting it false directly destroys app and
   migration-job resources. The workflow does not expose app deletion.
7. For SQL Server/PostgreSQL/MySQL, add the exact ACA egress addresses to
   `sql_firewall_ip_addresses` after first creation and apply again. Verify API readiness and
   database connectivity before releasing application changes.

Provider selection is a provisioning-time choice. Database resources are protected from routine
destroy; changing a provider does not migrate data and requires a separate migration plan. Azure
managed PostgreSQL/MySQL backups retain seven days of data. SQLite's Azure Files key is stored in
protected Terraform state, and its persistent share has no automated backup.

For a non-mutating preview, dispatch with `operation=plan`; it has one environment gate and produces
no apply job. For a change, dispatch with `operation=apply`: the same target Environment requires
an approval before planning and another approval after the plan summary is available. Configure
required reviewers in GitHub Environment settings or the workflow has no human approval gate. Do
not run an apply until the target, cost, ingress CIDRs, RBAC, state backend, and exact plan are
understood.

### Release and migration order

- PR checks build/test the application, validate SDK OCI archive publishing, and validate all six
  Azure/Cloudflare Terraform roots with the backend disabled. PR jobs do not authenticate to Azure,
  push images, or apply infrastructure.
- `beta-release.yml` and `stable-release.yml` retain their existing validation and release order.
  The repository variable `CONTAINER_DEPLOYMENT_TARGET` selects the default Web App path or calls
  the corresponding reusable container workflow on a release.
- `container-build.yml` builds the exact alpha/stable tag, runs the required validation, publishes
  OCI archives, and records their provenance. `azure-container-release.yml` and
  `cloudflare-container-release.yml` are reusable provider-specific deployment workflows.
- With `CONTAINER_DEPLOYMENT_TARGET=app-service` or unset, beta/stable releases deploy only to the
  testing/production Web Apps. With a container target selected, the corresponding provider
  workflow deploys alpha to testing or stable to production; unsupported tag/target combinations
  fail before provider credentials are used.
- Azure deployment verifies the artifact and archive hashes before OIDC login, pushes immutable ACR
  digests, updates and starts the one-shot migration job, and waits for success before updating the
  API and optional split-mode Worker revisions by digest. SQLite releases stop the single-mode API
  for the migration window, then restart it after the update or through the workflow cleanup path.
- Cloudflare deployment runs the selected Worker image's migration command before pushing the images
  and deploying the Worker with digest-pinned Container images. The GitHub runner must be allowed
  to reach the selected PostgreSQL server for this migration step.
- Terraform does not run database migrations. Keep migration changes backward-compatible with the
  currently deployed app while the job runs. The release identity's job-start permission is
  sensitive because the job has a database identity and Key Vault references.
- Azure split mode uses the Worker container for background services and the same Worker image for
  the one-shot migration command. The migration identity is distinct from the API/Worker identities.

### Initial Azure image seed

The first Terraform application resources need valid ACR image digests. Build OCI archives from the
validated source with the same .NET SDK target used by CI, then authenticate Podman with an Entra
token. Azure CLI documents [`DOCKER_COMMAND=podman`](https://learn.microsoft.com/azure/container-registry/container-registry-authentication#sign-in-by-using-an-alternative-container-tool-instead-of-docker)
for `az acr login`; do not enable the ACR admin account.

```powershell
# PowerShell 7.3+ propagates native command failures to the script.
$PSNativeCommandUseErrorActionPreference = $true
$ErrorActionPreference = "Stop"
$acrName = "your-acr-name"
$imageTag = (git rev-parse HEAD).Trim()
$imageDirectory = ".\artifacts\images"
$null = New-Item -ItemType Directory -Force $imageDirectory
$archiveDirectory = (Resolve-Path $imageDirectory).Path

# Build the UI and copy it into the API image, as the release workflow does.
dotnet restore .\RefTestManagement.slnx
Push-Location .\RefTestManagement.Ui
npm ci
npm run build -- --configuration production
Pop-Location
$null = New-Item -ItemType Directory -Force .\RefTestManagement.Api\wwwroot
Copy-Item .\RefTestManagement.Ui\dist\RefTestManagement.Ui\browser\* `
  .\RefTestManagement.Api\wwwroot\ -Recurse -Force
dotnet publish .\RefTestManagement.Api\RefTestManagement.Api.csproj `
  --configuration Release --no-restore /t:PublishContainer `
  -p:ContainerRuntimeIdentifier=linux-x64 "-p:ContainerImageTag=$imageTag" `
  "-p:ContainerArchiveOutputPath=$archiveDirectory\ref-test-management-api.tar.gz"
dotnet publish .\RefTestManagement.Worker\RefTestManagement.Worker.csproj `
  --configuration Release --no-restore /t:PublishContainer `
  -p:ContainerRuntimeIdentifier=linux-x64 "-p:ContainerImageTag=$imageTag" `
  "-p:ContainerArchiveOutputPath=$archiveDirectory\ref-test-management-worker.tar.gz"

$subscriptionId = "your-subscription-id"
az login
az account set --subscription $subscriptionId
$env:DOCKER_COMMAND = "podman"
az acr login --name $acrName
$loginServer = (az acr show --name $acrName --query loginServer --output tsv).Trim()
podman load --input "$archiveDirectory\ref-test-management-api.tar.gz"
podman load --input "$archiveDirectory\ref-test-management-worker.tar.gz"
podman tag "ref-test-management-api:$imageTag" "$loginServer/ref-test-management-api:$imageTag"
podman tag "ref-test-management-worker:$imageTag" "$loginServer/ref-test-management-worker:$imageTag"
podman push "$loginServer/ref-test-management-api:$imageTag"
podman push "$loginServer/ref-test-management-worker:$imageTag"
$apiDigest = az acr repository show --name $acrName --image "ref-test-management-api:$imageTag" --query digest --output tsv
$workerDigest = az acr repository show --name $acrName --image "ref-test-management-worker:$imageTag" --query digest --output tsv
podman logout $loginServer
```

Use the returned digests to form the two immutable image references and set the matching GitHub
Environment variables. The logged-in Entra user needs `AcrPush` on the registry. Treat the local
ACR login token as a credential; do not print it, commit it, or place it in Terraform variables.

## Cloudflare Workers and Containers

Cloudflare is deployed separately from the Azure workflows. Wrangler owns Worker code, image
rollout, Durable Object bindings, the one-minute Cron trigger, and runtime secrets. Terraform owns
the local-state R2 bucket bootstrap, separate testing/production R2 state keys, and custom-domain
binding only. No Cloudflare PostgreSQL or Redis service is provisioned by this repository.

Set `CONTAINER_DEPLOYMENT_TARGET=cloudflare-containers` in repository Actions variables to opt into
the Cloudflare flow for future alpha/stable releases. The release workflows call
`cloudflare-container-release.yml` with the exact generated tag and testing/production target. It
deploys the matching Worker service and does not apply the Terraform custom-domain binding.

Create protected GitHub Environments named `cloudflare-testing` and `cloudflare-production`;
configure required reviewers for production. Set the following **variables** in each environment.
`CLOUDFLARE_WORKER_SERVICE_NAME` must exactly match the Cloudflare Terraform
`worker_service_name` for that environment.

| Variable | Purpose |
| --- | --- |
| `CLOUDFLARE_ACCOUNT_ID` | Cloudflare account ID for image registry and Wrangler |
| `CLOUDFLARE_WORKER_SERVICE_NAME` | Deployed service name, for example `ref-test-management-testing` |
| `APP_MODE`, `API_HOSTNAME` | Runtime topology and exact public API hostname |
| `AUTH0_DOMAIN`, `AUTH0_CLIENT_ID`, `AUTH0_AUDIENCE`, `AUTH0_MANAGEMENT_CLIENT_ID` | Auth0 application/API settings |
| `EMAIL_BASE_URL`, `EMAIL_FROM_EMAIL`, `EMAIL_FROM_NAME`, `BREVO_API_URL` | Email sender settings |
| `RULES_QUESTIONS_URL` | IHF rules/questions API endpoint |

Set these **secrets** in each Cloudflare Environment. Use a dedicated API token with only the
Workers/Containers permissions needed by Wrangler; do not reuse Terraform's R2 backend credentials.

| Secret | Use |
| --- | --- |
| `CLOUDFLARE_API_TOKEN` | Wrangler image push and Worker deployment |
| `POSTGRES_CONNECTION_STRING` | Worker runtime and tagged Worker migration command |
| `CLOUDFLARE_DATA_PROTECTION_PFX_BASE64`, `CLOUDFLARE_DATA_PROTECTION_PFX_PASSWORD` | Persistent Data Protection key encryption |
| `AUTH0_MANAGEMENT_CLIENT_SECRET`, `BREVO_API_KEY` | Worker runtime |
| `AUTH0_CLIENT_SECRET`, `CLOUDFLARE_PROXY_ASSERTION_SECRET` | API-serving modes (`All` or `Split`) |
| `REDIS_ENDPOINT`, `PRIVACY_CHALLENGE_HMAC_SECRET` | `Split` mode only |

The migration runs from the GitHub-hosted runner before Worker deployment, so the selected
PostgreSQL service must accept that runner's egress. If it cannot, do not dispatch the workflow;
use the authorized migration procedure in the runbook instead.

### Configuration and secrets

Deploy testing and production as distinct Wrangler Worker services with `workers_dev` disabled.
Set these non-secret Worker variables per environment:

| Variable | Purpose |
| --- | --- |
| `APP_MODE` | `All` for one combined AppContainer, or `Split` for API plus WorkerContainer |
| `API_HOSTNAME` | Exact public hostname; must match the Terraform `api_hostname` |
| `AUTH0_DOMAIN`, `AUTH0_CLIENT_ID`, `AUTH0_AUDIENCE`, `AUTH0_MANAGEMENT_CLIENT_ID` | Auth0 application/API settings |
| `EMAIL_BASE_URL`, `EMAIL_FROM_EMAIL`, `EMAIL_FROM_NAME`, `BREVO_API_URL` | Email sender settings |
| `RULES_QUESTIONS_URL` | IHF rules/questions API endpoint |

Set these runtime values as Wrangler Secrets, not Terraform variables or checked-in `wrangler.jsonc`
values:

| Secret | Required |
| --- | --- |
| `POSTGRES_CONNECTION_STRING` | All modes; external PostgreSQL connection with TLS certificate validation |
| `CLOUDFLARE_DATA_PROTECTION_PFX_BASE64`, `CLOUDFLARE_DATA_PROTECTION_PFX_PASSWORD` | All modes; certificate protecting the database-backed key ring |
| `AUTH0_MANAGEMENT_CLIENT_SECRET`, `BREVO_API_KEY` | All modes |
| `AUTH0_CLIENT_SECRET`, `CLOUDFLARE_PROXY_ASSERTION_SECRET` | API-serving modes (`All` or `Split`) |
| `REDIS_ENDPOINT`, `PRIVACY_CHALLENGE_HMAC_SECRET` | `Split` mode only; Redis endpoint uses authenticated TLS |

The Worker checks its hostname and edge client address, strips client-supplied proxy headers, and
adds a private shared-secret assertion that the API validates. `CLOUDFLARE_PROXY_ASSERTION_SECRET`
must be at least 32 UTF-8 bytes and must match across Worker and API container configuration. In
split mode, Redis credentials must be accepted from Cloudflare Container egress. The API and Worker
must use the same PostgreSQL database and Data Protection certificate. Require TLS for PostgreSQL
and Redis connections; prefer full server-certificate verification and do not disable validation.

### Worker, state, and custom domain

1. Confirm the Cloudflare account has Workers Paid and Containers/Durable Objects availability.
   Confirm the external PostgreSQL and TLS Redis providers accept Cloudflare egress before
   production.
2. Build the Angular production assets and copy them into `RefTestManagement.Api/wwwroot` before
   publishing the API image; the API project does not build the SPA. Then build and push the API
   and Worker OCI images with the .NET SDK `PublishContainer` target and Wrangler. Replace the
   deliberate zero-digest image placeholders in `infra/cloudflare/worker/wrangler.jsonc` with the
   returned immutable registry digests:

   ```powershell
   # PowerShell 7.3+ propagates nonzero native-command exit codes.
   $PSNativeCommandUseErrorActionPreference = $true
   $ErrorActionPreference = "Stop"
   $null = New-Item -ItemType Directory -Force .\artifacts
   $archiveDirectory = (Resolve-Path .\artifacts).Path
   $imageTag = (git rev-parse HEAD).Trim()
   Push-Location .\RefTestManagement.Ui
   npm ci
   npm run build -- --configuration production
   Pop-Location
   $null = New-Item -ItemType Directory -Force .\RefTestManagement.Api\wwwroot
   Copy-Item .\RefTestManagement.Ui\dist\RefTestManagement.Ui\browser\* `
     .\RefTestManagement.Api\wwwroot\ -Recurse -Force
   dotnet publish .\RefTestManagement.Api\RefTestManagement.Api.csproj `
     --configuration Release /t:PublishContainer `
     -p:ContainerRuntimeIdentifier=linux-x64 `
     "-p:ContainerImageTag=$imageTag" `
     "-p:ContainerArchiveOutputPath=$archiveDirectory\ref-test-management-api.tar.gz"
   dotnet publish .\RefTestManagement.Worker\RefTestManagement.Worker.csproj `
     --configuration Release /t:PublishContainer `
     -p:ContainerRuntimeIdentifier=linux-x64 `
     "-p:ContainerImageTag=$imageTag" `
     "-p:ContainerArchiveOutputPath=$archiveDirectory\ref-test-management-worker.tar.gz"
   ```

   Push these OCI archives to the Cloudflare Container
   registry with the authorized Wrangler/Podman workflow for the account; deploy only the returned
   immutable digests. Do not publish the API image before copying the SPA assets.
3. Run the Worker image with `--migrate` from an authorized operator environment against the
   external PostgreSQL database before deploying the new Wrangler image. Cloudflare has no
   Terraform-managed migration job; the scheduled Worker wake does not apply schema changes. Use
   `DatabaseProvider=PostgreSQL` and the same connection string configured as
   `POSTGRES_CONNECTION_STRING`. See [Applying migrations](CONFIGURATION.md#applying-migrations).
   For example, with the Worker image already loaded in Podman and the connection string supplied
   through a protected host environment variable:

   ```powershell
   # Set $workerImage to the loaded Worker image tag or immutable registry reference.
   $PSNativeCommandUseErrorActionPreference = $true
   $ErrorActionPreference = "Stop"
   if ([string]::IsNullOrWhiteSpace($env:POSTGRES_CONNECTION_STRING)) { throw "Set POSTGRES_CONNECTION_STRING in the protected shell environment." }
   $env:DatabaseProvider = "PostgreSQL"
   $env:ConnectionStrings__RefTestManagement = $env:POSTGRES_CONNECTION_STRING
   podman run --rm --env DatabaseProvider --env ConnectionStrings__RefTestManagement $workerImage --migrate
   ```

   This command changes the external database schema. Confirm the target and use an authorized
   database credential; do not put the connection string in shell text or Terraform state.
4. Configure Wrangler variables and secrets, then deploy a distinct Worker service for each
   environment. The Worker validates the API hostname, routes requests to port 8080, and wakes the
   selected background container on its one-minute Cron schedule.
5. After Wrangler deployment, apply the Cloudflare environment Terraform root with its active
   `account_id`, `zone_id`, `api_hostname`, and deployed `worker_service_name`. Cloudflare creates
   the custom-domain DNS/TLS binding; Terraform does not transfer the domain, change its registrar,
   or manage nameserver settings. The zone must already be active.
6. Keep the R2 bootstrap state and its local state file safe, then migrate environment state to the
   separate R2 keys. `CLOUDFLARE_API_TOKEN` is for Cloudflare provider operations; the
   S3-compatible R2 backend uses separate `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY`
   credentials.

Cloudflare Containers/Durable Objects are subject to current account availability and platform
limits. The one-minute wake plus two-minute inactivity timeout can keep a background container
active for much of the month; the Terraform runbook's usage examples are not a complete cost
estimate or a spend cap. External database/cache costs, account allowances, logs, and egress must be
modeled separately. Wrangler dry-run/type checks do not validate live Container startup or egress;
run an authorized account smoke test before production. No live Cloudflare smoke test or deployment
was performed for this change.

## Local containers and Terraform

### Azure split topology in Podman Compose

Run the Azure split shape as one isolated local project from Git Bash with `uv`, Podman, .NET,
Node/npm, and OpenSSL available on `PATH`:

```bash
uv run --script scripts/start-azure-local.py
```

The launcher creates a separate rootless Podman WSL machine named `rtm-azure-local` (4 CPUs,
6 GiB memory, user-mode networking) if it does not exist, builds the Angular assets, publishes the
API and Worker OCI images with the .NET SDK, and starts `compose.azure-local.yaml` as project
`rtm-azure-local`. It uses a dedicated DNS-disabled bridge with static SQL/Redis addresses and
explicit host mappings, avoiding the user-systemd-bus requirement of rootless bridge DNS on this WSL
machine. It does not change the default Podman connection or touch containers, networks, or volumes
in other machines. The separate machine avoids changing/restarting an existing 2-GiB Podman machine.

On every run, the uv-launched Python script uses InquirerPy to display a styled checkbox list for
`SqlServer`, `PostgreSQL`, `SQLite`, and `MySQL`. Use Up/Down to move, Space to select, and Enter to
confirm exactly one provider; Ctrl+C cancels. No provider is preselected. The uv script metadata
resolves the pinned TUI dependency in uv's managed environment. Canceling closes the launcher before
Podman operations, and the launcher does not reuse the previous choice. The selected server-backed
provider starts only its matching database service. SQL Server remains bound to `127.0.0.1:11433`,
PostgreSQL to
`127.0.0.1:15432`, and MySQL to `127.0.0.1:13306`. SQLite starts no database server and stores its
database file in a persistent named volume shared by the migration, API, and Worker containers.
Each provider uses a separate persistent data volume; switching providers does not migrate or share
database contents.

The compose stack also runs authenticated TLS Redis, a one-shot Worker migration, separate API/Worker
containers, and a Caddy TLS edge. API and Worker use `Hosting:Provider=Local` and `State:Mode=Shared`;
local configuration comes from the image settings, API User Secrets, and Compose environment
overrides. The Worker sets `DOTNET_ENVIRONMENT=Development` because it uses the Generic Host. The API
is at `https://localhost:7039`; Caddy terminates TLS with the trusted .NET localhost development
certificate and proxies to the API over the isolated Compose network. No HTTP API port is published
to the host; API/Worker internal traffic remains HTTP. The selected provider's named data volume
persists after `down`.

If the launcher reports that the HTTPS development certificate is missing or untrusted, run
`dotnet dev-certs https --trust` once and rerun the launcher. The launcher checks certificate trust
and exports the certificate/key to ignored local files; it does not change the host certificate
trust store.

Configuration follows the .NET provider order: `appsettings.json`,
`appsettings.Development.json` when present, the API project's User Secrets, then environment
variables. The launcher does not generate or mount a local `appsettings.Development.json`; values
not overridden by User Secrets or Compose therefore come from the image's appsettings files. In
particular, base `PrivacyConfiguration` values remain in effect unless you provide local overrides.
The launcher copies the full API User Secrets file to an ignored path mounted read-only at the
standard .NET User Secrets location in both containers. The Worker image is published with the API
project's `UserSecretsId`, without changing the Worker project's host-development settings.
Optional `Auth0__<Key>` environment variables are passed through by Compose and override User
Secrets. Compose keeps the HTTPS email base URL at `https://localhost:7039` and sets
`EmailConfiguration__LogoBaseUrl` to the API's internal `http://10.250.241.12:8080` address for
logo downloads only. The selected local database provider and connection string, plus these email
URLs, override any matching User Secrets values. The launcher generates a provider-neutral local
database password; existing ignored `.env` files with `MSSQL_SA_PASSWORD` remain supported. Generated
local credentials, the copied User Secrets file, and TLS certificates are ignored.

The Auth0 Management M2M credentials allow permission refresh and approver lookup. The Worker also
runs additive permission sync at startup: it may add missing application permissions to the
resource server selected by `Auth0:Domain` and `Auth0:Audience`. Keep those settings pointed at the
intended Auth0 tenant/API; the required Management API scopes are listed in
[CONFIGURATION.md](CONFIGURATION.md#management-api-access-and-permission-freshness). The sync does
not assign user roles or modify user metadata. If the M2M client lacks the required read scopes,
login permission refresh fails closed. If resource-server read or update scopes are missing,
startup sync logs a sanitized failure and the app continues without syncing. Before building the
stack, the launcher requires non-empty `Auth0:Domain`, `ClientId`, `ClientSecret`, `Audience`,
`ManagementClientId`, and `ManagementClientSecret` values from the API project's User Secrets or
environment overrides. Its error names missing keys without showing values.

Because the full API User Secrets store is loaded, the Worker may send pending email jobs through
the configured Brevo account to the configured recipients, and the application may call the
configured IHF endpoint. These local external calls are enabled intentionally; keep the User
Secrets store pointed at the intended services and test recipients.

This reproduces split application behavior and shared TLS state, not Azure Container Apps,
managed identity, Key Vault, Entra-only SQL authentication, ingress, scaling, or revisions. The
server-backed providers use local-only database credentials, and SQLite uses a local file; never
reuse this configuration or its generated secrets outside local development.

To inspect or stop only this project:

```bash
podman --connection rtm-azure-local compose --project-name rtm-azure-local \
  --file compose.azure-local.yaml --env-file infra/local/azure/.env \
  --profile SqlServer --profile PostgreSQL --profile SQLite --profile MySQL ps
podman --connection rtm-azure-local compose --project-name rtm-azure-local \
  --file compose.azure-local.yaml --env-file infra/local/azure/.env \
  --profile SqlServer --profile PostgreSQL --profile SQLite --profile MySQL down
```

Do not add `--volumes`/`-v` when stopping; that deletes this project's provider data volumes. The
named Podman machine, its images, the external project network, and database volumes remain available
for the next run. Rerunning the launcher rebuilds/recreates this project's containers while
preserving provider data.

For a single SQL-only container without the API/Worker compose, the SQL Server Linux image is also
available. Use a strong local-only password and `TrustServerCertificate=True` only for development.
See Microsoft's [SQL Server Linux container quickstart](https://learn.microsoft.com/sql/linux/quickstart-install-connect-docker).

Podman can run the pinned Terraform CLI for offline `fmt`, backend-disabled `init`, and `validate`
across all six roots without cloud credentials. An authenticated Azure plan is also possible from a
Podman Terraform container with an untracked backend file and host-supplied `ARM_*` credentials;
the official Terraform image has no Azure CLI, so Azure CLI authentication is unavailable inside
that image. An authenticated `apply` is technically possible too, but it is a live cloud mutation
and is not covered by these validation steps. See
[offline validation and local authentication](../infra/terraform/README.md#offline-validation).
Never put credentials, plans, state, database passwords, or certificate bytes in the repository.

## Deployment gates

- Azure: verified ACA ingress peer CIDRs, app secrets, Entra SQL identities, current SQL/Key Vault
  egress, GitHub OIDC federation and RBAC, and target budget/region/quota.
- Cloudflare: Workers Paid and Container availability, active zone/hostname, runtime secrets and
  certificate, TLS connectivity, external-service egress acceptance, and refreshed provider costs.

No live Azure/Cloudflare plan or apply, workflow dispatch, Wrangler deployment, or resource creation
is performed by the repository validation commands.
