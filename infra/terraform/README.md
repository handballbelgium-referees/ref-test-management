# Terraform deployment options

The Azure roots provision Azure infrastructure; the Cloudflare roots provision R2 state and a Worker custom-domain binding. Application hosting remains configured through the provider-neutral `Hosting:Mode` and `State:Mode` settings; there is no Dockerfile. The API and Worker images are built with the .NET SDK `PublishContainer` target and supplied to Azure by immutable ACR digest or to the Cloudflare Worker through Wrangler-managed Container images. See [docs/DEPLOYMENT.md](../../docs/DEPLOYMENT.md) for GitHub environment setup and the end-to-end rollout.

## Topology and cost gates

- `deployment_mode = "single"` is the default in both environments. The API runs `Hosting:Mode=All`, one replica, and local in-process state; no Redis resource is created.
- `deployment_mode = "split"` runs API and Worker separately, uses shared state and Entra-authenticated Azure Managed Redis, and applies the per-identity `2026-05-01-preview` ACL. Split mode fails validation unless `allow_split_mode = true`. Keep it disabled while the combined testing/production target is EUR 50; the current split estimate is already above that target.
- `database_provider` is selected independently for testing and production and defaults to `SqlServer`. `PostgreSQL` and `MySQL` provision managed Flexible Servers and require `allow_managed_database_providers = true`; they use seven-day backups, low-cost burstable compute, and no HA/geo-redundant backup. PostgreSQL starts with 32 GiB and MySQL with 20 GiB; auto-grow is disabled to bound storage spend, so monitor capacity and expand intentionally. The opt-in does not cap spend.
- `SQLite` uses a dedicated ZRS Azure Files share with a 5 GiB quota mounted by the API and migration job. It is allowed only in single mode with one API replica. The share key is present in protected Terraform state, the release workflow pauses the API around migrations, and the share has no automated backup. Monitor the quota and plan provider/file migration before the share fills. SQLite's network-file locking risk remains; do not use it for split/API-scale-out deployments.
- API replicas are fixed to one. The GraphQL limiter is per instance and is not a safe reason to scale the API horizontally.
- The bootstrap creates a subscription budget filtered to the shared bootstrap, testing, and production resource groups only when `subscription_currency = "EUR"`. It alerts owners; Azure budgets do not stop charges or guarantee the cost target.
- Belgium Central is the default. Set `location = "West Europe"` only if a required resource is unavailable in Belgium Central. Keep an environment's resources co-located.

## Layout and state

| Path | Purpose |
| --- | --- |
| `bootstrap/` | Shared Basic ACR, private Azure Blob state container, OIDC role assignments, and the combined budget alert. Bootstrap state is local and contains no app credentials. |
| `environments/testing/` | Testing root with backend key `testing.tfstate`. |
| `environments/production/` | Production root with backend key `production.tfstate`. |
| `modules/aca-platform/` | Reusable Azure platform implementation used by both environment roots. |
| `cloudflare/bootstrap/` | Local-state bootstrap for the R2 bucket used by Cloudflare Terraform state. |
| `cloudflare/environments/testing/` | Testing custom-domain root with a dedicated R2 state key. |
| `cloudflare/environments/production/` | Production custom-domain root with a dedicated R2 state key. |
| `modules/cloudflare-platform/` | Reusable Worker custom-domain binding. Wrangler remains the Worker/container owner. |

The Terraform state account disables Shared Key, requires HTTPS/TLS 1.2, uses a private container, enables blob versioning and 14-day soft-delete retention, and is accessed through Entra Blob Data Contributor. AzAPI exports only storage/workspace identifiers, so the bootstrap/state and Log Analytics shared keys are not retrieved into Terraform state. The SQLite Azure Files mount is an exception: ACA environment storage requires the dedicated account's access key, which is retained in that environment's protected Terraform state. Never reuse this SQLite account for Terraform state or other application data. The bootstrap operator and optional GitHub OIDC principal receive Blob Data Contributor at the storage-account scope. The ACR admin account is disabled; Basic ACR does not support anonymous pull. Image publishing uses a separately scoped `AcrPush` assignment.

