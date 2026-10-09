# ADR 0001: Layered backend architecture

- **Status:** Accepted
- **Date:** 2026-10-08
- **Enforced by:** `RefTestManagement.UnitTests/ArchitectureDependencyTests.cs` and `DomainDependencyTests.cs`

## Context

The backend is split into several projects, but the code does not always respect the layers their names suggest:

- `RefTestManagement.Application` holds configuration models, job payloads and the StrawberryShake IHF client. It contains no use cases.
- The use-case logic (creating, starting, completing, approving, resetting and emailing RefTests) lives in the GraphQL mutation classes and in `RefTestManagement.Api/Services`. It is coupled to HotChocolate, `IHttpContextAccessor` and the EF Core context.
- The interfaces that Api depends on (`IJobEnqueueService`, `IEmailService`, `IRefTestSubscriptionService`, and others) are declared in Infrastructure, next to their implementations.
- Before this ADR, only the Domain layer had dependency tests, so nothing stopped wrong-direction references from creeping into other layers.

## Decision

Dependencies point inward: Domain ← Application ← Infrastructure / Api, with Api as the composition root only.

```mermaid
flowchart TB
    Api["Api (composition root)"] --> Infrastructure
    Api --> Auth0
    Api --> Migrations["Migrations.*"]
    Auth0 --> Security
    Api --> Security
    Api --> AuditLog
    Migrations --> Infrastructure
    Infrastructure --> Application
    Infrastructure --> AuditLog
    Application --> Domain
    AuditLog --> Domain
```

| Project          | May reference (projects)            | Responsibility                                                                                                             |
| ---------------- | ----------------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| `Domain`         | none; no NuGet packages             | Entities, invariants, domain events                                                                                        |
| `Application`    | `Domain`                            | Use cases and the ports (interfaces) they need; no EF Core, ASP.NET Core, HotChocolate, HTTP clients or document libraries |
| `AuditLog`       | `Domain`                            | Domain-event audit trail (EF Core interceptor)                                                                             |
| `Infrastructure` | `Domain`, `Application`, `AuditLog` | Adapters: EF Core, email, PDF/Excel, subscriptions, external clients                                                       |
| `Migrations.*`   | `Infrastructure`                    | Provider-specific EF Core migrations                                                                                       |
| `Security`       | none                                | Permission constants, authorization handlers and policy provider                                                           |
| `Auth0`          | `Security`                          | Auth0 Management API client                                                                                                |
| `Api`            | anything                            | Composition root: hosting, DI wiring, GraphQL transport, controllers                                                       |

Rules:

1. Application declares the ports it needs. Infrastructure implements them. Api wires them together.
2. GraphQL mutations and queries are transport adapters: authorize, map input, call an Application use case, map the result.
3. A new project must be added to `ArchitectureDependencyTests` with the references its layer may use. The test fails until that happens.

## Known violations

These are tolerated for now and tracked as remediation work.

| Violation                                                                                                      | Enforced                | Planned fix                                                       |
| -------------------------------------------------------------------------------------------------------------- | ----------------------- | ----------------------------------------------------------------- |
| Remaining use cases live in GraphQL mutations and `Api/Services` (creation, deletion and lifecycle handlers now live in Application) | Not yet (type-level)    | WP3: move the remaining use cases into Application handlers |
| Subscription events are published by hand from mutations, separately from domain events                        | Not yet                 | WP4: one event pipeline                                           |
| Job handlers and background services live in Api                                                               | Not yet                 | WP5: separate the worker from the web host                        |

Resolved:

- WP2 (IHF client): the StrawberryShake client and `IhfRulesQuestionsService` live in `RefTestManagement.Infrastructure/Ihf`; Application keeps only the `IIhfRulesQuestionsService` port. Application has no forbidden package references, so the tests no longer carry an allow-list.
- WP2 (ports): the service interfaces (`IJobEnqueueService`, `IEmailService`, `IRefTestSubscriptionService`, …) and the records in their signatures live in `RefTestManagement.Application/Abstractions`. `IJobPersistenceContext` exposes `IQueryable<Job>` and `AddJob` instead of an EF Core `DbSet`, so a job still commits in the caller's unit of work. `ArchitectureDependencyTests.InfrastructureDeclaresNoPorts` fails if Infrastructure declares an interface again.

## Consequences

- The declared project graph is checked on every test run, so a wrong-direction `ProjectReference` fails CI immediately.
- Where interfaces live is enforced by a type-level test. What mutations may call is not enforced yet; extend the tests when WP3 lands.
- The README and `docs/PROJECT-STRUCTURE.md` describe the actual graph. Update them together with this ADR when the graph changes.
