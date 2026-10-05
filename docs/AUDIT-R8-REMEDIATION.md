# Audit Remediation Plan — R8

> **Status at a glance —** This file tracks Round 8 follow-up work packages, starting at
> **WP-73** (continuing after WP-72 in the R7 remediation wave).

Companion to [`AUDIT-R8.md`](./AUDIT-R8.md). The report records evidence and impact; this file
tracks the approved remediation work packages and their verification status.

---

## R8 remediation wave (2026-10-04)

**Approval:** Approved by the user's instruction to stop planning and begin implementation.  
**Scope:** All 23 R8 findings; external Auth0/GitHub/ingress settings and legal GDPR evidence remain
unverified. No production data operation is authorized by this plan.  
**Assumptions:** Cookie permission revocations take effect within five minutes; expired permission
snapshots fail closed. Release publication is blocked while High advisories remain, without an
advisory allowlist. Raw client-IP headers require a known proxy/network.

### WP-73 — Preserve atomicity for per-item creation and revive

**Findings:** R8-01 (🟡 Medium)  
**Status:** ✅ Implemented  
**Size:** M  
**Priority:** P1  
**Dependencies:** None

**Files**
- `RefTestManagement.Api/Graphql/Mutations/Creation/RefTestCreationMutations.cs`
- `RefTestManagement.Api/Graphql/Mutations/Reset/RefTestResetMutations.cs`
- `RefTestManagement.Infrastructure/Services/JobEnqueueService.cs`
- `RefTestManagement.AuditLog/AuditSaveChangesInterceptor.cs`
- Relevant unit-of-work and mutation tests in `RefTestManagement.UnitTests`

**Change**
- Make each RefTest and its required invitation/approval outbox work atomic.
- Roll back failed revive/enqueue work, including staged cancellations, token changes, and domain
  events; align result counters and errors with persisted state.
- Define approval-notification enqueue failure as a failed batch rather than persisting orphaned
  pending-approval records.

**Acceptance**
- Failed invitation enqueue leaves no RefTest or job; failed revive leaves state, tokens, jobs, and
  audit events unchanged.
- Successful items preserve current behavior, and counters describe only persisted items.

**Tests**
- Add failure-injection tests using the SQLite in-memory unit-of-work pattern.
- Run `dotnet test --solution RefTestManagement.slnx --configuration Release`.
- **Verification:** Failure-injection tests passed (4/4); full Release solution tests passed
  (308/308); solution build passed with 0 warnings/errors; `git diff --check` passed. Code review
  found no actionable issues.

**Watch out for**
- Preserve batch partial-success semantics where valid and account for `SaveChanges` interceptors
  draining domain events from tracked entities.

### WP-74 — Reconcile incomplete privacy-withdrawal batches

**Findings:** R8-02 (🟡 Medium)  
**Status:** ✅ Implemented  
**Size:** L  
**Priority:** P1  
**Dependencies:** None

**Files**
- `RefTestManagement.Api/Services/PrivacyWithdrawalRequestService.cs`
- `RefTestManagement.Api/BackgroundServices/PrivacyWithdrawalCleanupService.cs`
- `RefTestManagement.Api/BackgroundServices/JobHandlers/PrivacyWithdrawalBatchJobHandler.cs`
- `RefTestManagement.Domain/Jobs/Job.cs` and withdrawal target state as needed
- `RefTestManagement.Migrations.SqlServer`, `.PostgreSQL`, `.SQLite`, and `.MySQL` if durable
  attempt/backoff state requires a model change
- `PrivacyWithdrawalPipelineTests` and related job tests
- `docs/PRIVACY.md`

**Change**
- Reuse request-driven recovery semantics in a scheduled, idempotent reconciliation for incomplete
  targets whose batch job is missing or terminal.
- Add durable backoff/attempt bounds and a non-PII failure signal; do not repeatedly requeue a
  deterministically failing target without limit.

**Acceptance**
- Recovery does not depend on another participant request, creates at most one replacement job,
  and leaves active leases and completed batches untouched.
- Repeated sweeps are idempotent; exhaustion is observable without logging participant data.

