# Deployment guide

Azure App Service is the supported hosted release target. `Hosting:Mode`, `Hosting:Provider`,
`State:Mode`, and `DatabaseProvider` are independent choices; see the
[configuration reference](CONFIGURATION.md). Azure infrastructure is managed outside this
repository.

| Target            | Release path                         | Infrastructure/code ownership                                                                             |
| ----------------- | ------------------------------------ | --------------------------------------------------------------------------------------------------------- |
| Local             | Podman Compose for development       | Local-only services and data; no cloud resources                                                          |
| Azure App Service | Beta/stable GitHub Actions workflows | GitHub Actions deploys to the configured Web App; Azure infrastructure is managed outside this repository |

Configure the App Service names through the existing `AZURE_WEBAPP_TESTING_NAME` and
`AZURE_WEBAPP_PRODUCTION_NAME` secrets. No Azure infrastructure or deployment resources are
provisioned by this repository.

## Local containers

### Isolated Podman Compose stack

Run the isolated local project from Git Bash with `uv`, Podman, .NET,
Node/npm, and OpenSSL available on `PATH`:

```bash
uv run --script infra/local/start.py
```

Before preparing the stack, the launcher asks for a hosting mode and database provider. Choose
**Combined** to run the API and background processing in one API container, or **Split** to run
separate API and Worker containers. These set `Hosting:Mode=All` or `Hosting:Mode=Api` on the API;
the Worker runs with `Hosting:Mode=Worker` in Split mode. The database choices are SQL Server,
PostgreSQL, SQLite, and MySQL.

The uv script metadata supplies the pinned InquirerPy terminal prompts.
The launcher creates a separate rootless Podman WSL machine named `rtm-local` (4 CPUs,
6 GiB memory, user-mode networking) if it does not exist, builds the Angular assets, publishes the
API and Worker OCI images with the .NET SDK, and starts `compose.local.yaml` as project `rtm-local`.
It uses a dedicated DNS-disabled bridge with static SQL/Redis addresses and
explicit host mappings, avoiding the user-systemd-bus requirement of rootless bridge DNS on this WSL
machine. It does not change the default Podman connection or touch containers, networks, or volumes
in other machines. The separate machine avoids changing/restarting an existing 2-GiB Podman machine.
If you have a previous local stack, run `podman machine stop rtm-azure-local` before starting this
renamed stack so the published ports are released. That old machine, its database volumes, and its
ignored settings are left untouched and are not migrated.

Use Up/Down and Enter to select the hosting mode and database provider; Ctrl+C cancels either prompt.
The launcher does not save selections between runs. The selected server-backed provider starts only
its matching database service. SQL Server remains bound to
`127.0.0.1:11433`, PostgreSQL to `127.0.0.1:15432`, and MySQL to `127.0.0.1:13306`. SQLite starts
no database server and stores its
database file in a persistent named volume shared by the migration and selected application
containers.
Each provider uses a separate persistent data volume; switching providers does not migrate or share
database contents. The PostgreSQL service stores data in the `postgresql-data-18` volume mounted at
`/var/lib/postgresql`, as required by PostgreSQL 18+ images.

The compose stack also runs authenticated TLS Redis, a one-shot Worker migration, the selected
application topology, and a Caddy TLS edge. Application processes use `Hosting:Provider=Local` and
`State:Mode=Shared`; local configuration comes from the image settings, API User Secrets, and Compose
environment overrides. The Worker sets `DOTNET_ENVIRONMENT=Development` because it uses the Generic
Host. The API is at `https://localhost:7039`; Caddy terminates TLS with the trusted .NET localhost
development certificate and proxies to the API over the isolated Compose network. No HTTP API port
is published to the host; split API/Worker internal traffic remains HTTP. The selected provider's
named data volume persists after `down`.

After startup, the launcher watches application and UI source files and automatically rebuilds the
container images when they change. Changes are debounced briefly; UI changes also rebuild and copy
the Angular assets. The launcher reruns database migrations and recreates only the selected API
and/or Worker containers; the database, Redis, and HTTPS proxy remain running. Build errors are
reported in the launcher. Generated outputs, dependency folders, and build directories are excluded
from watching. This is rebuild-and-restart, not .NET Hot Reload; the browser may need a manual
refresh after a UI update. Press Ctrl+C to stop watching and remove the project's containers.
Persistent database volumes, the isolated Podman machine, and its external network remain.

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
Optional `Identity__Auth0__<Key>` environment variables are passed through by Compose and override User
Secrets. Compose keeps the HTTPS email base URL at `https://localhost:7039` and sets
`EmailConfiguration__LogoBaseUrl` to the API's internal `http://10.250.241.12:8080` address for
logo downloads only. The selected local database provider and connection string, plus these email
URLs, override any matching User Secrets values. The launcher generates a provider-neutral local
database password; existing ignored `.env` files with `MSSQL_SA_PASSWORD` remain supported. Generated
local credentials, the copied User Secrets file, and TLS certificates are ignored.

The active provider is selected by `Identity:Provider` (`Auth0` or `Keycloak`). With `Auth0`, the
Management M2M credentials allow permission refresh and approver lookup. The Worker also runs
additive permission sync at startup: it may add missing application permissions to the resource
server selected by `Identity:Auth0:Domain` and `Identity:Auth0:Audience`. Keep those settings
pointed at the intended Auth0 tenant/API; the required Management API scopes are listed in
[CONFIGURATION.md](CONFIGURATION.md#management-api-access-and-permission-freshness). With `Keycloak`,
the API and Worker use the `Identity:Keycloak:*` service account to read user roles and add missing
permission roles to the `reftest-api` client (see [KEYCLOAK.md](KEYCLOAK.md)). The sync does
not assign user roles or modify user metadata. If the M2M client lacks the required read scopes,
login permission refresh fails closed. If resource-server read or update scopes are missing,
startup sync logs a sanitized failure and the app continues without syncing. Before building the
stack, the launcher requires non-empty values for the keys of the selected provider, from the API
project's User Secrets or environment overrides. With `Auth0` (the default when `Identity:Provider` is unset)
these are `Identity:Auth0:Domain`, `ClientId`, `ClientSecret`, `Audience`, `ManagementClientId`, and
`ManagementClientSecret`. With `Keycloak` these are `Identity:Keycloak:Authority`, `ClientId`,
`ClientSecret`, `Audience`, `AdminClientId`, and `AdminClientSecret`. Its error names missing keys without
showing values.

Because the full API User Secrets store is loaded, the Worker may send pending email jobs through
the configured Brevo account to the configured recipients, and the application may call the
configured IHF endpoint. These local external calls are enabled intentionally; keep the User
Secrets store pointed at the intended services and test recipients.

This reproduces the selected local application topology and shared TLS state, not a cloud provider's
managed identity, Key Vault, database authentication, ingress, scaling, or rollout. The server-backed
providers use local-only database credentials, and SQLite uses a local file; never reuse this
configuration or its generated secrets outside local development.

To inspect or stop only this project:

```bash
podman --connection rtm-local compose --project-name rtm-local \
  --file compose.local.yaml --env-file infra/local/.env \
  --profile SqlServer --profile PostgreSQL --profile SQLite --profile MySQL ps
podman --connection rtm-local compose --project-name rtm-local \
  --file compose.local.yaml --env-file infra/local/.env \
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
