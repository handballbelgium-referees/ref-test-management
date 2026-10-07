# Audit Remediation Plan — R9

> **Status at a glance —** All 41 R9 findings are assigned to WP-87 through WP-101.

Companion to [`AUDIT-R9.md`](./AUDIT-R9.md). The audit records evidence and impact; this tracker
records the approved work packages and their verification status.

---

## R9 remediation wave

**Approval:** Approved through the repository plan gate.  
**Scope:** All R9-01 through R9-41 findings.  
**Execution:** Use GPT-6 Luna for implementation and review. Each WP is committed and pushed to the
current non-main branch only after focused checks pass and a GPT-6 Luna review reports no findings.

**Approved decisions:** Withdrawal recovery uses existing operator escalation only; retention
settings are bounded to 1–3 years and 1–90 days; High/Critical dependency advisories (including
peer dependencies) block unless there is a documented, unexpired owner-attributed exception;
release tooling is excluded from Renovate auto-merge; retained R9-22 identifiers are classified as
non-linkable and require supporting evidence to be cited; HSTS max-age is one year; production
deployment waits for main synchronization; public withdrawal matching uses an indexed normalized
email value; and the R9-41 badge commit/push must precede tag creation without weakening the
validated-SHA guarantee.

### WP-87 — Make reset and approval mutations atomic

**Findings:** R9-01 (🟡 Medium)  
**Status:** ✅ Implemented\
**Size:** M  
**Priority:** P1  
**Dependencies:** None

**Files**
- `RefTestManagement.Api/Graphql/Mutations/Reset/RefTestResetMutations.cs`
- `RefTestManagement.Api/Graphql/Mutations/Approval/RefTestApprovalMutations.cs`
- `RefTestManagement.UnitTests`
- `docs/AUDIT-R9-REMEDIATION.md`, `README.md` (initial tracker setup)

**Change**
- Create this tracker and add its README index row.
- Commit each item's domain changes and required invitation job atomically; calculate response counts from committed outcomes.

**Acceptance**
- Failed per-item work leaves no partial persisted state; approval cannot persist without its required invitation job; returned counts match database outcomes.

**Tests**
- Extend/run `RefTestAtomicMutationTests` for failure injection, persisted state, enqueue failure, and counts; run the focused xUnit v3 test class and solution build.

**Verification**
- `dotnet test --solution RefTestManagement.slnx --configuration Release --filter-class Handball.Belgium.RefTestManagement.UnitTests.RefTestAtomicMutationTests` — passed (6/6).
- `dotnet build RefTestManagement.slnx -nologo -v q -clp:ErrorsOnly` — passed (0 warnings, 0 errors).
- `git diff --check` — passed. GPT-6 Luna code review found no findings.

**Watch out for**
- R9-01 recurs after the R8 mutation fix; preserve valid successful-item semantics and transaction boundaries.

### WP-88 — Confirm email acceptance and prevent provider-body disclosure

**Findings:** R9-02, R9-08 (🟡 Medium)  
**Status:** ✅ Implemented

**Size:** M  
**Priority:** P1  
**Dependencies:** None

**Files**
- `RefTestManagement.Infrastructure/Services/EmailService.cs`
- `RefTestManagement.Infrastructure/Logging/ServiceLoggerMessages.cs`
- Invitation/result email job handlers under `RefTestManagement.Api/BackgroundServices/JobHandlers`
- Focused email service and handler tests in `RefTestManagement.UnitTests`

**Change**
- Propagate provider submission outcomes explicitly; do not mark jobs sent unless accepted.
- Remove provider response bodies from logs; retain only bounded safe status/error/correlation data.

**Acceptance**
- Rejections and submission failures remain retryable; provider response content cannot expose invitation links/tokens in logs.

**Tests**
- Cover accepted, rejected, and failed submissions; assert log templates/arguments exclude provider bodies and tokens; run focused xUnit v3 tests and solution build.

**Verification**
- `dotnet test --solution RefTestManagement.slnx --configuration Release` — passed (396/396).
- `dotnet build RefTestManagement.slnx -nologo -v q -clp:ErrorsOnly` — passed (0 warnings, 0 errors).
- `git diff --check` — passed. Final GPT-6 Luna code review found no findings after one focused fix round.

**Watch out for**
- Provider response bodies and echo behavior cannot be verified from source; do not retain raw response data for diagnostics.