**Tests**
- Cover terminal/missing job recovery, active processing leases, completed targets, repeated sweeps,
  and request-driven recovery races; run the .NET unit suite.

**Verification**
- Focused privacy-withdrawal pipeline tests passed (39/39); full Release .NET suite passed
  (317/317); solution build passed with 0 warnings/errors; `git diff --check` passed.
- Final code review found no actionable issues. All four provider migrations and designer/snapshot
  models were verified consistent; MySQL translation was checked with the registered provider, not a
  live MySQL server. No migration was applied.

**Watch out for**
- Keep erasure idempotent and preserve retention rules; if a model change is necessary, add the
  same-named migration to all four providers.

### WP-75 — Remove rejection text from all erasure copies

**Findings:** R8-03 (🟡 Medium)  
**Status:** ✅ Implemented  
**Size:** L  
**Priority:** P1  
**Dependencies:** None

**Files**
- `RefTestManagement.Domain/RefTests/RefTest.cs`
- `RefTestManagement.Domain/RefTests/Events/RefTestSimpleEvents.cs`
- `RefTestManagement.Infrastructure/Services/RefTestPrivacyErasureService.cs`
- `RefTestManagement.AuditLog/AuditPiiRedactor.cs`
- `RefTestManagement.Application/Models/JobPayloads.cs`
- Privacy erasure, audit-redactor, and job-payload tests
- `docs/PRIVACY.md`

**Change**
- Clear the free-text reason on the RefTest, redact it from retained rejection events, and scrub
  the matching reason from approval-notification job payloads, including completed jobs.
- Add a bounded, idempotent repair path for already-anonymized records and their retained audit/job
  payloads; update the privacy statement to match the behavior.

**Acceptance**
- After erasure/repair, no rejection reason remains in the row, retained audit event, or related
  approval job payload.
- Re-running repair is safe and idempotent; existing audit accountability is preserved without
  retaining free-text identifiers.

**Tests**
- Cover new erasure, already-anonymized repair, completed job payloads, and repeated runs; run the
  .NET unit suite.

**Verification**
- Targeted erasure, redactor, and retention tests passed; full Release .NET suite passed (327/327);
  solution build passed with 0 warnings/errors; `git diff --check` passed.
- Final code review found no significant issues. The bounded repair was not run against production.

**Watch out for**
- Keep the repair bounded and non-PII in logs. This plan does not authorize running it against a
  production database.

### WP-76 — Scope Auth0 approver lookup to the API audience

**Findings:** R8-05 (🟡 Medium)  
**Status:** ✅ Implemented  
**Size:** M  
**Priority:** P1  
**Dependencies:** None

**Files**
- `RefTestManagement.Auth0/Services/Auth0ManagementService.cs`
- `RefTestManagement.Api/BackgroundServices/JobHandlers/ApprovalNotificationEmailJobHandler.cs`
- Auth0 management-service and approval-notification tests

**Change**
- Match both role-based and direct-user grants by permission name and the configured
  `ResourceServerIdentifier`/API audience.

**Acceptance**
- A permission granted only on a different resource server never selects an approver or receives
  an approval notification.

**Tests**
- Cover matching and non-matching audiences for direct and role grants and verify notification
  recipients; run the .NET unit suite.

**Verification**
- Targeted Auth0 service and notification tests passed (2/2); full Release .NET suite passed
  (329/329); solution build passed with 0 warnings/errors; `git diff --check` passed.
- Final code review found no findings. Live Auth0 tenant grants were not queried or changed.

**Watch out for**
- Apply the audience check consistently to wildcard and superadmin matching; tenant grant settings
  remain external evidence.

### WP-77 — Refresh cookie permissions within five minutes

**Findings:** R8-04 (🟡 Medium)  
**Status:** ✅ Implemented  
**Size:** L  
**Priority:** P1  
**Dependencies:** WP-76

**Files**
- `RefTestManagement.Api/SecurityStartup.cs`
- `RefTestManagement.Api/Controllers/AccountController.cs`
- Permission resolution/authorization services and `RefTestManagement.Security`
- `RefTestManagement.Api/Graphql/Subscriptions/RefTestSubscriptions.cs`
- `RefTestManagement.Ui/src/app/auth/services/permissions.ts`
- Auth, cookie, subscription, and `TimeProvider` tests
- `docs/SECURITY.md` and `docs/CONFIGURATION.md`

