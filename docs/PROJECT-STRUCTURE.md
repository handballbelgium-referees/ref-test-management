# 📁 Project Structure

A high-level map of the repo — expand a project to see its top-level folders. For the exact, current layout, use your editor's file explorer or GitHub's file browser; this doc only tracks stable, top-level organization so it doesn't drift as components are added.

- **`.github/workflows/`** — CI/CD pipelines (PR checks and releases)
- **`.husky/`** — Git hooks (commit-msg, pre-commit)
- **`badges/`** — Auto-generated release badge images
- **`docs/`** — Architecture, security, privacy, configuration docs
- **`infra/`** — Local Podman Compose development assets
- **`scripts/`** — Repo automation (badges, README version sync)

<details open>
<summary><strong><code>RefTestManagement.Api/</code></strong> — 🔷 .NET Web API (.NET 10) — GraphQL endpoint and composition root</summary>

- `Controllers/` — Account endpoints (login, logout, current user, permissions, authentication status); provider-neutral, and the login challenge is chosen by `Identity:Provider`
- `Graphql/` — Mutations, Queries, Subscriptions, Types, ReadModels
- `Identity/` — Identity provider selection (`IdentityProvider`) and OIDC/JWT/cookie wiring (`SecurityStartup`)
- `Services/` — Host-side services, including the Redis-backed privacy rate limiter and participant session lease
- Hosted services (job worker, expiration, privacy retention, audit cleanup, permission sync) live in `RefTestManagement.Hosting/BackgroundServices/`, not in this project

</details>

- **`RefTestManagement.Worker/`** — 🔷 .NET Worker entry point for split background processing and one-shot migrations
- **`RefTestManagement.Hosting/`** — Shared process composition and provider-adapter contract, hosting modes, database and state registration
- **`RefTestManagement.Hosting.Azure/`** — Azure Key Vault Data Protection and managed-identity Redis adapters
- **`RefTestManagement.AuditLog/`** — 📋 Domain-event–driven audit log library (Marten-style event store)
- **`RefTestManagement.Auth0/`** — 🔐 Auth0 Management API client (M2M token, role-based user discovery); used when `Identity:Provider` is `Auth0`
- **`RefTestManagement.Keycloak/`** — 🔐 Keycloak Admin REST client and permission sync (realm roles and client roles on `reftest-api`); used when `Identity:Provider` is `Keycloak`
- **`RefTestManagement.Application/`** — 🔷 Ports (`Abstractions/`: email, PDF, report, translation, session, subscription, privacy-erasure, IHF question-bank, job-queue and export-request cleanup services), job payloads, configuration models and use cases
- **`RefTestManagement.Domain/`** — 🔷 Domain entities: RefTest, RefTestTitle, Job, and their domain events
- **`RefTestManagement.Security/`** — 🔐 Provider-neutral permission model: `Authorization/` (permission constants, handlers, dynamic policy provider), `Identity/` (identity provider name and permission-sync contract), `Extensions/` (DI registration)
- **`RefTestManagement.Infrastructure/`** — 🔷 EF Core DbContext, job-queue persistence/claims/retention, export-request cleanup, PDF/Excel/email service implementations and subscriptions; `AddInfrastructureServices()` registers the adapters
- **`RefTestManagement.Migrations.SqlServer/`, `.PostgreSQL/`, `.SQLite/`, `.MySQL/`** — 🗄️ Provider-specific EF Core migrations
- **`RefTestManagement.UnitTests/`** — 🧪 xUnit tests, including the architecture dependency tests

<details open>
<summary><strong><code>RefTestManagement.Ui/</code></strong> — 🅰️ Angular frontend</summary>

- `src/app/`
  - `auth/` — Guards, permission directive, PermissionsService
  - `home/` — Landing page
  - `audit-logs/` — Audit log viewer
  - `ref-tests/` — RefTest creation, detail view, and list management
  - `ref-test/` — RefTest-taking experience (welcome, take, results)
  - `privacy/` — GDPR privacy notice and consent UI
  - `shared/` — Shared components, pipes, utilities
- `graphql/` — GraphQL operations (queries/mutations/subscriptions) by feature
- `public/` — Static assets, i18n translation files, PWA manifest

</details>

- **`package.json`** — Root dependencies (semantic-release, husky)
- **`README.md`**

## Project dependencies

Arrows are `ProjectReference`s. The rule and its known violations are recorded in [ADR 0001](adr/0001-layered-architecture.md) and enforced by `RefTestManagement.UnitTests/ArchitectureDependencyTests.cs`. Shared state across replicas is described in [ADR 0002](adr/0002-multi-replica-state.md); combined and split container hosting is described in [ADR 0003](adr/0003-container-hosting.md).

```mermaid
flowchart TB
    Api["Api (composition root)"] --> Infrastructure
    Api --> Hosting["Hosting (shared composition)"]
    Worker["Worker (background entry point)"] --> Hosting
    Api --> AzureHosting["Hosting.Azure"]
    Worker --> AzureHosting
    AzureHosting --> Hosting
    Hosting --> Migrations["Migrations.* (4 providers)"]
    Hosting --> Infrastructure
    Hosting --> Application
    Hosting --> AuditLog
    Hosting --> Auth0
    Hosting --> Keycloak
    Hosting --> Security
    Api --> Security
    Api --> Auth0
    Api --> Keycloak
    Api --> AuditLog
    Migrations --> Infrastructure
    Infrastructure --> Application
    Infrastructure --> AuditLog
    Application --> Domain
    AuditLog --> Domain
    Auth0 --> Security
    Keycloak --> Security
```