### WP-89 — Harden retention and terminal privacy-job recovery

**Findings:** R9-03, R9-04, R9-05, R9-06, R9-07, R9-23 (🟡 Medium, ⚪ Low)  
**Status:** ✅ Implemented  
**Size:** L  
**Priority:** P1  
**Dependencies:** WP-88

**Files**
- `RefTestManagement.Api/Program.cs`
- `RefTestManagement.Application/Configurations/PrivacyConfiguration.cs`
- `RefTestManagement.AuditLog/AuditLogOptions.cs`
- `RefTestManagement.Api/BackgroundServices/PrivacyRetentionService.cs`
- `RefTestManagement.Infrastructure/Queries/PrivacyRetentionQueries.cs`
- `RefTestManagement.Infrastructure/Services/RefTestPrivacyErasureService.cs`
- `RefTestManagement.Api/BackgroundServices/BackgroundJobService.cs`
- Privacy/export job handlers, cleanup queries, domain job/request state, and focused unit tests
- `docs/CONFIGURATION.md`, `docs/PRIVACY.md`

**Change**
- Revalidate retention eligibility in the erasure transaction and isolate failures per candidate.
- Validate retention values at startup; recheck/cancel email work during erasure.
- Make exhausted withdrawal targets and final-attempt export jobs terminal, observable, and securely cleaned up or escalated.

**Acceptance**
- Reactivated tests are not erased from stale candidates; one failed candidate does not skip later records.
- Accepted retention values are 1–3 years and 1–90 days.
- Exhausted targets use existing operator escalation only; no new operator retry UI is added.
- Final-attempt export failures do not strand request data; handlers recheck erasure state immediately before provider submission.

**Tests**
- Extend/run `PrivacyRetentionQueriesTests`, `RefTestPrivacyErasureServiceTests`, `PrivacyWithdrawalPipelineTests`, and `BackgroundJobProcessingTests` for validation, continuation, terminal cleanup, and email/erasure interleavings; run focused xUnit v3 tests and solution build.

**Watch out for**
- WP-88 precedes this package because handler paths overlap. Provider-accepted email cannot be recalled; preserve this limit and do not claim otherwise.

### WP-90 — Fail closed for blocked accounts and preserve managed scope data

**Findings:** R9-09 (🟡 Medium), R9-30, R9-31 (⚪ Low)  
**Status:** ✅ Implemented
**Size:** L  
**Priority:** P1  
**Dependencies:** None

**Files**
- `RefTestManagement.Api/Services/PermissionSnapshotService.cs`
- `RefTestManagement.Api/SecurityStartup.cs`
- `RefTestManagement.Auth0/Services/Auth0ManagementService.cs`
- Approval notification handler and focused tests in `RefTestManagement.UnitTests`

**Change**
- Deny/invalidate access when permission refresh detects a blocked or indeterminate account.
- Bound or cache approver discovery.
- Preserve customized Auth0 scope descriptions during synchronization.

**Acceptance**
- A blocked account cannot retain refreshed permissions; approver lookup has bounded API work; synchronization changes only managed scope data.

**Tests**
- Extend/run `PermissionSnapshotServiceTests`, `Auth0ManagementServiceTests`, and `PermissionSyncServiceTests` for blocked/unknown status, bounded lookup, and description preservation; run focused xUnit v3 tests and solution build.

**Watch out for**
- Verify live Auth0 account and scope-update semantics where possible. Fail closed and do not overwrite custom scope data if update semantics are uncertain.

### WP-91 — Preserve participant test recovery and announce question changes

**Findings:** R9-10, R9-11, R9-12 (🟡 Medium), R9-32 (⚪ Low)  
**Status:** ✅ Implemented

**Size:** M  
**Priority:** P1  
**Dependencies:** None

**Files**
- `RefTestManagement.Ui/src/app/ref-test/take/state/ref-test.facade.ts`
- `RefTestManagement.Ui/src/app/ref-test/take/take-ref-test.ts` and `.html`
- Result and question-card components; leave-navigation guard
- Focused UI specs and locale entries if new text is needed

**Change**
- Preserve completed answers for retry/resume; flush pending debounced answers before confirmed navigation; keep scoring indeterminate until configuration loads; announce question changes.