**Change**
- Resolve current effective grants with a per-user single-flight cache no older than five minutes;
  refresh or invalidate cookie claims and ensure the UI permission endpoint returns the refreshed
  grants.
- Revalidate or terminate admin PII subscriptions within the same freshness window.
- Fail closed when a permission snapshot is stale and cannot be refreshed; document least-privilege
  Management API scopes and operational requirements.

**Acceptance**
- A revoked permission stops authorizing API/UI actions and admin PII subscriptions within five
  minutes.
- Refresh failures do not extend stale authorization, and concurrent checks do not create an
  unbounded Auth0 request burst.

**Tests**
- Use `TimeProvider` for freshness boundaries; cover revocation, cache expiry, Auth0 429/outage,
  refreshed `AccountController` results, and subscription revalidation/termination.
- Run the .NET unit suite.

**Verification**
- Auth/cache/Auth0 tests passed; full Release .NET suite passed (344/344); solution build passed
  with 0 warnings/errors; permissions UI spec passed (2/2); i18n keys match in all locales;
  production UI build passed with its initial-bundle budget warning; `git diff --check` passed.
- Final GPT-6 Luna code review found no significant issues. External tenant Management API scopes
  and rate limits remain unverified because the required research CLI is not installed; no live
  Auth0 tenant changes were made.

**Watch out for**
- Authentication uses no refresh token today. Verify required Management API scopes, rate limits,
  cookie renewal behavior, and failure handling without logging claims or participant PII.

### WP-78 — Protect GraphQL question identifiers

**Findings:** R8-13 (⚪ Low)  
**Status:** ✅ Implemented  
**Size:** M  
**Priority:** P2  
**Dependencies:** None

**Files**
- `RefTestManagement.Api/Graphql/Types/QuestionType.cs`
- `RefTestManagement.Api/Graphql/Types/RefTestType.cs`
- `docs/SECURITY.md`
- GraphQL authorization/schema tests in `RefTestManagement.UnitTests`

**Change**
- Require the documented question-specific permission for the `questions` field and nested
  question identifiers.
- Document that result-ID arrays remain available to `ViewDetail` as result metadata unless the
  schema test demonstrates otherwise.

**Acceptance**
- The schema's field-permission matrix matches `docs/SECURITY.md`; a `ViewDetail`-only user cannot
  retrieve nested question IDs through `questions`.
- Participant question flow remains unchanged.

**Tests**
- Cover `ViewDetail` versus `ViewDetailQuestions` in GraphQL schema authorization tests; run the
  .NET unit suite.

**Verification**
- Targeted GraphQL authorization tests passed (4/4); full Release .NET suite passed (345/345);
  solution build passed with 0 warnings/errors; `git diff --check` passed.
- Final GPT-6 Luna code review found no significant issues.

**Watch out for**
- Preserve the intended distinction between question content and limited result metadata; check the
  staff detail-page query for compatibility.

### WP-79 — Align UI language with enabled locales

**Findings:** R8-06, R8-18, R8-19 (🟡 Medium/⚪ Low)  
**Status:** ✅ Implemented  
**Size:** M  
**Priority:** P1  
**Dependencies:** None

**Files**
- `RefTestManagement.Ui/src/index.html`
- `RefTestManagement.Ui/src/app/app.ts`
- `RefTestManagement.Ui/src/app/app.config.ts`
- `RefTestManagement.Ui/src/app/services/language-config.ts`
- `RefTestManagement.Ui/src/app/ref-test/take/take-ref-test.html`
- `RefTestManagement.Ui/public/i18n/{en,nl,fr,de}.json`
- Relevant UI language/accessibility specs

**Change**
- Update `document.documentElement.lang` on initial selection and every language change.
- Use the first enabled language for unsupported browser/saved values and derive `LOCALE_ID` from
  the selected language instead of hard-coding English.
- Translate the “Close” accessible name in all four locales.

