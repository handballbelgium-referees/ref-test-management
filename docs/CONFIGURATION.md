# Configuration Reference

Full reference for `RefTestManagement.Api/appsettings.json`. For local development, prefer [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) over editing `appsettings.json` directly — see [Using User Secrets](#using-user-secrets) below. Deployment-specific setup is in [docs/DEPLOYMENT.md](DEPLOYMENT.md).

## Full Example

```json
{
  "Hosting": {
    "Mode": "All",
    "Provider": "Local"
  },
  "State": {
    "Mode": "Local"
  },
  "DatabaseProvider": "SqlServer",
  "ConnectionStrings": {
    "RefTestManagement": "Server=localhost;Database=RefTestManagement;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Identity": {
    "Provider": "Auth0",
    "Auth0": {
      "Domain": "your-tenant.auth0.com",
      "ClientId": "your-client-id",
      "ClientSecret": "your-client-secret",
      "Audience": "your-api-identifier",
      "ManagementClientId": "your-m2m-client-id",
      "ManagementClientSecret": "your-m2m-client-secret"
    }
  },
  "EmailConfiguration": {
    "BaseUrl": "https://localhost:7039",
    "BrevoApiKey": "your-brevo-api-key",
    "BrevoApiUrl": "https://api.brevo.com/v3",
    "FromEmail": "noreply@yourdomain.com",
    "FromName": "IHF RefTest",
    "ScheduledDelayMinutes": 0
  },
  "RulesQuestions": {
    "Url": "https://your-question-bank-api.com/graphql"
  },
  "LanguageConfiguration": {
    "DefaultPhraseLanguage": "en",
    "EnabledLanguages": ["en", "nl", "fr", "de"]
  },
  "ScoreConfiguration": {
    "PassingPercentage": 80,
    "Correct": 1,
    "InCorrect": -1,
    "NotAnswered": 0,
    "NegativeScore": false,
    "PenalizeGuessingStrategy": false
  },
  "ReportConfiguration": {
    "RecipientEmails": []
  },
  "PrivacyConfiguration": {
    "ControllerName": "Your Name",
    "ControllerAddress": "Your Address",
    "ContactEmail": "privacy@yourdomain.com",
    "NoticeVersion": "1.0",
    "NoticeEffectiveDate": "2026-08-03",
    "RetentionYears": 3
  },
  "PrivacyChallengeConfiguration": {
    "PrivacyChallengeKeyLifetimeHours": 24,
    "RateLimitWindowSeconds": 60,
    "RequestRateLimitPermitLimit": 5,
    "ConfirmationRateLimitPermitLimit": 10,
    "CleanupIntervalMinutes": 15
  },
  "RefTestExpirationConfiguration": {
    "ExpirationCheckIntervalMinutes": 5,
    "StartupDelaySeconds": 30,
    "ExpirationIfNotStarted": "7.00:00:00"
  },
  "BackgroundJobConfiguration": {
    "PollingIntervalSeconds": 5,
    "LockDurationMinutes": 5,
    "MaxAttempts": 3,
    "BatchSize": 10,
    "StartupDelaySeconds": 10,
    "EnableCleanup": true,
    "CleanupIntervalHours": 24,
    "RetainCompletedJobsDays": 7,
    "RetainFailedJobsDays": 30
  },
  "AuditLogConfiguration": {
    "EnableCleanup": true,
    "CleanupIntervalHours": 24,
    "RetentionDays": 90
  },
  "GraphQlLimitsConfiguration": {
    "EnforceCostLimits": true,
    "MaxFieldCost": 20000,
    "MaxTypeCost": 5000,
    "EnableRateLimiting": true,
    "RateLimitPermitLimit": 300,
    "RateLimitWindowSeconds": 60,
    "RateLimitQueueLimit": 20
  }
}
```

### Hosting mode and shared state

`Hosting:Mode` controls which processes run the background services. The API accepts `All` or
`Api`; the Worker executable requires `Worker`. Run the API in `All` mode for one combined process,
or run the API in `Api` mode with a separate Worker in `Worker` mode:

| Value | API executable | Worker executable |
| ----- | ------------- | ----------------- |
| `All` | HTTP API and background services (default) | Rejected |
| `Api` | HTTP API only | Rejected |
| `Worker` | Rejected | Background services only; no HTTP listener |

`State:Mode` is provider-neutral. `Local` keeps subscriptions, session locks, and the privacy
challenge limiter in-process; use it only with one API instance and no separate Worker.
`Shared` requires an authenticated TLS Redis endpoint and Redis-backed privacy challenge limiting.
Use `Shared` for API/Worker split deployments or any API scale-out: Worker-published events must
reach API subscribers, and a participant's session lock and privacy limiter must be shared too.
The Worker executable rejects `Local`.

Set `RedisConfiguration:Endpoint` to an authenticated TLS URI (`rediss://:password@host:port`).
`RedisConfiguration:AuthenticationMode` defaults to `Password`. Azure split mode can use `Token`
and an attached user-assigned identity.
The API and Worker use one shared connection for Hot Chocolate pub/sub, the 90-second participant
session-lock lease, and the Redis privacy limiter. The endpoint is required when `State:Mode=Shared`
or the privacy limiter backend is `Redis`; it is otherwise optional. Provide it through the hosting
environment or a secret reference, never source control. Use Redis 7.0 or later and a dedicated
deployment/ACL namespace for this application; grant the limiter and session-lock commands plus
`PUBLISH` and `SUBSCRIBE` for pub/sub.

`Hosting:Provider` selects the host's Data Protection key-encryption adapter. It defaults to
`Local` for development. The current values are `Local` and `Azure`; this setting
selects the host adapter, not the database or combined/split topology. The database-backed key ring
is always used; API and Worker must point to the same database and retain the `RefTestManagement`
application name. Set `Azure` for Azure App Service deployments to wrap key-ring entries with Azure Key Vault. Configure
`Azure:KeyVault:DataProtectionKeyIdentifier` as an HTTPS, versionless key URI (for example
`https://<vault>.vault.azure.net/keys/<name>`). An optional
`Azure:KeyVault:ManagedIdentityClientId` selects a user-assigned identity; otherwise the adapter
uses the system-assigned managed identity. Grant that identity the Key Vault Crypto User role, and
retain old Key Vault key versions while any data-protection key references them.

Other cloud providers should supply their own host adapter rather than changing `Hosting:Mode` or
`State:Mode`.

Subscription events are live-refresh hints published after the change commits. Publishing is best
effort: during a Redis outage the change still succeeds, a warning is logged, and open screens
catch up on their next query. Emails do not depend on this; they are queued as jobs in the same
transaction as the change.

### Shared privacy challenge limiter

`PrivacyChallengeConfiguration:RateLimitBackend` defaults to `Local`, which uses process-local
state and is safe only when exactly one API instance is running. Select `Redis` and configure
`RedisConfiguration:Endpoint` and `PrivacyChallengeConfiguration:HmacSecret` before selecting
`State:Mode=Shared`; `Shared` requires the Redis limiter, and `Redis` without an endpoint fails at
startup. The limiter uses an atomic `MULTI`/`EXEC` transaction with `INCR`,
conditional `EXPIRE`/`PEXPIRE NX` (`ExpireWhen.HasNoExpiry`), and `PTTL` verification; grant the
configured identity permission for those commands. Operations have a one-second deadline, do not
retry in application code, and deny only the privacy challenge operation during an outage. Provide
the HMAC secret like the endpoint; rotating it changes all active bucket keys and effectively
resets quotas.

Configure a reachable shared Redis service in both API and Worker environments before selecting
`State:Mode=Shared`; this application does not provision infrastructure. Before rollout, verify
ingress proxy addresses/networks in `ForwardedHeadersConfiguration` and verify that the trusted
client-IP resolver returns the actual client address. Do not trust forwarded headers from untrusted
peers. GraphQL rate limits remain per API instance, so deployments that scale the API horizontally
need a shared/global GraphQL limiter.

## Sections

The API validates each settings section below when it starts, and refuses to start if a value is
out of range; the error names the section. Besides the rules listed per section, these bounds
apply:

| Section                          | Rule                                                                                                                                       |
| -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| `BackgroundJobConfiguration`     | `PollingIntervalSeconds`, `LockDurationMinutes`, `MaxAttempts`, `BatchSize`, `CleanupIntervalHours` ≥ 1; `StartupDelaySeconds` and the `Retain*Days` values ≥ 0 |
| `EmailConfiguration`             | `ScheduledDelayMinutes` ≥ 0                                                                                                                |
| `GraphQlLimitsConfiguration`     | `MaxFieldCost`, `MaxTypeCost`, `RateLimitPermitLimit`, `RateLimitWindowSeconds` ≥ 1; `RateLimitQueueLimit` ≥ 0                            |
| `ScoreConfiguration`             | `PassingPercentage` from `0` through `100`                                                                                                 |
| `RefTestExpirationConfiguration` | `ExpirationCheckIntervalMinutes` ≥ 1; `StartupDelaySeconds` ≥ 0; `ExpirationIfNotStarted` at least one second                              |
| `ForwardedHeadersConfiguration`  | `ForwardLimit` ≥ 1                                                                                                                         |

### Hosting

| Key | Description | Default |
| --- | --- | --- |
| `Mode` | API executable role: `All` (HTTP and workers) or `Api` (HTTP only); the Worker executable requires `Worker` | `All` for API, `Worker` for Worker |
| `Provider` | Data Protection key-encryption adapter selected by the host | `Local` |

Set `Hosting:Provider=Azure` in Azure deployments. The Azure adapter requires
`Azure:KeyVault:DataProtectionKeyIdentifier`; see [Data Protection key ring](SECURITY.md#data-protection-key-ring).
Future providers must add their own host adapter.

### State

| Key | Description | Default |
| --- | --- | --- |
| `Mode` | `Local` for a single API with no separate Worker; `Shared` for API/Worker split or API scale-out | `Local` |

`Shared` requires Redis and `PrivacyChallengeConfiguration:RateLimitBackend=Redis`. The Worker
executable requires `Shared`. These settings are independent of the cloud provider and container
platform; do not use platform environment variables to infer the state topology.

### DatabaseProvider

| Key                | Description                           | Required | Default       |
| ------------------ | ------------------------------------- | -------- | ------------- |
| `DatabaseProvider` | Selects the EF Core database provider | No       | `"SqlServer"` |

The application supports the database providers below; this repository does not provision Azure
database resources. Configure database services and backups through the hosting provider or
operator. Shared-state/split mode requires a TLS Redis endpoint.

Valid values:

| Value        | Provider                                        | Connection string format                                                       |
| ------------ | ----------------------------------------------- | ------------------------------------------------------------------------------ |
| `SqlServer`  | SQL Server / Azure SQL                          | `Server=...;Database=...;Trusted_Connection=True;TrustServerCertificate=True;` |
| `PostgreSQL` | PostgreSQL (via Npgsql)                         | `Host=...;Database=...;Username=...;Password=...`                              |
| `SQLite`     | SQLite                                          | `Data Source=RefTestManagement.db`                                             |
| `MySQL`      | MySQL / MariaDB (via MySql.EntityFrameworkCore) | `Server=...;Database=...;User=...;Password=...;`                               |

For managed database services, use a provider connection string with TLS and full
server-certificate validation.

### ConnectionStrings

| Key                 | Description                                                       | Required |
| ------------------- | ----------------------------------------------------------------- | -------- |
| `RefTestManagement` | Database connection string (format depends on `DatabaseProvider`) | Yes      |

### Auth0

| Key                      | Description                                                              | Required |
| ------------------------ | ------------------------------------------------------------------------ | -------- |
| `Domain`                 | Auth0 tenant domain                                                      | Yes      |
| `ClientId`               | Auth0 OIDC application client ID used for interactive sign-in and sign-out | Yes      |
| `ClientSecret`           | Auth0 application client secret                                          | Yes      |
| `Audience`               | Auth0 API identifier                                                     | Yes      |
| `ManagementClientId`     | Client ID for a Machine-to-Machine app authorized for the Management API | Yes      |
| `ManagementClientSecret` | Secret for the Management API M2M app                                    | Yes      |

`ClientId` identifies the OIDC application; it is not the API token audience. `Audience` is the
Auth0 API resource-server identifier sent during OIDC sign-in and validated on bearer tokens.
`ManagementClientId` and `ManagementClientSecret` belong to a separate machine-to-machine app.

The Management API credentials are used by `RefTestManagement.Auth0` to sync permissions shortly after startup (`PermissionSyncService`, running in the background), resolve approvers by permission, and refresh each user's effective permissions for authorization. The refresh uses the existing machine-to-machine client-credentials flow. End-user sign-in refreshes the user's token separately through the refresh-token flow described in [Identity permission freshness](#identity-permission-freshness).

Startup permission sync skips with a sanitized warning when required Auth0 configuration is missing.
Transient network errors, timeouts, circuit-breaker rejections, and HTTP 408/429/5xx responses
trigger at most four retries of the sync operation (five operation attempts total), with 2, 4, 8,
and 16 second delays. Persistent failure is reported through a sanitized warning without blocking
startup; shutdown cancels a pending retry delay.

#### Management API access and permission freshness

The current Auth0 endpoint reference documents these least-privilege Management API scopes:

- Refresh current effective grants with `read:users` for `GET /api/v2/users/{id}` and
  `GET /api/v2/users/{id}/permissions`, `read:users read:roles read:role_members` for
  `GET /api/v2/users/{id}/roles`, and `read:roles` for
  `GET /api/v2/roles/{id}/permissions`. See Auth0's [Get a User](https://auth0.com/docs/api/management/v2/users/get-users-by-id),
  [Get a User's Permissions](https://auth0.com/docs/api/management/v2/users/get-permissions),
  [Get a user's roles](https://auth0.com/docs/api/management/v2/users/get-user-roles), and
  [Get permissions granted by role](https://auth0.com/docs/api/management/v2/roles/get-role-permission)
  references.
- Resolve approvers in `GetUsersWithPermissionAsync` (Keycloak equivalent: see [Keycloak user lookup](KEYCLOAK.md#user-lookup)) with `read:roles` for
  `GET /api/v2/roles` and `/api/v2/roles/{id}/permissions`, `read:users read:roles read:role_members` for
  `GET /api/v2/roles/{id}/users`, and `read:users` for `GET /api/v2/users` and
  `/api/v2/users/{id}/permissions`. See Auth0's [Get roles](https://auth0.com/docs/api/management/v2/roles/get-roles),
  [Get a role's users](https://auth0.com/docs/api/management/v2/roles/get-role-user), [List or
  Search Users](https://auth0.com/docs/api/management/v2/users/get-users), and permissions
  references above.
- Sync the API resource server at startup with `read:resource_servers` for
  `GET /api/v2/resource-servers` and `update:resource_servers` for
  `PATCH /api/v2/resource-servers/{id}`. See Auth0's [Get resource servers](https://auth0.com/docs/api/management/v2/resource-servers/get-resource-servers)
  and [Update a resource server](https://auth0.com/docs/api/management/v2/resource-servers/patch-resource-servers-by-id)
  references.

The application does not write user metadata, assign users to roles, or request unrelated
Management API access. The M2M scope grant in the target tenant was not inspected; confirm the
application is authorized for the scopes above before deployment. If any are missing, an Auth0
administrator must authorize them for the M2M application.

Successful permission snapshots are cached per user and API process for `Identity:PermissionCacheSeconds`
seconds (default `240`, must be positive). Shorter values pick up role changes sooner at the cost of more
identity-provider API calls. Concurrent checks for one user share a snapshot refresh, and an internal limit permits at most
four simultaneous refreshes per API process. Expired snapshots are never reused after a refresh
failure; authorization fails closed. Tenant/plan rate limits were not verified, and no quota figure is assumed.
Monitor the Auth0
tenant's own rate-limit signals and verify its operational limits before rollout.

### Identity permission freshness

| Key                                               | Purpose                                                                                       | Default |
| ------------------------------------------------- | --------------------------------------------------------------------------------------------- | ------- |
| `Identity:LivePermissionTimeoutSeconds`           | Timeout for the live identity-provider check on `live:` mutation policies. Must be positive. | `2`     |

- **Mutations** (`live:` policies) check the provider directly when the cache grants the permission, so a
  revocation takes effect on the next write, bounded by this timeout. A timeout or provider failure denies the write.
  Each write therefore adds one Management API (Auth0) or Admin API (Keycloak) call per user; the cache
  limits this to users who already hold the permission.
- **Subscriptions** re-check the live permission every 30 seconds (fixed constant, not configurable) and before
  events that follow a check older than 30 seconds.
- **Sign-in and token refresh** force a fresh permission snapshot, bypassing the cache. The authentication
  cookie refreshes its access token when it is within 60 seconds of expiry (fixed).

Refresh tokens are requested with `offline_access`, and both providers must issue them to the web client:

- **Auth0:** enable the *Refresh Token* grant on the application (Application → Advanced Settings → Grant Types),
  and include `offline_access` in the allowed scopes.   Rotation support is not verified against a live tenant;
    confirm the tenant's rotation setting before rollout.
- **Keycloak:** the `offline_access` client scope must be assigned to the client in use (documented as `reftest-beta` in [KEYCLOAK.md](KEYCLOAK.md)).
  Confirm that the realm's SSO and offline session limits suit the expected session length.

If the provider issues no refresh token, the access token cannot be renewed: the user is signed out when it nears
expiry (60 seconds before) and must sign in again.

### EmailConfiguration

| Key                     | Description                                  | Required | Default |
| ----------------------- | -------------------------------------------- | -------- | ------- |
| `BaseUrl`               | Base URL used to build email links and the default logo fetch URL | Yes | – |
| `LogoBaseUrl`           | Optional base URL used only to fetch the logo; defaults to `BaseUrl` | No | `BaseUrl` |
| `BrevoApiKey`           | Brevo API key                                | Yes      | –       |
| `BrevoApiUrl`           | Brevo API endpoint                           | Yes      | –       |
| `FromEmail`             | Sender email address                         | Yes      | –       |
| `FromName`              | Sender display name                          | Yes      | –       |
| `ScheduledDelayMinutes` | Delay in minutes applied to scheduled emails | No       | `0`     |

Emails sent by background jobs carry a Brevo `idempotencyKey` derived from the job, recipient and
subject. If a send's response is lost and the job retries, Brevo recognises the key (it remembers
keys for 30 minutes) and answers `duplicate_parameter`, which the API treats as already delivered.
Job retries run within seconds, well inside that window. Emails sent directly by a request, outside
a job, carry no key.

### RulesQuestions

| Key   | Description                                 | Required |
| ----- | ------------------------------------------- | -------- |
| `Url` | External IHF question-bank GraphQL endpoint | Yes      |

### LanguageConfiguration

| Key                     | Description                                         | Required | Default  |
| ----------------------- | --------------------------------------------------- | -------- | -------- |
| `DefaultPhraseLanguage` | Default language for question phrasing              | Yes      | –        |
| `EnabledLanguages`      | Array of enabled UI languages (`en`/`nl`/`fr`/`de`) | No       | all four |

Unsupported non-empty `EnabledLanguages` values cause the API to fail startup with a configuration error.

### ScoreConfiguration

| Key                        | Description                                     | Default |
| -------------------------- | ----------------------------------------------- | ------- |
| `PassingPercentage`        | Percentage required to pass a RefTest           | `80`    |
| `Correct`                  | Points for each correct answer selected         | `1`     |
| `InCorrect`                | Points for each incorrect answer selected       | `-1`    |
| `NotAnswered`              | Points for each correct answer NOT selected     | `0`     |
| `NegativeScore`            | Allow a negative score per question             | `false` |
| `PenalizeGuessingStrategy` | Zero the question score if all answers selected | `false` |

### ReportConfiguration

| Key               | Description                                | Default |
| ----------------- | ------------------------------------------ | ------- |
| `RecipientEmails` | Emails to receive automated system reports | `[]`    |

### PrivacyConfiguration

| Key                   | Description                                                               | Default |
| --------------------- | ------------------------------------------------------------------------- | ------- |
| `ControllerName`      | Data controller name shown in the privacy notice                          | –       |
| `ControllerAddress`   | Data controller correspondence address                                    | –       |
| `ContactEmail`        | Privacy contact email                                                     | –       |
| `NoticeVersion`       | Current privacy-notice version string; participants must accept it        | –       |
| `NoticeEffectiveDate` | Effective date of the current notice version                              | –       |
| `RetentionYears`      | Years to retain terminal RefTests before automatic anonymization; accepted range is 1–3 years | `3` |

See [docs/PRIVACY.md](PRIVACY.md) for how retention and erasure actually work.

### PrivacyChallengeConfiguration

Controls public personal-data export and consent-withdrawal verification. For example,
`PrivacyChallengeConfiguration:PrivacyChallengeKeyLifetimeHours` (environment variable
`PrivacyChallengeConfiguration__PrivacyChallengeKeyLifetimeHours`) sets how long an emailed
verification key remains valid for either flow. The API validates these values at startup and
rejects invalid configuration. Deployments must use the `PrivacyChallengeConfiguration` section.

| Key                                   | Description                                                         | Default | Validation                    |
| ------------------------------------- | ------------------------------------------------------------------- | ------- | ----------------------------- |
| `PrivacyChallengeKeyLifetimeHours`    | Shared lifetime of export/withdrawal verification keys, in hours    | `24`    | `1` through `168`, inclusive  |
| `RateLimitWindowSeconds`              | Fixed-window duration for each public privacy request operation    | `60`    | Positive integer              |
| `RequestRateLimitPermitLimit`         | Request challenges allowed per client address per window           | `5`     | Positive integer              |
| `ConfirmationRateLimitPermitLimit`    | Confirmation attempts allowed per client address per window        | `10`    | Positive integer              |
| `CleanupIntervalMinutes`              | Interval for clearing expired challenge data and completed targets  | `15`    | Positive integer              |

Expired, unverified challenges have their recipient and key data cleared by background cleanup;
their rows remain as non-identifying lifecycle metadata. Completed withdrawal target rows are
deleted by cleanup. See [docs/PRIVACY.md](PRIVACY.md) for the export flow, audit lifecycle, and
provider-retention limits.

### RefTestExpirationConfiguration

| Key                              | Description                                           | Default               |
| -------------------------------- | ----------------------------------------------------- | --------------------- |
| `ExpirationCheckIntervalMinutes` | How often to check for expired tests                  | `5`                   |
| `StartupDelaySeconds`            | Delay before the first check                          | `30`                  |
| `ExpirationIfNotStarted`         | Validity period for unused invitations (`d.hh:mm:ss`) | `7.00:00:00` (7 days) |

### BackgroundJobConfiguration

| Key                       | Description                               | Default |
| ------------------------- | ----------------------------------------- | ------- |
| `PollingIntervalSeconds`  | How often to poll for new jobs            | `5`     |
| `LockDurationMinutes`     | How long a job is locked while processing | `5`     |
| `MaxAttempts`             | Maximum retry attempts for failed jobs    | `3`     |
| `BatchSize`               | Maximum jobs processed per cycle          | `10`    |
| `StartupDelaySeconds`     | Delay before job processing starts        | `10`    |
| `EnableCleanup`           | Enable automatic cleanup of old jobs      | `true`  |
| `CleanupIntervalHours`    | How often cleanup runs                    | `24`    |
| `RetainCompletedJobsDays` | Days to keep completed jobs               | `7`     |
| `RetainFailedJobsDays`    | Days to keep failed jobs                  | `30`    |

### AuditLogConfiguration

| Key                    | Description                                    | Default |
| ---------------------- | ---------------------------------------------- | ------- |
| `EnableCleanup`        | Enable automatic redaction and archiving of old audit events | `true`  |
| `CleanupIntervalHours` | How often cleanup runs                         | `24`    |
| `RetentionDays`        | Older events have known personal-data fields and actor name/email redacted, then are archived; remaining accountability details are retained. Accepted range: 1–90 days | `90` |

### GraphQlLimitsConfiguration

Protects `/graphql` from abuse. The participant test-taking flow is reachable without signing
in, so the endpoint cannot rely on authorization alone.

| Key                      | Description                                                    | Default |
| ------------------------ | -------------------------------------------------------------- | ------- |
| `EnforceCostLimits`      | Reject queries whose analysed cost exceeds the limits below    | `true`  |
| `MaxFieldCost`           | Maximum analysed field cost per operation                      | `20000` |
| `MaxTypeCost`            | Maximum analysed type cost per operation                       | `5000`  |
| `EnableRateLimiting`     | Apply the per-address rate limiter to `/graphql`               | `true`  |
| `RateLimitPermitLimit`   | Requests allowed per address within the window                 | `300`   |
| `RateLimitWindowSeconds` | Length of the rate limit window, in seconds                    | `60`    |
| `RateLimitQueueLimit`    | Requests queued once the limit is reached, instead of rejected | `20`    |

#### Tuning the cost limits

The defaults were measured against this schema, not guessed. For reference:

| Operation                                         | Field cost | Type cost |
| ------------------------------------------------- | ---------- | --------- |
| `GetRefTests` (100 per page, filtered and sorted) | 3,667      | 303       |
| `GetAuditLogs` (100 per page)                     | 1,122      | 203       |
| `GetRefTestById` including questions and answers  | 43         | 5         |
| `GetRefTestByToken` (participant)                 | small      | 2         |

Filter and sort arguments dominate field cost, and they are charged from the query document,
so passing filters as variables costs the same as omitting them. If a legitimate query starts
being rejected, the error carries the measured cost (`extensions.fieldCost` /
`extensions.typeCost`) — raise the matching limit to just above it rather than disabling
`EnforceCostLimits`.

#### Rate limiting behind a proxy

The limiter partitions on the resolved client address. `UseForwardedHeaders` processes
`X-Forwarded-For` and `X-Forwarded-Proto` only from the explicitly configured trusted proxy
addresses and networks below. For `X-Forwarded-For`, the resolver uses the resulting
`RemoteIpAddress` at its configured position and never the raw header value. An earlier valid
custom header can take precedence, but a later custom header cannot override a resolved address.
Other headers selected by
`TrustedClientIpHeaders` are considered only when the original transport peer (captured before
forwarded-header middleware runs) matches `KnownProxies` or `KnownNetworks`, and only a single
valid IP address is accepted. The trusted ingress must overwrite each configured custom header
rather than append to or preserve client-supplied values. Missing, malformed, or list-valued custom
headers fall back to the resolved remote address. If the deployment already rate limits at the
edge, set `EnableRateLimiting` to `false`.

### ForwardedHeadersConfiguration

Controls which reverse proxies may supply the client address used by the rate limiter. Forwarded
headers from any other source are ignored.

| Key             | Description                                                  | Default |
| --------------- | ------------------------------------------------------------ | ------- |
| `ForwardLimit`  | Number of trusted proxy hops to process                      | `1`     |
| `TrustedClientIpHeaders` | Ordered client-IP headers; `X-Forwarded-For` uses the resolved remote address, while other raw headers require one valid IP from a known ingress | `[]` |
| `KnownProxies`  | Exact proxy IP addresses allowed to supply forwarded headers | `[]`    |
| `KnownNetworks` | CIDR networks allowed to supply forwarded headers            | `[]`    |
| `AllowUnsafeRateLimitingWithoutTrustedForwarders` | Permit GraphQL rate limiting without known proxies/networks outside development (not recommended); does not trust raw client-IP headers | `false` |

Configure the actual ingress addresses per environment. Leaving both allow-lists empty is the safe
default for direct/local access; it does not trust arbitrary `X-Forwarded-For` headers. When
GraphQL rate limiting is enabled, non-development environments fail fast unless at least one trusted
proxy address/network is configured (`KnownProxies` or `KnownNetworks`) or
`AllowUnsafeRateLimitingWithoutTrustedForwarders` is explicitly set to `true`.

**Compatibility migration:** header-only configurations are rejected at startup, even when GraphQL
rate limiting is disabled. If a platform-specific header such as `X-Azure-ClientIP` is needed,
configure `KnownProxies` or `KnownNetworks` with the actual immediate ingress peer address/CIDR;
do not guess these values. Otherwise remove the raw header configuration and rely on the remote
address after trusted `X-Forwarded-For` processing. The unsafe-rate-limiting override does not make
header-only configuration valid. Verify ingress addresses with the deployment operator before
rollout; this repository does not assume production ingress or CIDRs.

## Managing Migrations

Each provider has its own migrations project. Migrations must be generated separately per provider because the DDL (identity columns, data types, etc.) differs between databases.

### Adding migrations

When setting up a new provider for the first time, generate its initial migration. The EF CLI uses the startup project's DI to resolve the `DbContext`, so you must override both `DatabaseProvider` **and** the connection string to match the target provider — environment variables override user secrets and `appsettings.json`.

**SQL Server** (default — no env vars needed if user secrets already hold a SQL Server connection string):

```bash
dotnet ef migrations add Initial \
  --project RefTestManagement.Migrations.SqlServer \
  --startup-project RefTestManagement.Api
```

**PostgreSQL:**

```bash
DatabaseProvider=PostgreSQL \
ConnectionStrings__RefTestManagement="Host=localhost;Database=RefTestManagement;Username=postgres;Password=postgres" \
dotnet ef migrations add Initial \
  --project RefTestManagement.Migrations.PostgreSQL \
  --startup-project RefTestManagement.Api
```

**SQLite:**

```bash
DatabaseProvider=SQLite \
ConnectionStrings__RefTestManagement="Data Source=RefTestManagement.db" \
dotnet ef migrations add Initial \
  --project RefTestManagement.Migrations.SQLite \
  --startup-project RefTestManagement.Api
```

**MySQL:**

```bash
DatabaseProvider=MySQL \
ConnectionStrings__RefTestManagement="Server=localhost;Database=RefTestManagement;User=root;Password=root;" \
dotnet ef migrations add Initial \
  --project RefTestManagement.Migrations.MySQL \
  --startup-project RefTestManagement.Api
```

> `ConnectionStrings__RefTestManagement` uses double underscores (`__`) because that is how .NET maps environment variables to nested configuration keys (`ConnectionStrings:RefTestManagement`).

### Applying migrations

When the API runs with `Hosting:Mode=All` (the default), it applies pending migrations before
serving requests. Its database identity therefore needs schema-change permissions. For split
deployments, keep the API in `Hosting:Mode=Api` and run the Worker executable's one-shot migration
mode as a separate deployment job before starting or updating the API and Worker apps:

```bash
dotnet RefTestManagement.Worker.dll --migrate
```

The command registers only the database and audit dependencies needed for migrations; it does not
start background pollers or require Redis. Give this migration job database schema-change
permissions separately from the runtime API/Worker identities. `Hosting:Mode=Api` does not apply
migrations automatically.

The Azure App Service release workflow deploys the application but does not run a separate
migration job. With `Hosting:Mode=All`, the API applies migrations at startup; split deployments
must run the Worker command from an authorized deployment/operator environment before updating the
API and Worker. Use the same database provider and connection string that the application will use;
keep the connection string out of command text, logs, and source control.

For local development, run it manually with the matching provider and connection string:

```bash
DatabaseProvider=PostgreSQL \
ConnectionStrings__RefTestManagement="Host=localhost;Database=RefTestManagement;Username=postgres;Password=postgres" \
dotnet ef database update \
  --project RefTestManagement.Migrations.PostgreSQL \
  --startup-project RefTestManagement.Api
```

### Listing applied migrations

```bash
# SQL Server (no env vars needed if user secrets are set)
dotnet ef migrations list \
  --project RefTestManagement.Migrations.SqlServer \
  --startup-project RefTestManagement.Api

# Other providers — add the same env var pair as shown above
```

## Using User Secrets

```bash
cd RefTestManagement.Api

dotnet user-secrets set "ConnectionStrings:RefTestManagement" "Server=localhost;Database=RefTestManagement;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet user-secrets set "Identity:Auth0:Domain" "your-tenant.auth0.com"
dotnet user-secrets set "Identity:Auth0:ClientId" "your-client-id"
dotnet user-secrets set "Identity:Auth0:ClientSecret" "your-client-secret"
dotnet user-secrets set "Identity:Auth0:Audience" "your-api-identifier"
dotnet user-secrets set "Identity:Auth0:ManagementClientId" "your-m2m-client-id"
dotnet user-secrets set "Identity:Auth0:ManagementClientSecret" "your-m2m-client-secret"
dotnet user-secrets set "EmailConfiguration:BrevoApiKey" "your-brevo-api-key"
```

Auth0 settings live under `Identity:Auth0`. Secrets under a top-level `Auth0:*` path are not read.

To use Keycloak, set `Identity:Provider` to `Keycloak` and configure `Identity:Keycloak:*`
(see [KEYCLOAK.md](KEYCLOAK.md) for clients, roles, and the clean-slate switch):

```bash
dotnet user-secrets set "Identity:Provider" "Keycloak"
dotnet user-secrets set "Identity:Keycloak:Authority" "https://login.referees-handballbelgium.be/realms/referees-handballbelgium-beta"
dotnet user-secrets set "Identity:Keycloak:ClientId" "reftest-beta"
dotnet user-secrets set "Identity:Keycloak:ClientSecret" "your-client-secret"
dotnet user-secrets set "Identity:Keycloak:Audience" "reftest-api"
dotnet user-secrets set "Identity:Keycloak:AdminClientId" "reftest-management-admin"
dotnet user-secrets set "Identity:Keycloak:AdminClientSecret" "your-admin-client-secret"
```

Any key from the sections above can be set the same way, using `:` to nest sections and array indices (e.g. `LanguageConfiguration:EnabledLanguages:0`).

## Release Credentials

The release workflows push to protected branches and create GitHub releases with a short-lived
GitHub App installation token. They are **App-only**: each release job fails before checkout if
either required App credential is missing, and there is no personal-access-token fallback.

1. Create a GitHub App in the organisation. Grant it only **Contents: Read and write**, **Issues:
   Read and write**, and **Pull requests: Read and write**; it does not need to be public. The
   workflows request only Contents for branch promotion/synchronization and request the additional
   Issues/Pull requests permissions for semantic-release.
2. Install the app on `handballbelgium-referees/ref-test-management`.
3. Generate a private key for the app and download the `.pem`.
4. Add the app to the branch protection bypass list for `main` and `release`.
5. In the repository settings add:
   - a **secret** `RELEASE_APP_CLIENT_ID` holding the app's numeric Client ID;
   - a **secret** `RELEASE_APP_PRIVATE_KEY` holding the full contents of the `.pem`.

Do not create a `GH_PAT` repository secret or a personal access token for releases. Preserve the App's least-privilege permissions and keep the
workflow action references SHA-pinned.