**Acceptance**
- Retry reuses the same answers; leaving cannot silently discard a pending answer; unavailable threshold configuration never produces a passing result; assistive technology is informed when the active question changes.

**Tests**
- Extend/run `ref-test.facade.spec.ts`, `can-deactivate-ref-test.guard.spec.ts`, and `ref-test-results.spec.ts`; add question-announcement coverage; run focused UI specs and i18n checks if text changes.

**Watch out for**
- Preserve existing submission/idempotency semantics; do not treat configuration errors as a zero threshold.

### WP-92 — Separate create, lookup, and query error states from empty results

**Findings:** R9-13, R9-14, R9-15 (🟡 Medium), R9-33 (⚪ Low)  
**Status:** ✅ Implemented

**Size:** L  
**Priority:** P1  
**Dependencies:** None

**Files**
- Create-test component and title/question autocomplete components under `RefTestManagement.Ui/src/app/ref-tests/create`
- `RefTestManagement.Ui/src/app/ref-tests/services/ref-test-data.ts` and list components
- Audit-log and privacy-notice data/services/templates
- Ref-test empty-state component and affected `public/i18n/{en,nl,fr,de}.json`
- Focused UI specs

**Change**
- Track mutation loading reactively; distinguish loading, successful empty, and failed query/search states; add retry; permit manual fallback only after a successful no-match; localize filtered-empty state and clear-filter action.

**Acceptance**
- In-flight creation prevents duplicate submits; lookup failure never appears as no match; affected pages expose distinct loading/empty/error states and recovery; filtered lists do not show create-first wording; user-visible text exists in all four locales.

**Tests**
- Add/run focused create, autocomplete, data-service, list, audit-log, and privacy-notice specs; run `npm run check:i18n` and production UI build.

**Watch out for**
- WP-94 changes the same autocomplete components; keep this package focused on state/error behavior and execute it first.

### WP-93 — Add recovery for authentication-check failures

**Findings:** R9-16 (🟡 Medium)  
**Status:** ⏳ Pending  
**Size:** M  
**Priority:** P1  
**Dependencies:** None

**Files**
- `RefTestManagement.Ui/src/app/auth/services/auth.ts`
- `RefTestManagement.Ui/src/app/auth/guards/auth-guard.ts`
- `RefTestManagement.Ui/src/app/home/home.ts` and related templates/locales
- Focused UI specs

**Change**
- Model authentication-check failure explicitly and provide retry or sign-in recovery rather than indefinite loading or a misleading unauthenticated state.

**Acceptance**
- Transient failures are distinguishable from unauthenticated users; recovery does not require a reload loop; protected access remains fail-closed.

**Tests**
- Add/run focused auth, guard, and home specs for success, unauthenticated, failure, and recovery; run i18n check and production UI build.

**Watch out for**
- Avoid retry loops and misleading sign-in state for an already authenticated user.

### WP-94 — Complete keyboard and semantic behavior of shared controls

**Findings:** R9-17 (🟡 Medium), R9-34, R9-35 (⚪ Low)  
**Status:** ⏳ Pending  
**Size:** L  
**Priority:** P1  
**Dependencies:** WP-92

**Files**
- Datepicker/calendar/days-grid/date-time picker under `RefTestManagement.Ui/src/app/shared/components`
- Create-test schedule template; title/question autocomplete components
- `RefTestManagement.Ui/src/app/app.ts` and `app.html`
- All affected locale files and focused UI specs

**Change**
- Implement keyboard/focus date selection, labels/localized names, combobox/listbox relationships and active descendant, consistent autocomplete keyboard selection, and language-selector semantics matching its interaction model.

**Acceptance**
- Date/time, autocomplete, and language controls work with keyboard and assistive technology; labels/names are localized; focus is visible and managed.

**Tests**
- Add/run focused datepicker, time-picker, autocomplete, and language-selector interaction/accessibility specs; run i18n check and production UI build.

**Watch out for**
- Depends on WP-92 because autocomplete files overlap. Prefer semantics that match actual behavior over unsupported ARIA roles.

### WP-95 — Align privacy wording and record the confirmed data classification

**Findings:** R9-18 (🟡 Medium), R9-22 (⚪ Low)  
**Status:** ⏳ Pending  
**Size:** M  
**Priority:** P1  
**Dependencies:** WP-89