**Acceptance**
- Initial and changed document language and locale match the selected enabled language; disabled
  English is never selected as a fallback.
- All locale files contain the required translated key.

**Tests**
- Add initial/switch/disabled-English specs; run `npm run check:i18n`, focused UI specs,
  `npm test -- --watch=false`, and `npm run build -- --configuration production`.

**Verification**
- Focused language/config specs passed (3/3); full UI suite passed (101/101); all four locales
  expose the same 698 keys; production build passed with the existing initial-bundle budget warning
  (605.38 kB vs. 600 kB); `git diff --check` passed.
- Final GPT-6 Luna code review found no significant issues.

**Watch out for**
- Preserve `de.json` escaping and locale-file CRLF/indentation; do not reserialize locale JSON.

### WP-80 — Expose answer selection and input labels

**Findings:** R8-07, R8-20 (🟡 Medium/⚪ Low)  
**Status:** ✅ Implemented  
**Size:** S  
**Priority:** P1  
**Dependencies:** None

**Files**
- `RefTestManagement.Ui/src/app/ref-test/take/components/answer-option/answer-option.html`
- Its component/spec
- `RefTestManagement.Ui/src/app/audit-logs/components/audit-log-filter/audit-log-filter.html`
- Its component/spec

**Change**
- Expose answer selection with `aria-pressed` or equivalent native toggle semantics.
- Give the Stream ID input a unique ID and associate the visible label with `for`.

**Acceptance**
- Assistive technology can determine selected answers, and the visible Stream ID label names its
  input.

**Tests**
- Assert DOM accessibility semantics and run the focused specs plus the UI test suite.

**Verification**
- Focused accessibility specs passed (4/4); full UI suite passed (103/103); all four locales
  expose the same 698 keys; production build passed with the initial-bundle budget warning
  (605.38 kB vs. 600 kB); `git diff --check` passed.
- GPT-6 Luna code review found no significant issues; the decorative selection icon is marked
  `aria-hidden`.

**Watch out for**
- Preserve keyboard operation, visible focus, and existing answer selection behavior.

### WP-81 — Harden release workflow provenance

**Findings:** R8-08, R8-09, R8-10 (🟡 Medium)  
**Status:** ✅ Implemented  
**Size:** L  
**Priority:** P1  
**Dependencies:** None

**Files**
- `.github/workflows/beta-release.yml`
- `.github/workflows/stable-release.yml`
- `.github/workflows/codeql.yml`
- `.releaserc.mjs` only if required by the workflow design

**Change**
- Run required candidate validation before tag/release publication; make stable promotion use the
  validated commit rather than moving `origin/main`.
- Pass and verify one validated SHA through tag, build, and deployment checkouts; serialize
  concurrent releases.
- Disable checkout credential persistence by default. Expose write credentials only to the exact
  semantic-release/push step that needs them, never to `npm ci` or build steps.

**Acceptance**
- No publication, promotion, or deployment occurs before validation succeeds; every artifact/tag
  is tied to the validated SHA.
- Workflow permissions are least-privilege and no write credential is available during untrusted
  dependency/build execution.
- A main/release divergence is reported as a failure requiring reconciliation, never as a successful
  skipped sync.

**Tests**
- Validate YAML and job dependency/trigger graphs; run semantic-release dry-run only, never an
  actual release. Confirm the hosted CI checks after workflow changes.
- **Verification:** YAML parsing, all 53 Bash run blocks, provenance/credential/concurrency checks,
  semantic-release dry-run, and `git diff --check` passed locally. Hosted CI was not confirmed
  because no push was authorized.

**Watch out for**
- Preserve the badge/promotion push with step-scoped authentication; keep tags and release outputs
  from racing or diverging. GitHub rulesets and secret configuration are not changed here.

### WP-82 — Enforce dependency security and locked restores

**Findings:** R8-11 (🟡 Medium), R8-22/R8-23 (⚪ Low)  
**Status:** ✅ Implemented; privileged releases remain blocked by 11 unresolved High npm advisories.  
**Size:** L  
**Priority:** P1  
**Dependencies:** WP-81

