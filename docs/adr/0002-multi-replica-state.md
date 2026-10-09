# ADR 0002: State and side effects across replicas

- **Status:** Accepted
- **Date:** 2026-10-09
- **Related:** [ADR 0001](0001-layered-architecture.md), `docs/CONFIGURATION.md` ("Shared Redis")

## Context

The API is moving from a single App Service instance to several replicas (Azure Container Apps). Three things were
process-local or unsafe to repeat:

- GraphQL subscriptions used in-memory delivery, so a mutation on one replica did not reach subscribers on another.
- The participant single-tab lock was a dictionary in one process, and a crashed process never released it.
- Email jobs retried the whole send. When a send's response was lost after the provider accepted it, the retry
  delivered the email a second time.

## Decision

1. **One shared Redis connection, configured once.** `RedisConfiguration:Endpoint` (authenticated TLS, validated at
   startup, required on Container Apps) owns the connection. When it is set, subscriptions use Redis and the session
   lock becomes a Redis lease; the privacy rate limiter uses the same connection when its backend is `Redis`. When it
   is empty, everything stays in-process for development and single-instance hosting.
2. **The session lock is a lease**: 90 seconds, renewed every 30 seconds by the open subscription, and renewed or
   released only by the session that holds it. A replica that dies stops renewing and the RefTest unlocks.
3. **Subscription events are best effort, without an outbox.** They are live-refresh hints published after commit;
   Redis pub/sub delivers at most once anyway, so an outbox would only guarantee the publish, not the delivery. A
   failed publish is logged and swallowed so it cannot fail a committed mutation (inviting a duplicate retry) or a job
   whose email already went out. Screens catch up on their next query.
4. **Job emails are idempotent at the provider.** While a job runs, `EmailService` sends a Brevo `idempotencyKey`
   derived from the job, recipient and subject. Brevo drops a repeat within 30 minutes and answers
   `duplicate_parameter`, which counts as delivered. Job retries run within seconds, well inside that window.
5. **Notification emails are not events.** They are staged as job rows in the same transaction as the change, so they
   are already as reliable as the change itself.

## Consequences

- Multi-replica hosting requires Redis 7+ and an identity allowed `SET`, `EVAL`/`EVALSHA`, `PUBLISH`, `SUBSCRIBE` and
  the limiter's commands. `PrivacyChallengeConfiguration:RedisEndpoint` was replaced by `RedisConfiguration:Endpoint`
  (breaking configuration change).
- A Redis outage degrades live updates and blocks new single-tab sessions, but does not fail mutations or jobs.
- A job retried more than 30 minutes after a lost response could still send twice. With the default 5-minute job
  lock that needs a worker outage of half an hour, or `BackgroundJobConfiguration:LockDurationMinutes` above 30;
  accept it rather than adding a delivery ledger.
- Emails sent directly by a request, outside a job, carry no key; they are not retried automatically.