**Files**
- `RefTestManagement.Ui/public/i18n/{en,nl,fr,de}.json`
- `docs/PRIVACY.md`
- `docs/GDPR-PROCESSOR-REGISTER.md`
- Focused privacy-notice specs

**Change**
- Align localized erasure wording with retained anonymized records.
- Record the confirmed non-linkable classification and supporting owner/processor evidence.

**Acceptance**
- All locales distinguish identifier erasure from retention of anonymized records; the register accurately states the classification and cites its evidence; no retained identifier values are changed.

**Tests**
- Run focused privacy-notice spec and `npm run check:i18n`; review privacy/register consistency.

**Watch out for**
- The approved assumption is non-linkability. Pause this WP if supporting evidence cannot be cited; do not invent a basis or change data fields.

### WP-96 — Enforce high-severity audits for root and UI dependencies

**Findings:** R9-19, R9-20 (🟡 Medium)  
**Status:** ⏳ Pending  
**Size:** L  
**Priority:** P1  
**Dependencies:** None

**Files**
- `.github/workflows/pr.yml`
- `.github/workflows/beta-release.yml`
- `.github/workflows/stable-release.yml`
- `scripts/check-npm-audit.mjs` (new), an exception policy file under `.github`, and `docs/SECURITY.md`

**Change**
- Add blocking High/Critical audits for root and UI dependency trees in PR and release validation.
- Enforce exceptions with advisory identity, rationale, owner, and expiry; expired/malformed exceptions fail.

**Acceptance**
- Both trees are checked; peer advisories block by default; only explicit, unexpired, owner-attributed exceptions pass; policy is documented.

**Tests**
- Test audit-result parsing and exception validation, including peer and expired cases; validate workflow YAML and run representative root/UI audits.

**Watch out for**
- Retain full-SHA action pins, least privilege, disabled checkout credential persistence, and safe shell handling. No blanket peer/dev exemption.

### WP-97 — Exclude release tooling from dependency auto-merge

**Findings:** R9-21 (🟡 Medium)  
**Status:** ⏳ Pending  
**Size:** S  
**Priority:** P1  
**Dependencies:** None

**Files**
- `renovate.json`
- Focused Renovate configuration validation

**Change**
- Exclude release tooling packages from patch-level dev-dependency auto-merge.

**Acceptance**
- Updates to packages used by privileged release workflows require human review; unrelated auto-merge behavior remains unchanged.

**Tests**
- Validate Renovate configuration and assert each release-tooling package is excluded.

**Watch out for**
- Include release plugins and badge tooling, not only the top-level semantic-release package.

### WP-98 — Bound public withdrawal matching with a normalized indexed lookup

**Findings:** R9-24 (⚪ Low)  
**Status:** ⏳ Pending  
**Size:** L  
**Priority:** P2  
**Dependencies:** WP-89

**Files**
- `RefTestManagement.Domain/RefTests/RefTest.cs`
- `RefTestManagement.Infrastructure/Configurations/RefTestConfiguration.cs`
- `RefTestManagement.Api/Services/PrivacyWithdrawalRequestService.cs`
- `RefTestManagement.Infrastructure/Queries/PrivacyWithdrawalQueries.cs`
- `RefTestManagement.Infrastructure/Services/RefTestPrivacyErasureService.cs`
- Same-named migrations for `RefTestManagement.Migrations.SqlServer`, `.PostgreSQL`, `.SQLite`, and `.MySQL`
- Focused provider/query tests

**Change**
- Persist and index a normalized-email lookup value, backfill existing records, query it for bounded database-side matching, and clear/redact it during erasure.

**Acceptance**
- Matching no longer materializes every eligible record in application memory; trim/case-insensitive behavior is preserved; anonymized records never match; all providers have equivalent schema/backfill; erasure removes the derived value.

**Tests**
- Extend `PrivacyWithdrawalPipelineTests`; validate migrations/model snapshots and generated query behavior for all providers; run focused xUnit v3 tests and solution build.

**Watch out for**
- This adds another personal-data-derived value; preserve normalization consistency and all-provider migration parity.

### WP-99 — Harden security headers and correct API/security documentation

**Findings:** R9-25 through R9-29 (⚪ Low)  
**Status:** ⏳ Pending  
**Size:** M  
**Priority:** P2  
**Dependencies:** WP-96

