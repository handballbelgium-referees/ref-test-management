---
applyTo: "**/*.cs"
---

# Backend conventions

The invariants in `.github/copilot-instructions.md` (permissions and `[Authorize]`, migrations in all four providers, logging, audit log, test command) apply to every `.cs` change. This file adds only the details below.

- Put GraphQL queries and mutations under `RefTestManagement.Api/Graphql/Queries` and `RefTestManagement.Api/Graphql/Mutations/<Area>`, following the nearest feature.
- For every GraphQL field or mutation, verify all three parts together: the permission constant, the `[Authorize(Policy = ...)]` requirement, and the `docs/SECURITY.md` entry.
- Follow existing .NET and HotChocolate patterns. Use the `hotchocolate-best-practices` skill for general HotChocolate guidance.
