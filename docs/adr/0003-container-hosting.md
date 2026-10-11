# ADR 0003: Provider-neutral container hosting

- **Status:** Accepted
- **Date:** 2026-10-11
- **Related:** [ADR 0001](0001-layered-architecture.md), [ADR 0002](0002-multi-replica-state.md), [deployment guide](../DEPLOYMENT.md)

## Context

The API currently owns HTTP, background services, and deployment-specific configuration. Moving to
containers should not force a provider choice into Domain or Application, and operators need to be
able to run one combined process or separate API and Worker processes. Azure is the initial target;
Cloudflare is a second option with different identity, state, ingress, and data-service capabilities.

## Decision

1. Keep process roles and shared-state selection provider-neutral:
   - `Hosting:Mode=All` runs HTTP and background services in the API process.
   - `Hosting:Mode=Api` runs HTTP only.
   - `Hosting:Mode=Worker` runs background services only.
   - `State:Mode=Local` is for one API process; `Shared` requires authenticated TLS Redis for
     subscriptions, participant session locks, and the privacy challenge limiter.
2. Select host-specific adapters through `Hosting:Provider`; do not add provider-specific
   configuration to Domain or Application. `Local` is for development, `Azure` uses Key Vault and
   managed identity, and `Cloudflare` uses certificate-based Data Protection and the Worker
   assertion boundary.
3. Use the combined `All` mode by default. Enable split mode only when shared Redis, provider
   credentials, health/readiness checks, and the cost profile are ready. Keep the API at one replica
   until GraphQL rate limiting is shared across API instances.
4. Build API and Worker OCI images with the .NET SDK `PublishContainer` target; no Dockerfile is
   required. Azure release workflows publish images to ACR by digest and run the one-shot migration
   job before updating API/Worker revisions. Cloudflare Wrangler owns Worker/container rollout,
   Durable Object bindings, Cron, and runtime secrets.
5. Keep infrastructure ownership provider-specific. Azure Terraform provisions ACA, SQL, Key
   Vault, identities, and optional managed Redis. Cloudflare Terraform provisions R2 state and
   attaches a hostname to an already deployed Worker; PostgreSQL and Redis remain external inputs.

| Provider | Combined mode | Split mode | Persistent/shared services |
| --- | --- | --- | --- |
| Azure Container Apps | One API app in `All` mode | API app plus Worker app and manual migration job | Entra-only Azure SQL; optional Azure Managed Redis |
| Cloudflare Workers/Containers | One `AppContainer` runs API and background services | `AppContainer` API plus `WorkerContainer` | External PostgreSQL; external TLS Redis in split mode |

## Consequences

- Hosting topology can change without introducing Azure or Cloudflare types into application
  use-cases. Database provider and host-specific secrets remain explicit deployment choices.
- Split deployments require provider-supported shared Redis and increase cost/operational
  complexity. Azure split mode remains cost-gated; API scale-out remains disabled.
- Azure public ingress requires verified ACA peer CIDRs before applications are enabled. Missing
  evidence is a deployment blocker, not a reason to trust broad address ranges.
- Cloudflare uses a Worker-owned proxy assertion and certificate for key protection rather than
  Azure managed identities or Key Vault. The account's Containers/Durable Objects features, external
  service egress, hostname, and current costs must be verified before production.
- Cloudflare's one-minute Cron wake and container startup may delay background work. The existing
  SQL-backed queue remains durable; this does not provide a strict execution-latency guarantee.
- The Cloudflare hostname must belong to an active zone. Terraform can create the Worker custom
  domain, DNS record, and TLS certificate, but does not transfer a domain or change registrar or
  nameserver settings.
