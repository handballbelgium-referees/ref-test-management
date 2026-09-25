# Repository Copilot instructions

## Plan gate
- Before changing tracked files, present a complete written plan and wait for explicit approval.
- In CLI plan mode, use `exit_plan_mode`. Otherwise present the plan and use `ask_user` with Approve/Request changes.
- If the host has no `ask_user`, show the plan and wait for an explicit approval reply; never infer approval.
- A trivial change still gets a three-line plan. Do not implement before approval.
- An implementer may act only on one approved work package (WP); it must not ask for approval again.
- Stop and request a revised plan if implementation exceeds the approved scope.
- For cloud-agent work, put the plan in the PR description and make no code changes until a maintainer comments `@copilot approved`.

## Repository map
- Backend: .NET 10 and HotChocolate 16 in `RefTestManagement.Api`, `RefTestManagement.Domain`, `RefTestManagement.Application`, `RefTestManagement.Infrastructure`, `RefTestManagement.Security`, and `RefTestManagement.Auth0`.
- Database providers: `RefTestManagement.Migrations.SqlServer`, `.PostgreSQL`, `.SQLite`, and `.MySQL`. Tests: `RefTestManagement.UnitTests`.
- Frontend: Angular 22 in `RefTestManagement.Ui`. GraphQL documents are in its `graphql/` directory.
- Start with `README.md`; see `docs/CONFIGURATION.md`, `docs/SECURITY.md`, and `docs/AUDIT-R*.md` for operational rules and audit history.

## Build and test
- Backend build: `dotnet build RefTestManagement.slnx -nologo -v q -clp:ErrorsOnly`.
- Targeted backend test: `dotnet test --solution RefTestManagement.slnx --configuration Release --filter-class Handball.Belgium.RefTestManagement.UnitTests.AuthorizationTests`. MTP requires `--solution`; use `--filter-class` or `--filter-method` for xUnit v3.
- UI spec (run in `RefTestManagement.Ui`): `npm test -- --include src/app/privacy/privacy-notice.spec.ts --watch=false`. Also run `npm run check:i18n` and `npm run build -- --configuration production` when affected.
- `npm run codegen` requires a running API.
- Done means the build and affected tests pass; report any check that could not run.

## Invariants
- Use only Conventional Commit types/scopes from `commitlint.config.mjs`; commit and PR-title headers are at most 100 characters. PR bodies use `Closes #N`.
- Preserve XML documentation, JSDoc, and comments that explain why.
- A GraphQL operation needs a permission constant in `Permissions.cs`, `[Authorize(Policy = ...)]`, and a matching entry in `docs/SECURITY.md`.
- An EF model change needs a same-named migration in all four provider projects. Follow `docs/CONFIGURATION.md`.
- Keep `[LoggerMessage]` methods in static partial classes and never put PII in logs.
- Put user-visible UI text in all four locales: `en`, `nl`, `fr`, and `de`.
- `main` is protected and every push triggers a pre-release; do work on `feat/*`, `fix/*`, or `chore/*`.

## Token hygiene
- Do not read lock files, `RefTestManagement.Ui/graphql/generated.ts`, `docs/AUDIT*.md`, or locale JSON in full. Search first, then use ranged reads.
- Run `git diff --stat` before reading a full diff.
- Send long build, test, or hook output to a temp log and show only errors or a short tail.