**Files**
- `RefTestManagement.Api/Program.cs`
- `RefTestManagement.Api/SecurityStartup.cs`
- `RefTestManagement.Api/Controllers/AccountController.cs`
- `README.md`
- `docs/SECURITY.md`, `docs/CONFIGURATION.md`
- Focused API/header tests

**Change**
- Clarify append-oriented audit-log behavior and privacy redaction; tighten CSP with explicit sources and restrictive object/frame directives; use one-year HSTS; distinguish OIDC client ID from API audience; document permissions-endpoint 503/recovery; document WP-96 dependency exceptions.

**Acceptance**
- CSP is restrictive without unsupported inline allowances; HSTS is one year; settings and 503/recovery docs match behavior; README accurately describes audit-log redaction.

**Tests**
- Add/run focused response-header and controller tests; run solution build and documentation/code review.

**Watch out for**
- Do not add `includeSubDomains`; preserve styling while tightening CSP, using nonce/hash support if required.

### WP-100 — Align release ordering, fork handling, provenance, and badge publication

**Findings:** R9-36, R9-38, R9-39, R9-41 (⚪ Low)  
**Status:** ⏳ Pending  
**Size:** L  
**Priority:** P2  
**Dependencies:** WP-96, WP-99

**Files**
- `.github/workflows/stable-release.yml`
- `.github/workflows/beta-release.yml`
- `.github/workflows/pr.yml`
- `.releaserc.mjs`, `scripts/generate-badges.mjs`, and release documentation in `README.md`

**Change**
- Make production deployment wait for main synchronization; correct fork checkout behavior; record exact toolchain/runner provenance; move badge commit/push before tag/publication.

**Acceptance**
- Production deploy depends on successful sync; fork PRs use their actual head ref or skip write-oriented sync; provenance records exact toolchain/runner information; badge push failure prevents tag/publication; successful release tags point exactly to the validated candidate SHA.

**Tests**
- Validate workflow YAML, triggers, permissions, and dependencies; test fork/ref handling and simulated badge failure without production credentials; assert no tag on failure and validated-SHA identity on success.

**Watch out for**
- Badge generation already runs during semantic-release prepare, but branch push follows tag creation. Redesign must move branch update pre-publication without weakening validated-SHA provenance; stop for re-planning if unsafe.

### WP-101 — Correct code-owner coverage and release documentation claims

**Findings:** R9-37, R9-40 (⚪ Low)  
**Status:** ⏳ Pending  
**Size:** S  
**Priority:** P2  
**Dependencies:** WP-100

**Files**
- `.github/CODEOWNERS`
- `.releaserc.mjs`
- `README.md`
- Focused CODEOWNERS/release-config validation

**Change**
- Correct root/default and active `.releaserc.mjs` ownership patterns; clarify that main pushes trigger the workflow but semantic-release rules determine publication.

**Acceptance**
- Ownership patterns cover the intended paths; README describes workflow trigger and publication behavior accurately.

**Tests**
- Run supported CODEOWNERS and release-config checks; review README against `.releaserc.mjs`.

**Watch out for**
- Required code-owner review depends on GitHub repository settings; record external confirmation as an operational prerequisite.

## Execution order

1. WP-87, WP-88, WP-90, WP-91, WP-92, WP-93, WP-96, and WP-97.
2. WP-89 after WP-88; WP-94 after WP-92; WP-95 and WP-98 after WP-89.
3. WP-99 after WP-96; WP-100 after WP-96 and WP-99; WP-101 after WP-100.

After each WP's focused checks pass, update only its status, obtain a GPT-6 Luna review with no
findings, then commit and push that WP to the current non-main branch before starting the next one.
Use an allowed Conventional Commit type/scope, include the WP number in the subject, keep the
header within 100 characters, and include the Copilot App co-author trailer.

## Explicitly out of scope

- Broad security/accessibility cleanup beyond R9 findings; a new operator-triggered withdrawal retry UI.
- Removing retained identifiers under R9-22; the approved classification is non-linkable, subject to citing its evidence.
- HSTS `includeSubDomains`, production endpoint changes, or automatic exemptions based only on peer/dev dependency type.
- Any weakening of authorization, privacy/logging safeguards, pinned-action rules, release provenance, or validated-SHA checks.
- Merging, pull requests, or pushes to `main`.