**Files**
- Root `package.json` and `package-lock.json`
- `.github/workflows/pr.yml`
- `.github/workflows/beta-release.yml`
- `.github/workflows/stable-release.yml`
- `.github/workflows/codeql.yml`
- `.github/workflows/copilot-setup-steps.yml`

**Change**
- Refresh advisory data; remove unused `@semantic-release/changelog` and
  `@semantic-release/git`, update compatible vulnerable release dependencies and the bundled npm
  version where applicable.
- Add a blocking High-level root audit before privileged release credentials are minted. Keep
  releases blocked while High advisories remain; do not use `npm audit fix --force` or add an
  advisory allowlist.
- Use `dotnet restore --locked-mode` on CI restore/build paths and add a SHA-pinned dependency
  review check for newly introduced High/Critical npm and NuGet advisories.

**Acceptance**
- In-scope root release-tool dependencies have no unresolved High advisory, or privileged release
  remains blocked pending an upstream fix.
- Locked restore passes at the branch tip and fails on lockfile drift; PR checks reject newly
  introduced advisories at the documented threshold.

**Tests**
- `npm ci` passed. `npm audit --audit-level=high` exited 1 with 0 Critical, 11 High, and 1 Moderate
  advisory; this is the intended release block.
- Locked .NET restore, the lockfile-drift probe (rejected with `NU1004`), solution build, workflow
  YAML/permission validation, and `git diff --check` passed. GPT-6 Luna review found no significant
  issues.
- Hosted dependency-review behavior and GitHub dependency-graph settings remain unverified; do not
  treat those PR controls as externally enforced until confirmed in CI.

**Watch out for**
- The current `braces` advisory may have no compatible upstream fix and may block releases. The
  separately reported UI advisories were not package-attributed in R8; triage them before broad
  UI dependency upgrades. GitHub dependency-graph settings are external evidence.

### WP-83 — Trust client IP only from known proxies

**Findings:** R8-12 (⚪ Low)  
**Status:** ✅ Implemented; production proxy/CIDR settings remain unverified.  
**Size:** M  
**Priority:** P2  
**Dependencies:** None

**Files**
- `RefTestManagement.Api/Program.cs`
- `RefTestManagement.Api/Services/ClientIpResolver.cs`
- Forwarded-header/rate-limit configuration
- `ClientIpResolverTests` and configuration documentation

**Change**
- Capture the transport peer before forwarded-header middleware changes the remote address.
- Accept raw configured client-IP headers only when the original peer matches configured
  `KnownProxies` or `KnownNetworks`; reject unsafe header-only configurations with an actionable
  startup error.

**Acceptance**
- A spoofed header from an untrusted peer cannot change a rate-limit partition; configured trusted
  peers and networks continue to work.

**Tests**
- Focused `ClientIpResolverTests` passed (12/12); solution build passed with 0 warnings/errors;
  `git diff --check` passed. GPT-6 Luna review found no significant issues.

**Watch out for**
- This is a compatibility change. Document the migration; actual production ingress and CIDRs
  remain unverified and must be checked by operators.

### WP-84 — Remove inline-script allowance from CSP

**Findings:** R8-14/R8-15 (⚪ Low)  
**Status:** ✅ Implemented; hosted production behavior was not exercised.  
**Size:** M  
**Priority:** P2  
**Dependencies:** WP-83

**Files**
- `RefTestManagement.Api/Program.cs`
- `RefTestManagement.Ui/src/index.html`
- `RefTestManagement.Ui/angular.json`
- `docs/SECURITY.md`

**Change**
- Remove `unsafe-inline` from `script-src` and remove or synchronize the duplicate CSP meta policy.
- Prevent unapproved inline script generation by disabling Angular critical-CSS inlining if
  required; retain only a narrowly justified nonce/hash when disabling it is insufficient.
- Document CSP and non-development HSTS in the security-header inventory.

**Acceptance**
- Production bundle scripts are permitted only by the response CSP, while inline script execution
  is not broadly allowed; the application stylesheet still renders.
- Documentation accurately lists emitted CSP/HSTS behavior.

