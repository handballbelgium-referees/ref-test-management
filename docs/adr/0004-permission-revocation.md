# ADR 0004: Permission revocation latency

- **Status:** Accepted
- **Date:** 2026-10-16
- **Related:** [ADR 0002](0002-multi-replica-state.md), `docs/SECURITY.md` ("Permission freshness and failure behavior"), `docs/CONFIGURATION.md` ("Identity permission freshness")

## Context

Permissions are granted and revoked in the identity provider (Auth0 now, Keycloak within about a week). The API caches
each user's resolved permissions in process memory for `Identity:PermissionCacheSeconds` (default 240). Before this
decision, a revoked permission stayed effective on every replica until its cache entry expired, and the UI kept showing
the matching controls. The API can run as several replicas, so an invalidation signal would have to reach each of them.

Options considered:

- **Webhook or log-stream invalidation.** Auth0 log streams are asynchronous and batched, with a static header and no
  HMAC; Keycloak has no built-in webhook (it needs a custom Event Listener SPI or admin-event polling). Both add a
  provider-specific integration, an event-delivery delay, and a second source of truth that can lag or drop events.
- **Live identity-provider check on every request.** Simple and immediate, but adds an external call to every read and
  makes provider availability a dependency of every query.
- **Live check on mutations only, cache elsewhere.** Writes are checked at call time. Reads and UI visibility keep the
  cache.

## Decision

1. **Mutations** that need a permission use `live:` policies. The handler checks the cache first, so a user the cache
   denies never causes an identity-provider call. For a cache-granted permission, the API asks the provider directly
   through `IIdentityPermissionSync`, with a timeout of `Identity:LivePermissionTimeoutSeconds` (default 2). A timeout
   or provider failure denies the write (fail closed).
2. **Reads and UI visibility** use the TTL cache. Worst-case lag is one cache lifetime per process (four minutes by
   default). The UI polls about every minute, so a hidden control can appear for a short time after revocation, but the
   backend rejects the write.
3. **Sign-in and token refresh** force a fresh permission snapshot, bypassing the cache. The cookie validator renews
   the access token from a stored refresh token when it is within 60 seconds of expiry. It uses single-flight per refresh
   token and reuses a successful result for 60 seconds. Any refresh failure signs the user out.
4. **Subscriptions** re-check the live permission every 30 seconds and before events that follow a check older than
   30 seconds. A revoked user's stream ends with no further events.
5. No webhook and no provider-side event integration is added. Auth0 and Keycloak need only the provider settings listed
   in `docs/CONFIGURATION.md` (refresh-token grant and `offline_access`).

## Consequences

- A revoked permission is refused at the next write, bounded by the live-check timeout. This is the main security gain.
- Each write by a user the cache grants adds one Management API (Auth0) or Admin API (Keycloak) call. Provider rate
  limits and the added write latency were not measured and must be checked before rollout.
- Writes depend on provider availability: when the provider is slow or down, writes are denied rather than allowed.
- Reads still lag by up to one cache lifetime per process. Lowering `Identity:PermissionCacheSeconds` narrows this at
  the cost of more provider calls.
- Refresh-token rotation races between replicas are not coordinated. A rotating refresh token used concurrently on two
  replicas can sign the user out. Accepted for now; verify against a live tenant.
- The refresh flow and `live:` policies are covered by unit tests only. They have not been exercised against a real Auth0
  or Keycloak tenant.