Do not commit `terraform.tfvars`, backend files, state, plans, or `.terraform/`. Commit each generated `.terraform.lock.hcl` after the provider versions have been initialized. The environment backends use Entra auth; for GitHub Actions, enable `use_oidc` only after configuring that environment's federated credential and backend role.

## Bootstrap

1. Copy `bootstrap/terraform.tfvars.example` to `bootstrap/terraform.tfvars`. Replace the subscription, globally unique storage/ACR names, resource-group names, and verified billing currency. Set `terraform_runner_object_id` and `acr_publisher_object_id` to the GitHub OIDC service-principal **object ID** when the identities are ready.
2. Authenticate to the intended subscription. The bootstrap caller needs permission to create the resources and role assignments.
3. Initialize, validate, and apply the bootstrap root. This creates no application environment:

   ```powershell
   terraform -chdir=infra/terraform/bootstrap init -backend=false
   terraform -chdir=infra/terraform/bootstrap validate
   terraform -chdir=infra/terraform/bootstrap apply -var-file=terraform.tfvars
   ```

4. Copy each environment's `backend.hcl.example` to an untracked `backend.hcl`, fill in the bootstrap storage values, and initialize that environment with the matching backend configuration.
5. Push already-built API and Worker OCI images to the shared ACR before setting `create_applications = true`. Use the `acr_login_server` bootstrap output and deploy by image digest, never a mutable tag. For an initial seed from Podman, use the .NET SDK publish archives, load/tag/push them with Podman, and retrieve the digest; see [docs/DEPLOYMENT.md](../../docs/DEPLOYMENT.md#initial-azure-image-seed).

The state container is private and Shared Key is disabled. The GitHub OIDC Terraform principal needs `Storage Blob Data Contributor` on the state storage account and `AcrPush` on the ACR. The release principal needs only the Container Apps image-update and migration-job permissions; SQLite releases also need `Microsoft.App/containerapps/stop/action` and `Microsoft.App/containerapps/start/action` on the API app. The bootstrap and first environment apply are operator-run because these roots create resource groups and role assignments. After the groups exist, grant the OIDC principal `Contributor` on each target resource group and `Role Based Access Control Administrator` with a condition limiting assignable roles/principals at the role-assignment scopes (or an equivalent custom role). If CI itself must create resource groups, separately grant only `Microsoft.Resources/subscriptions/resourceGroups/write` at subscription scope. Managing the subscription budget additionally requires `Cost Management Contributor` at subscription scope. Do not grant subscription-wide `Owner`; bootstrap does not grant the runner its own ARM permissions.

## Environment rollout

Environment roots default to `create_applications = false` so Key Vault, the selected database resources, UAMIs, ACA environment, and foundational RBAC can exist before apps refer to externally seeded Key Vault secrets. Key Vault secret-reader assignments are created at individual secret scopes only when applications are enabled, after those secret values exist. With direct Terraform, changing `create_applications` back to false destroys the Container Apps and migration job. The GitHub `infra-deploy.yml` workflow has an additional state check: after the API app exists in state, an unchecked `create_applications` input is overridden to true so a routine infrastructure run cannot delete the apps accidentally. The workflow does not expose app deletion.

1. Copy the environment's `terraform.tfvars.example` to an untracked `terraform.tfvars`. Set actual non-secret IDs/URLs, unique names and suffix, `database_provider`, and a valid pushed image digest. Set the matching `AZURE_DATABASE_PROVIDER` GitHub Environment variable for releases and infrastructure workflows; it defaults to `SqlServer` when unset. PostgreSQL/MySQL additionally require `allow_managed_database_providers = true` and the protected `AZURE_DATABASE_ADMIN_PASSWORD` GitHub Environment secret. Update `AZURE_DATABASE_ADMIN_PASSWORD_VERSION` when rotating the write-only server administrator password. Before enabling applications, set `api_trusted_proxy_networks` to the verified CIDRs of the actual ACA ingress peers. Do not use broad Azure service ranges, `0.0.0.0/0`, `::/0`, or `AllowUnsafeRateLimitingWithoutTrustedForwarders`; if the peer CIDRs cannot be verified, leave `create_applications = false` and stop before public app creation. Do not put Auth0, Brevo, SQL, database, or HMAC secret values in Terraform variables files.
2. Initialize/validate the environment without reaching Azure state if desired, then configure the remote backend for normal Terraform operations:

   ```powershell
   terraform -chdir=infra/terraform/environments/testing init -backend=false
   terraform -chdir=infra/terraform/environments/testing validate
   terraform -chdir=infra/terraform/environments/testing init -backend-config=backend.hcl
   ```

   Replace `testing` with `production` for the production root. The two roots use separate state keys and databases.
3. For `SqlServer`, set `sql_firewall_ip_addresses` to the operator's exact public IPv4 address and apply with `create_applications = false`. This allows the interactive Entra SQL administrator to perform the contained-user bootstrap without opening SQL to all Azure services. For PostgreSQL/MySQL, the same allowlist input controls exact server firewall addresses; SQLite does not use a database firewall.
4. For `SqlServer`, read the SQL FQDN/database and the API, Worker, and migration identity names, client IDs, and object IDs using `terraform output`. Run `bootstrap/sql-identities.sql` in the corresponding database with an Entra-authenticated `sqlcmd -G` session. The script uses the first five object-ID characters as a stable suffix for each database user:

   ```powershell
   sqlcmd `
     -S "tcp:<sql-server-fqdn>,1433" -d "<database-name>" -G -b `
     -i "infra/terraform/bootstrap/sql-identities.sql" `
     -v "ApiIdentityName=<api-identity-name>" "ApiObjectId=<api-object-id>" "ApiObjectIdSuffix=<first-five-object-id-characters>" `
        "WorkerIdentityName=<worker-identity-name>" "WorkerObjectId=<worker-object-id>" "WorkerObjectIdSuffix=<first-five-object-id-characters>" `
        "MigrationIdentityName=<migration-identity-name>" "MigrationObjectId=<migration-object-id>" "MigrationObjectIdSuffix=<first-five-object-id-characters>"
   ```

   It grants `db_datareader` and `db_datawriter` to the API and Worker identities; only the one-shot migration identity receives `db_owner`.

   The operator must be the configured Entra SQL administrator (or a member of its group) and have the directory-read permissions required by Azure SQL to resolve service principals for `CREATE USER FROM EXTERNAL PROVIDER`. This tenant-level prerequisite is intentionally not assigned by Terraform.
   For PostgreSQL/MySQL, Terraform creates the managed server and database but does not pass the administrator credential to the API. Supply `AZURE_DATABASE_ADMIN_PASSWORD` as a protected GitHub Environment secret for plan/apply; Terraform consumes it through an ephemeral variable and the server's write-only password field. Increment `AZURE_DATABASE_ADMIN_PASSWORD_VERSION` when rotating that password. Use the administrator account out of band to create separate least-privilege API, Worker, and migration users. For SQLite, no database users or connection secrets are needed.
   For the managed providers, read `database_server_fqdn` and `database_name` from Terraform outputs when creating the Key Vault connection strings. For direct Terraform runs, pass the admin password through `TF_VAR_database_admin_password_wo` in a protected shell; never put it in `terraform.tfvars`.
5. Create the application Key Vault secret values out-of-band using the Azure portal or an approved secret-management process. Terraform stores only versionless Key Vault secret references. Use the `sql-*` database secrets only for `SqlServer`, the `database-*` connection secrets only for PostgreSQL/MySQL, and no database connection secret for SQLite. Required names:

   | Secret | Used by |
   | --- | --- |
   | `sql-api-connection` | API |
   | `sql-worker-connection` | Worker |
   | `sql-migration-connection` | Migration job |
   | `database-api-connection` | API when PostgreSQL or MySQL is selected |
   | `database-worker-connection` | Worker when PostgreSQL or MySQL is selected in split mode |
   | `database-migration-connection` | Migration job when PostgreSQL or MySQL is selected |
   | `auth0-client-secret` | API |
   | `auth0-management-client-secret` | API in single and split modes for on-demand authorization permission refresh; Worker in split mode |
   | `brevo-api-key` | API and Worker |
   | `privacy-challenge-hmac` | API and Worker only in split mode; use at least 32 UTF-8 bytes |

   The `sql-*` secrets are connection strings without passwords:
   `Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;Encrypt=True;TrustServerCertificate=False;Authentication=Active Directory Managed Identity;User Id=<role-identity-client-id>;Connection Timeout=30;`.
   Use the matching API, Worker, or migration client ID. The `database-*` secrets for PostgreSQL/MySQL contain the selected server/database and separate user credentials with TLS server-certificate validation. Never expose these values in Terraform variables, outputs, command text, or logs.

   The split API retains the Auth0 Management credential because `SecurityStartup` refreshes effective user permissions through `PermissionSnapshotService`, which calls the Auth0 Management API on a cache miss. Its managed identity receives secret-read access only to the required `auth0-management-client-secret` (and the API's other explicitly referenced secrets), not to the whole vault. Azure generally recommends vault-scoped roles within an application/environment; these separate API, Worker, and migration identities use the documented limited exception for secret-scoped roles because each needs a different fixed set of secrets.
6. After seeding every applicable Key Vault secret, set `create_applications = true` and apply. This creates secret-scoped reader assignments and then the app resources; it does not grant any identity vault-wide secret access. The API's trusted proxy list must be populated with verified ACA ingress peer CIDRs. For initial app creation, the selected server database firewall can still contain only the operator address; the apps may not reach it yet. Read `terraform output -json container_app_egress_ip_addresses`, add those exact addresses to `sql_firewall_ip_addresses`, retain the operator address while needed, and apply again. No `0.0.0.0` Azure-services rule is created. For SQLite, this step does not configure a database firewall.
7. If using Key Vault firewall rules, first create the ACA resources with `key_vault_firewall_enabled = false`. Then add both the current ACA egress addresses and the Terraform runner/operator egress addresses to `key_vault_allowed_ip_addresses`, set `key_vault_firewall_enabled = true`, and apply. Keep the rules current: consumption-profile egress can change. Public network access stays enabled for this public-endpoint design.
8. After the selected server database firewall allows ACA egress, the release workflow runs the manual migration job using the Worker image and migration UAMI before deploying application revisions. For SQLite, the release workflow stops the single-mode API, runs the migration job against the same Azure Files share, updates the API revision, and starts the API in its cleanup path. This causes a maintenance outage; SQLite remains single-mode-only. Terraform does not run migrations.

Key Vault uses RBAC, TLS, purge protection, and a 90-day soft-delete window. Its public endpoint has no IP allowlist by default because both consumption ACA egress and hosted OIDC runner egress are dynamic; set the explicit Key Vault firewall inputs after those addresses are available if the environment supports that operationally. If stable private-only access is required, stop and revise the network/cost design rather than silently introducing billable private endpoints or a NAT gateway.

## Secrets, identities, and releases

- Container Apps logs use the `azure-monitor` destination and an
  `azurerm_monitor_diagnostic_setting` that routes `ContainerAppConsoleLogs` and
  `ContainerAppSystemLogs` to the environment workspace using resource-specific tables. The
  workspace keeps `disableLocalAuth = true`; the application workspace shared key is not fetched or
  stored in Terraform state. The subscription must have `Microsoft.Insights` registered, and the
  Terraform principal needs permission to configure diagnostic settings on the Container Apps
  environment. See [Container Apps log options](https://learn.microsoft.com/azure/container-apps/log-options).
- API, Worker, and migration job each use a separate user-assigned managed identity in each environment for ACR/Key Vault access. With `SqlServer`, those identities also authenticate to Entra-only SQL and the contained users are provisioned by the operator script. PostgreSQL/MySQL use distinct database credentials in Key Vault rather than sharing the managed database administrator password.
- API and Worker can read only their explicitly referenced Key Vault secrets and use the Key Vault-wrapped database Data Protection key. The migration identity can read only its database connection string and has no key-cryptography role.
- In split mode, API and Worker use their own managed identities for Redis token authentication. The preview ACL allows `+@read +@write +@pubsub +@scripting +@transaction +@connection +cluster|slots +cluster|nodes ~*` for each identity and retains all keys within that environment. If the preview API is unavailable, the apply must fail; there is no full-access or access-key fallback.
- Terraform creates the initial app/job configuration from digest references. `ignore_changes` on the image fields lets the release workflow update those images by digest without Terraform reverting a later release. Terraform continues to own the app settings, identity, ingress, and scale limits.
- The API has HTTPS-only external ingress on port 8080. The Worker app has no ingress. The migration job is manual-only. Each environment has its own selected database, Key Vault, Data Protection key, Log Analytics workspace, and optional Redis backplane. SQL Server remains Entra-only; PostgreSQL/MySQL use managed-service credentials; SQLite uses the mounted file.
- ACR uses the Basic public endpoint because Premium networking controls exceed the approved cost profile. Registry and Key Vault access require Entra RBAC; server-database public endpoints use exact IP firewall rules. Changing `database_provider` after provisioning is blocked by database lifecycle protection; migrate data separately before changing providers.

## Offline validation

The pinned Terraform container can run formatting and backend-disabled validation without Azure credentials or Azure state access. From the repository root, with Podman installed:

```powershell
$workspace = (Get-Location).Path
$image = "docker.io/hashicorp/terraform:1.16.5"

podman run --rm --volume "${workspace}:/workspace" --workdir /workspace $image fmt -check -recursive infra/terraform
podman run --rm --volume "${workspace}:/workspace" --workdir /workspace $image -chdir=infra/terraform/bootstrap init -backend=false
podman run --rm --volume "${workspace}:/workspace" --workdir /workspace $image -chdir=infra/terraform/bootstrap validate
podman run --rm --volume "${workspace}:/workspace" --workdir /workspace $image -chdir=infra/terraform/environments/testing init -backend=false
podman run --rm --volume "${workspace}:/workspace" --workdir /workspace $image -chdir=infra/terraform/environments/testing validate
podman run --rm --volume "${workspace}:/workspace" --workdir /workspace $image -chdir=infra/terraform/environments/production init -backend=false
podman run --rm --volume "${workspace}:/workspace" --workdir /workspace $image -chdir=infra/terraform/environments/production validate
podman run --rm --volume "${workspace}:/workspace" --workdir /workspace $image -chdir=infra/terraform/cloudflare/bootstrap init -backend=false
podman run --rm --volume "${workspace}:/workspace" --workdir /workspace $image -chdir=infra/terraform/cloudflare/bootstrap validate
podman run --rm --volume "${workspace}:/workspace" --workdir /workspace $image -chdir=infra/terraform/cloudflare/environments/testing init -backend=false
podman run --rm --volume "${workspace}:/workspace" --workdir /workspace $image -chdir=infra/terraform/cloudflare/environments/testing validate
podman run --rm --volume "${workspace}:/workspace" --workdir /workspace $image -chdir=infra/terraform/cloudflare/environments/production init -backend=false
podman run --rm --volume "${workspace}:/workspace" --workdir /workspace $image -chdir=infra/terraform/cloudflare/environments/production validate
```

An authenticated local run is also possible with Podman: mount the repository, pass AzureRM authentication through the host environment, and use the environment's untracked backend/tfvars files. The official HashiCorp Terraform image does **not** include Azure CLI, so `use_cli` auth won't work in it unless you use a separately prepared CLI-enabled image. For an OIDC or service-principal setup, pass only the required `ARM_*` environment variables with `--env`; do not put credentials in `terraform.tfvars`, command text, or this repository. Example for a testing plan:

```powershell
$workspace = (Get-Location).Path
$image = "docker.io/hashicorp/terraform:1.16.5"

podman run --rm --volume "${workspace}:/workspace" --workdir /workspace `
  --env ARM_CLIENT_ID --env ARM_TENANT_ID --env ARM_SUBSCRIPTION_ID `
  --env ARM_CLIENT_SECRET `
  $image -chdir=infra/terraform/environments/testing init -backend-config=backend.hcl

podman run --rm --volume "${workspace}:/workspace" --workdir /workspace `
  --env ARM_CLIENT_ID --env ARM_TENANT_ID --env ARM_SUBSCRIPTION_ID `
  --env ARM_CLIENT_SECRET `
  $image -chdir=infra/terraform/environments/testing plan `
  -var-file=terraform.tfvars -out=testing.tfplan
```

For OIDC, provide `ARM_USE_OIDC` and the host-issued `ARM_OIDC_TOKEN`, and set `use_oidc = true` in the untracked backend file. Do not run `apply` until the target subscription, policy, quota, cost, backend, identities, resource names, and changes in the plan have been explicitly authorized and checked.

Use Terraform `1.16.5`, AzureRM `~> 5.9.0`, and AzAPI `~> 2.13.0`. Validation does not establish live region support, quota, policy compatibility, identity permissions, or the current SQL/Key Vault egress allowlist.

## Cloudflare Containers and custom domain

Terraform owns only the R2 state bucket and the Worker custom-domain binding. Wrangler owns Worker code, Durable Object bindings, Cron, container image rollout, and runtime secrets. The domain must already be an active Cloudflare zone, and the Worker service must be deployed before Terraform attaches the hostname. Cloudflare creates the DNS record and TLS certificate for that custom domain; no registrar, transfer, or nameserver changes are performed.

The API hostname is an input in each environment's `terraform.tfvars` and must match `API_HOSTNAME` in the Wrangler deployment. The Worker rejects requests for another host. Use distinct Worker service names for testing and production. The provider's optional custom-domain `environment` field is deprecated, so `worker_environment` is left `null` in the examples.

### Bootstrap and state

The bootstrap root uses local state and creates only a Standard R2 bucket protected by `prevent_destroy`. Do not lose or discard the local bootstrap state after creation. The R2 bucket is not self-hosted: the operator must create Cloudflare credentials out of band and keep them outside this repository.

1. Copy `cloudflare/bootstrap/terraform.tfvars.example` to `terraform.tfvars`, then set the Cloudflare account ID and a globally unique state bucket name.
2. Set `CLOUDFLARE_API_TOKEN` in the shell. The bootstrap token needs the Cloudflare **Workers R2 Storage Write** permission; the environment roots need **Workers Scripts Read** and **Workers Scripts Write**. A shared token must include both resource scopes. Initialize and validate offline first; applying the bootstrap is a live Cloudflare change and must be separately authorized.
3. After the bucket exists, create a separate R2 S3-compatible API token with **Object Read & Write** access scoped to that state bucket. Export its Access Key ID as `AWS_ACCESS_KEY_ID` and Secret Access Key as `AWS_SECRET_ACCESS_KEY`; use `AWS_REGION=auto` if the shell or backend requires it. These credentials are distinct from `CLOUDFLARE_API_TOKEN`; see [R2 token authentication](https://developers.cloudflare.com/r2/api/tokens/).
4. Copy each environment's `backend.hcl.example` to an untracked `backend.hcl`. Replace the bucket and account endpoint placeholders with bootstrap output and the account ID. The examples use the S3-compatible R2 endpoint, path-style requests, and R2 object conditional writes for the Terraform lock file.
5. Copy the environment's `terraform.tfvars.example` to an untracked `terraform.tfvars`. Set its active zone ID, API hostname, and deployed Wrangler service name. Initialize with `terraform init -backend-config=backend.hcl -migrate-state` to move local state to R2. Testing and production use separate state keys.

Do not commit actual `terraform.tfvars`, `backend.hcl`, state, plans, credentials, or runtime secrets. Terraform API credentials are supplied through `CLOUDFLARE_API_TOKEN`; backend credentials are supplied only through `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY`. Do not put either credential pair in Terraform variables or files.

### Wrangler deployment order

1. Build the API and Worker OCI images with the .NET SDK `PublishContainer` target, then push the images to the Cloudflare Container registry with Wrangler. The `durable_object` scheduling policy requires digest-pinned Cloudflare registry references; replace the deliberate zero-digest placeholders in `infra/cloudflare/worker/wrangler.jsonc` with the immutable digests Wrangler returns. No Dockerfile is used.
2. Configure the Wrangler `API_HOSTNAME`, deployment mode, non-secret Auth0/email settings, and Worker Secrets for that environment. Deploy testing and production as separate Worker services so each has its own variables and secrets; the Terraform `worker_service_name` must match the service deployed by Wrangler. For example, deploy an environment with `npx wrangler deploy --name ref-test-management-testing --var APP_MODE:Split --var API_HOSTNAME:testing.example.com`. Use the appropriate `APP_MODE` and hostname for each environment. Deploy the Worker and Container images with Wrangler; the Worker config disables `workers.dev`.
3. Set the matching `api_hostname` and `worker_service_name` in the corresponding Terraform environment and apply the custom-domain binding only after the Worker exists. Cloudflare then creates the DNS record and issues its TLS certificate.

Cloudflare Containers require the Workers Paid plan, currently a $5 USD/month account minimum. A `standard-1` container provisions 4 GiB memory and 8 GB disk; if a single background container remains active for 30 days due to the one-minute Cron wake and two-minute inactivity timeout, the 2,880 GiB-hours of memory exceed the plan's 25 GiB-hour monthly container allowance by 2,855 GiB-hours (about $25.70 at the published memory rate), before disk, active CPU, Worker/Durable Object, request, logging, or egress charges. One `standard-1` disk active for that period also exceeds the included disk allowance and costs about $1.40 at the published rate. Container egress in Europe has 1 TB/month included, then costs $0.025/GB. These are usage illustrations, not a complete monthly estimate; allowances are account-level and the API container's active time depends on traffic.

R2 Standard includes 10 GB-month of storage, 1 million Class A operations, and 10 million Class B operations monthly; above those allowances, current rates are $0.015/GB-month, $4.50/million Class A operations, and $0.36/million Class B operations. R2 egress is free. External PostgreSQL and Redis-compatible service costs remain unknown until vendors and usage are selected. The Azure EUR 50 target does not apply to Cloudflare and is not claimed to be met.

For the rates and account limits, see [Workers pricing](https://developers.cloudflare.com/workers/platform/pricing/), [Containers pricing](https://developers.cloudflare.com/containers/platform/pricing/), and [R2 pricing](https://developers.cloudflare.com/r2/pricing/). Cloudflare documents Durable Object Container scheduling as public beta and requires external services to accept Container egress. Confirm account feature availability, database/cache TLS connectivity, the Worker hostname, and runtime secret/certificate provisioning before production. No Cloudflare plan, apply, Wrangler deployment, or live resource change has been run as part of this implementation.
