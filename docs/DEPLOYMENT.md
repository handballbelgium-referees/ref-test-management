# Deployment guide

Azure App Service is the supported hosted release target. `Hosting:Mode`, `Hosting:Provider`,
`State:Mode`, and `DatabaseProvider` are independent choices; see the
[configuration reference](CONFIGURATION.md). Azure infrastructure is managed outside this
repository.

| Target | Release path | Infrastructure/code ownership |
| --- | --- | --- |
| Local | Podman Compose for development | Local-only services and data; no cloud resources |
| Azure App Service | Beta/stable GitHub Actions workflows | GitHub Actions deploys to the configured Web App; Azure infrastructure is managed outside this repository |

Configure the App Service names through the existing `AZURE_WEBAPP_TESTING_NAME` and
`AZURE_WEBAPP_PRODUCTION_NAME` secrets. No Azure infrastructure or deployment resources are
provisioned by this repository.

## Local containers

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

This reproduces split application behavior and shared TLS state, not a cloud provider's managed
identity, Key Vault, database authentication, ingress, scaling, or rollout. The
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

Never put credentials, plans, state, database passwords, or certificate bytes in the repository.

## Deployment gates

- Azure App Service: configured Web App names, runtime settings, database connectivity, and
  managed-identity access to required Azure services.
