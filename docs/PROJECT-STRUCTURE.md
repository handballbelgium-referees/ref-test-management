# 📁 Project Structure

A high-level map of the repo — expand a project to see its top-level folders. For the exact, current layout, use your editor's file explorer or GitHub's file browser; this doc only tracks stable, top-level organization so it doesn't drift as components are added.

- **`.github/workflows/`** — CI/CD pipelines (PR checks, releases, cleanup)
- **`.husky/`** — Git hooks (commit-msg, pre-commit)
- **`badges/`** — Auto-generated release badge images
- **`docs/`** — Architecture, security, privacy, configuration docs
- **`scripts/`** — Repo automation (badges, README version sync)

<details open>
<summary><strong><code>RefTestManagement.Api/</code></strong> — 🔷 .NET Web API (.NET 10) — entry point, GraphQL, background services</summary>

- `BackgroundServices/` — Hosted services: jobs, expiration, privacy retention, audit cleanup, permission sync
- `Controllers/` — Auth0 login/callback endpoints
- `Graphql/` — Mutations, Queries, Subscriptions, Types, ReadModels

</details>

- **`RefTestManagement.AuditLog/`** — 📋 Domain-event–driven audit log library (Marten-style event store)
- **`RefTestManagement.Auth0/`** — 🔐 Auth0 Management API client (M2M token, role-based user discovery)
- **`RefTestManagement.Application/`** — 🔷 Business logic — external GraphQL client (IHF Rules), configuration models
- **`RefTestManagement.Domain/`** — 🔷 Domain entities: RefTest, RefTestTitle, Job, and their domain events
- **`RefTestManagement.Security/`** — 🔐 Permission constants, authorization handlers, dynamic policy provider
- **`RefTestManagement.Infrastructure/`** — 🔷 EF Core DbContext, migrations, PDF/Excel/email service implementations

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

## Key Directories

| Directory                                                        | Purpose                                                                       |
| ---------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| `RefTestManagement.AuditLog`                                     | Self-contained audit log library — interceptor, entity, options, DI extension |
| `RefTestManagement.Api/Graphql`                                  | GraphQL schema, queries, mutations, and type definitions                      |
| `RefTestManagement.Api/Graphql/Mutations/Approval`               | Approve/reject mutations (requires `ref-tests:approve`)                       |
| `RefTestManagement.Auth0`                                        | Auth0 Management API client — resolves approvers by permission at runtime     |
| `RefTestManagement.Application/GraphQL`                          | External GraphQL client schemas and queries (IHF Rules)                       |
| `RefTestManagement.Security`                                     | Permission constants, authorization handlers and policy provider              |
| `RefTestManagement.Infrastructure/Services`                      | PDF/Excel generation, email delivery (Brevo), approval notifications          |
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