## Key Directories

| Directory                                                        | Purpose                                                                       |
| ---------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| `RefTestManagement.AuditLog`                                     | Audit log library — interceptor, entity, options, DI extension. Depends on `Domain` for the event abstractions, never the reverse |
| `RefTestManagement.Api/Graphql`                                  | GraphQL schema, queries, mutations, and type definitions                      |
| `RefTestManagement.Api/Graphql/Mutations/Approval`               | Approve/reject mutations (requires `ref-tests:approve`)                       |
| `RefTestManagement.Worker`                                        | Background-service process entry point and `--migrate` one-shot command        |
| `RefTestManagement.Hosting`                                        | Shared API/Worker hosting composition, `IHostingProviderAdapter` contract, and mode selection |
| `RefTestManagement.Hosting.Azure`                                  | Azure Key Vault Data Protection and managed-identity Redis adapter implementation |
| `RefTestManagement.Auth0`                                        | Auth0 Management API client — resolves approvers by permission at runtime (used when `Identity:Provider` is `Auth0`) |
| `RefTestManagement.Keycloak`                                     | Keycloak Admin REST client and permission sync; `Configuration/`, `Services/`, `Extensions/` (used when `Identity:Provider` is `Keycloak`) |
| `RefTestManagement.Application/Abstractions`                     | Ports implemented by Infrastructure, including `IRefTestUnitOfWork`, `IJobQueueStore` and `IPersonalDataExportRequestCleanup` |
| `RefTestManagement.Application/RefTests`                         | Use-case handlers called by GraphQL resolvers and job handlers, including creation, report requests, privacy-notice acceptance, completion, approval, rejection, reset, revive, updates, on-request emails and deletion |
| `RefTestManagement.Infrastructure/IhfRules`                      | IHF Rules question-bank GraphQL client (StrawberryShake schema, queries) and its service |
| `RefTestManagement.Security`                                     | `Authorization/` (permission constants, handlers, policy provider), `Identity/` (identity provider name, permission-sync contract), `Extensions/` (DI registration) |
| `RefTestManagement.Infrastructure/Services/Email`                | Email delivery (Brevo) and approval notifications                             |
| `RefTestManagement.Infrastructure/Services/Reports`              | Excel and PDF renderers for the RefTest report; `RefTestReportService` only orchestrates and emails them |
| `RefTestManagement.Infrastructure/Services/Privacy`              | Consent-withdrawal and data-export challenges, participant token adapters, and EF-backed export-request cleanup. Withdrawal stays behind its Application port: every step is a serializable transaction under EF's retry strategy |
| `RefTestManagement.Infrastructure/Services/Sessions`             | Participant session lease and session services                                |
| `RefTestManagement.Infrastructure/Services/Shared`               | PDF/Excel generation helpers shared across services                           |
| `RefTestManagement.Infrastructure/Persistence`                   | `RefTestManagementContext`, its extensions, and the EF unit-of-work implementations |
| `RefTestManagement.Infrastructure/Jobs/Handlers`                 | Background job handlers keyed by job type; `AddJobHandlers()` registers them  |
| `RefTestManagement.Infrastructure/Jobs/Payloads`                 | Job payload records and the EF-backed job queue store contract                |
| `RefTestManagement.AuditLog/Model`, `/Interceptors`, `/Redaction`, `/Configuration` | Audit event entity and EF configuration, SaveChanges interceptor, PII redaction, and options/DI |
| `RefTestManagement.Migrations.*/Extensions`, `/Design`           | Provider-specific DI extensions for migrations, and design-time context factories |
| `RefTestManagement.UnitTests`                                    | Unit tests grouped by feature folder (`Architecture/`, `Audit/`, `Authorization/`, `Configuration/`, `Hosting/`, `Identity/`, `Jobs/`, `Localization/`, `Privacy/`, `RefTests/`, `Reports/`) |
| `RefTestManagement.Ui/src/app/ref-tests`                         | RefTest creation, detail view, and management UI                              |
| `RefTestManagement.Ui/src/app/ref-tests/list`                    | List view with mobile/desktop layouts, filters and operations                 |
| `RefTestManagement.Ui/src/app/ref-tests/list/services`           | Business logic services for data, filters, state and operations               |
| `RefTestManagement.Ui/src/app/ref-tests/list/components/dialogs` | All bulk-action dialogs incl. approve & reject                                |
| `RefTestManagement.Ui/src/app/audit-logs`                        | Audit log list page with filters, expandable change diffs and entity linking  |
| `RefTestManagement.Ui/src/app/auth`                              | Auth guard, permission guard, `HasPermission` directive, `PermissionsService` |
| `RefTestManagement.Ui/src/app/ref-test`                          | RefTest-taking experience (welcome, take, results)                            |
| `RefTestManagement.Ui/src/app/privacy`                           | Privacy notice display and consent-withdrawal UI                              |
| `RefTestManagement.Ui/graphql/ref-tests/mutations`               | All management mutations incl. `approve-ref-tests` and `reject-ref-tests`     |
| `.github/workflows`                                              | CI/CD pipelines for automated testing and deployment                          |
