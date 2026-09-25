---
applyTo: "**/*.cs"
---

# Backend conventions

- Put GraphQL queries and mutations under `RefTestManagement.Api/Graphql/Queries` and `RefTestManagement.Api/Graphql/Mutations/<Area>`, following the nearest feature.
- For every GraphQL field or mutation, verify the permission constant in `Permissions.cs`, the `[Authorize(Policy = ...)]` requirement, and the corresponding `docs/SECURITY.md` entry. Record security-relevant state changes in the audit log.
- An EF model change requires a same-named migration in `RefTestManagement.Migrations.SqlServer`, `RefTestManagement.Migrations.PostgreSQL`, `RefTestManagement.Migrations.SQLite`, and `RefTestManagement.Migrations.MySQL`. Use the commands in `docs/CONFIGURATION.md`.
- Keep `[LoggerMessage]` declarations in static partial classes. Never log participant PII, secrets, or unredacted exception data that may contain them.
- Add xUnit v3 tests to `RefTestManagement.UnitTests`. This repository uses Microsoft.Testing.Platform; target a class with `dotnet test --solution RefTestManagement.slnx --configuration Release --filter-class Handball.Belgium.RefTestManagement.UnitTests.AuthorizationTests` or a method with `--filter-method <fully-qualified-method>`.
- Follow existing .NET and HotChocolate patterns. Use the `hotchocolate-best-practices` skill for general HotChocolate guidance.