**Tests**
- `PrivacyWithdrawalGetRequestTests` passed (1/1); solution build passed with 0 warnings/errors.
- Production UI build and `npm run check:i18n` passed. The built index has no inline scripts,
  inline styles, or CSP meta policy, and its external stylesheet asset exists. The initial bundle
  measured 605.38 kB, 5.38 kB above the warning budget. `git diff --check` passed.
- GPT-6 Luna review found no significant issues. A visual browser smoke test could not run because
  Chrome and Edge are unavailable; no hosted production deployment was exercised.

**Watch out for**
- Angular's critical-CSS optimizer may emit an inline media-switch script; a header-only test is
  insufficient.

### WP-85 — Retry Auth0 permission synchronization

**Findings:** R8-17 (⚪ Low)  
**Status:** ✅ Implemented; no live Auth0 requests were made.  
**Size:** M  
**Priority:** P2  
**Dependencies:** None

**Files**
- `RefTestManagement.Api/BackgroundServices/PermissionSyncService.cs`
- Auth0 service/configuration as needed
- Permission-sync tests

**Change**
- Keep startup non-blocking while retrying transient failures with bounded exponential backoff and
  cancellation; treat empty credentials as a configuration failure rather than repeatedly calling
  Auth0.
- Surface persistent failure through a non-PII warning/health signal.

**Acceptance**
- Transient failures recover without restart; persistent errors remain bounded and observable;
  shutdown interrupts delay promptly.

**Tests**
- `PermissionSyncServiceTests` passed (5/5), `Auth0ManagementServiceTests` (3/3), and
  `AuthPermissionRefreshTests` (5/5). The solution build passed with 0 warnings/errors;
  `git diff --check` passed.
- Retry attempts are capped at five with 2/4/8/16-second delays; missing credentials stop before
  calling Auth0. GPT-6 Luna review found no significant issues; live Auth0 behavior remains
  unverified.

**Watch out for**
- Avoid hammering Auth0 or logging credentials, permission payloads, or PII.

### WP-86 — Correct Auth0 and language documentation

**Findings:** R8-16/R8-21 (⚪ Low)  
**Status:** ✅ Implemented; tenant scope names, grants, and rate limits remain unverified.  
**Size:** S  
**Priority:** P2  
**Dependencies:** WP-77, WP-85

**Files**
- `README.md`
- `docs/CONFIGURATION.md`
- `docs/SECURITY.md` if required scopes or refreshed-permission behavior change

**Change**
- Add Auth0 Management API client settings and the least-privilege scopes required by permission
  synchronization and freshness checks to the Quick Start/configuration guidance.
- Describe language preference as browser-local, not account-level.

**Acceptance**
- Quick Start, configuration reference, and runtime requirements agree and contain placeholders
  only, never real secrets.

**Tests**
- README placeholders, configuration names, links, and browser-local language behavior were checked
  against repository source; `git diff --check` passed. No build or code tests were run (docs-only).
- The Nitro CLI was unavailable, so no external scope research was attempted. Exact Auth0 scope names,
  tenant grants, and rate limits remain unverified.

**Watch out for**
- Keep provider/tenant settings and legal or operational evidence explicitly marked unverified.

## Phase view

| Phase | Focus | Packages |
|---|---|---|
| 1 | Release integrity and privacy safety | WP-81, WP-73 → WP-75 |
| 2 | Auth0 and GraphQL authorization | WP-76 → WP-78 |
| 3 | Dependency controls and runtime hardening | WP-82 → WP-85 |
| 4 | UI behavior and operator documentation | WP-79 → WP-80, WP-86 |

## Suggested execution order

1. WP-81 before WP-82, so publication/security gates are stabilized before dependency checks are added.
2. WP-73, WP-74, WP-75, then WP-76 → WP-77 → WP-78.
3. WP-82, WP-83 → WP-84, and WP-85.
4. WP-79 and WP-80; finish documentation in WP-86 after WP-77 and WP-85.

## Explicitly out of scope

- Changing live Auth0 tenant grants/scopes, GitHub rulesets/secrets, or production ingress settings.
- Making legal GDPR-compliance determinations or supplying provider/processor evidence.
- Running any cleanup, migration, or erasure operation against a production database.
- Creating commits, pushing branches, or publishing releases.
