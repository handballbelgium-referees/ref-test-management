# Audit Remediation Plan - R12

> **Status at a glance -** WP-114 through WP-130 are implemented.

Companion to [`AUDIT-R12.md`](../Audits/AUDIT-R12.md). The audit records evidence and impact; this
tracker records the approved repository remediation packages and their verification status.

---

## R12 remediation wave

**Approval:** Approved through the repository plan gate.\
**Scope:** All 19 repository-verifiable R12 findings. Operational/legal evidence and live configuration
checks remain out of scope.\
**Execution:** Execute one WP at a time and mark it implemented only after its acceptance criteria and
focused checks pass. The session plan's WP-01 through WP-17 map in order to this tracker's WP-114
through WP-130.\
**Model:** GPT-6 Luna only for the main session and all implementation/review agents.

### WP-114 - Retain and review failed withdrawal targets

**Finding:** R12-01 (Medium)\
**Status:** ✅ Implemented

**Size:** L\
**Priority:** P1\
**Dependencies:** None

**Files**
- `RefTestManagement.Api/BackgroundServices/PrivacyWithdrawalCleanupService.cs`
- `RefTestManagement.Domain/Privacy/PrivacyWithdrawalBatchTarget.cs`
- `RefTestManagement.Api/Services/PrivacyWithdrawalRequestService.cs`
- Relevant GraphQL query/mutation files under `RefTestManagement.Api/Graphql/`
- `RefTestManagement.Security/Permissions.cs`
- `RefTestManagement.Ui/src/app/auth/models/permissions.ts`
- Relevant GraphQL documents under `RefTestManagement.Ui/graphql/`
- Review route/component under `RefTestManagement.Ui/src/app/privacy/`
- `RefTestManagement.Ui/public/i18n/{en,nl,fr,de}.json`
- Relevant audit-event handling under `RefTestManagement.AuditLog/`
- `docs/SECURITY.md`, `docs/PRIVACY.md`, and focused backend/UI tests

**Change**
- Retain the minimal failed-target reference and sanitized failure category after terminal failure.
- Add a protected query and acknowledgement operation plus an operator UI guarded by a dedicated
  privacy-operations permission.
- Resolve the target only after a successful retry or explicit authorized acknowledgement; audit the
  acknowledgement before purging the reference.

**Acceptance**
- Only users with the new permission can view or acknowledge failed targets; the view exposes only the
  minimal reference/category.
- Failed targets remain recoverable until successful retry or acknowledgement. Acknowledged purge and
  its typed audit event are atomic and contain no participant PII.

**Tests**
- Focused privacy-withdrawal tests for retention, retry, acknowledgement, authorization, audit event, and
  purge.
- UI route/permission and component tests; `npm run check:i18n`; affected UI build and GraphQL codegen
  with the API available if required.

**Watch out for**
- Add the permission to the backend permission inventory and UI permission model, apply
  `[Authorize(Policy = ...)]`, and document it in `docs/SECURITY.md`.
- Do not display participant names/emails or log target identifiers. If persisted review state requires
  an EF model change, add the same-named migration for SQL Server, PostgreSQL, SQLite, and MySQL.

### WP-115 - Normalize export email matching

**Finding:** R12-02 (Medium)\
**Status:** ✅ Implemented

**Size:** M\
**Priority:** P1\
**Dependencies:** None

**Files**
- `RefTestManagement.Api/Services/PersonalDataExportRequestService.cs`
- `RefTestManagement.Api/BackgroundServices/JobHandlers/PersonalDataExportDeliveryEmailJobHandler.cs`
- `RefTestManagement.Api/Graphql/Mutations/Update/RefTestUpdateMutations.cs`
- `RefTestManagement.Domain/RefTests/RefTest.cs`
- Related export-request and delivery tests

**Change**
- Apply the same whitespace-only email normalization at writes, export request matching, and delivery
  lookup, including legacy stored values with surrounding whitespace.

**Acceptance**
- A whitespace-padded stored address can complete the existing self-service export flow when the
  requester submits its trimmed form; established case-matching behavior is unchanged.

**Tests**
- Add a round-trip test for write, request match, and delivery lookup plus a legacy stored-whitespace
  case; run focused export tests.

**Watch out for**
- Preserve the existing verification and recipient boundaries. Ensure lookup compatibility with all
  supported database providers and do not introduce unrequested case folding.

### WP-116 - Export readable assessment progress and answers

**Finding:** R12-03 (Medium)\
**Status:** ✅ Implemented — saved position included; historical wording limitation disclosed

**Size:** M\
**Priority:** P1\
**Dependencies:** None

**Files**
- `RefTestManagement.Application/Models/PersonalDataExportPayloads.cs`
- `RefTestManagement.Api/BackgroundServices/JobHandlers/PersonalDataExportDeliveryEmailJobHandler.cs`
- `RefTestManagement.Infrastructure/Services/PersonalDataExportPdfService.cs`
- `RefTestManagement.Infrastructure/Services/TranslationService.cs`
- Export request/delivery tests and `docs/PRIVACY.md`

**Change**
- Include the saved question position in the participant self-service PDF when available, without
  representing it as a count of answered questions or exposing opaque IDs.
- Add a localized PDF notice and align `docs/PRIVACY.md` to explain that readable historical question
  and selected-answer text cannot be reproduced because the saved RefTest has no IHF content-version
  reference.
- Do not call the current unversioned IHF endpoint, substitute current wording, or silently omit the
  limitation. Treat it as a technical export limitation, not a legal determination.

**Acceptance**
- The export includes an accurate saved position when available and a visible, localized explanation
  that historical question/selected-answer text is unavailable; internal IDs and substituted current
  wording are absent.
- The privacy notice matches the PDF, and participant verification and recipient boundaries remain
  unchanged.

**Tests**
- Verify saved-position mapping, review-step bounds, all supported PDF translations, and focused export
  delivery behavior.

**Watch out for**
- Do not add a historical content lookup, new snapshots, or a schema migration. The export model must not
  contain raw question or answer IDs.

### WP-117 - Sanitize expiration-job failure logs

**Finding:** R12-04 (Medium)\
**Status:** ✅ Implemented

**Size:** M\
**Priority:** P1\
**Dependencies:** None

**Files**
- `RefTestManagement.Api/BackgroundServices/JobHandlers/RefTestExpirationJobHandler.cs`
- `RefTestManagement.Infrastructure/Logging/ServiceLoggerMessages.cs`
- Focused background-job tests

**Change**
- Replace provider-controlled exception logging with a fixed failure category and safe operational
  reference while preserving retry and job-failure behavior.

**Acceptance**
- Provider-controlled exception text and exception objects do not reach this logger; retry/error
  behavior remains unchanged.

**Tests**
- Inject an exception containing marker text and assert it is absent from captured logs while the job
  still fails/retries as before.

**Watch out for**
- Preserve useful diagnostics through a fixed category; never log participant PII or unredacted
  provider exception data.

### WP-118 - Audit no-work withdrawal confirmations

**Finding:** R12-10 (Low)\
**Status:** ✅ Implemented

**Size:** S\
**Priority:** P2\
**Dependencies:** None

**Files**
- `RefTestManagement.Domain/Privacy/PrivacyWithdrawalChallenge.cs`
- `RefTestManagement.Api/Services/PrivacyWithdrawalRequestService.cs`
- Relevant audit-event handling under `RefTestManagement.AuditLog/`
- Focused privacy-withdrawal tests

**Change**
- Emit a minimal typed domain/audit event when a verified confirmation succeeds without queuing new
  withdrawal work.

**Acceptance**
- The confirmation is represented in the typed audit trail without participant PII.

**Tests**
- Verify event creation and persistence on the no-work path and ensure unrelated challenge state
  changes retain existing behavior.

**Watch out for**
- Keep event data minimal and consistent with audit-log privacy rules.

### WP-119 - Make expiration enqueue idempotent

**Finding:** R12-11 (Low)\
**Status:** ✅ Implemented

**Size:** M\
**Priority:** P2\
**Dependencies:** None

**Files**
- `RefTestManagement.Api/BackgroundServices/RefTestExpirationService.cs`
- `RefTestManagement.Api/Graphql/Queries/RefTestQueries.cs`
- `RefTestManagement.Infrastructure/Services/JobEnqueueService.cs`
- Job entity/configuration only if required
- Focused expiration/enqueue tests

**Change**
- Make enqueue atomic/idempotent per RefTest and expiration action across the sweep and other enqueue
  caller, so only one active matching job can exist.

**Acceptance**
- Repeated and concurrent sweeps/requests do not create duplicate active expiration jobs; completed and
  failed job behavior is unchanged.

**Tests**
- Cover repeat calls, concurrent enqueue attempts, and both callers using the same idempotency boundary.

**Watch out for**
- Avoid a check-then-insert race. If a persistence key/index is needed, add matching migrations for
  SQL Server, PostgreSQL, SQLite, and MySQL.

### WP-120 - Restore touch-device zoom

**Finding:** R12-05 (Medium)\
**Status:** ✅ Implemented

**Size:** S\
**Priority:** P1\
**Dependencies:** None

**Files**
- `RefTestManagement.Ui/src/main.ts`
- Related UI test

**Change**
- Remove restrictive viewport settings that disable user zoom.

**Acceptance**
- Viewport metadata permits user magnification without fixed-scale restrictions.

**Tests**
- Add/update a focused metadata assertion; run UI tests and the production build.

**Watch out for**
- Preserve responsive viewport width behavior.

### WP-121 - Meet normal-text contrast

**Finding:** R12-06 (Medium) and R12-16 (Low)\
**Status:** ✅ Implemented

**Size:** M\
**Priority:** P1\
**Dependencies:** None

**Files**
- `RefTestManagement.Ui/src/app/home/home.html`
- `RefTestManagement.Ui/src/app/ref-tests/detail/ref-test-detail.html`
- `RefTestManagement.Ui/src/app/ref-test/take/components/ref-test-results/ref-test-results.html`
- `RefTestManagement.Ui/src/styles.css`
- Relevant UI tests

**Change**
- Adjust the home-page description and secondary neutral text/color tokens for the cited detail/results
  surfaces.

**Acceptance**
- Each affected normal-text/background pair measures at least 4.5:1 in the rendered theme.

**Tests**
- Add/extend a measurable contrast assertion where supported; verify exact rendered color/background
  pairs and run affected UI tests and production build.

**Watch out for**
- Check actual component backgrounds and shared-token consumers; do not leave another cited surface
  below threshold.

### WP-122 - Associate creation-form errors and labels

**Finding:** R12-07 (Medium) and R12-17 (Low)\
**Status:** ✅ Implemented

**Size:** M\
**Priority:** P1\
**Dependencies:** None

**Files**
- `RefTestManagement.Ui/src/app/ref-tests/create/components/ref-test-user-list-item/ref-test-user-list-item.html`
- `RefTestManagement.Ui/src/app/ref-tests/create/create-ref-tests.html`
- Related component specs and existing locale keys

**Change**
- Associate each validation message with its input and expose invalid state programmatically; use the
  canonical existing translation key for the checkbox accessible label.

**Acceptance**
- Invalid controls reference their own visible errors and expose invalid state; the checkbox accessible
  label resolves to the same translated text as its visible label.

**Tests**
- Add/extend template accessibility and translation-key specs; run UI tests and `npm run check:i18n`.

**Watch out for**
- Preserve visible validation behavior and avoid duplicate user-facing strings.

### WP-123 - Announce loading states

**Finding:** R12-18 (Low)\
**Status:** ✅ Implemented

**Size:** S\
**Priority:** P2\
**Dependencies:** None

**Files**
- `RefTestManagement.Ui/src/app/home/home.html`
- `RefTestManagement.Ui/src/app/ref-test/take/take-ref-test.html`
- Related UI specs

**Change**
- Expose the existing loading indicators through appropriate status/live-region semantics.

**Acceptance**
- Assistive technology can detect and announce both loading states without duplicate or noisy
  announcements.

**Tests**
- Assert status/live-region semantics in focused UI specs; run UI tests, `npm run check:i18n`, and the
  production build.

**Watch out for**
- Reuse existing localized loading text.

### WP-124 - Block unaccepted High npm advisories

**Finding:** R12-08 (Medium)\
**Status:** ✅ Implemented

**Size:** L\
**Priority:** P1\
**Dependencies:** None

**Files**
- `scripts/check-npm-audit.mjs`
- `scripts/check-npm-audit.test.mjs`
- Root and UI package manifests/lockfiles
- A narrowly scoped npm-audit exception manifest if needed
- `.github/workflows/pr.yml`
- `.github/workflows/beta-release.yml`
- `.github/workflows/stable-release.yml`

**Change**
- Make unaccepted High advisories fail the shared required check while keeping Critical advisories
  blocking and Moderate behavior unchanged.
- Address current root/UI advisories by upgrading parent dependencies first; use a compatible npm
  override only when verified by tests; add an advisory-specific reasoned exception with expiry only as
  the final fallback.

**Acceptance**
- Every unaccepted High and every Critical fails the check. Valid unexpired exceptions are narrowly
  scoped; malformed/expired exceptions fail. Current root/UI High advisories are fixed or have explicit
  expiring exceptions, with no blanket suppression.

**Tests**
- Extend `node --test scripts/check-npm-audit.test.mjs` for High, Critical, valid exception, and expired
  exception cases; run root/UI audit checks, UI tests, i18n check, and production build after dependency
  changes.

**Watch out for**
- Required CI/deployment checks may fail until dependencies are fixed or waived. Re-evaluate current
  advisories at implementation; do not upgrade unrelated packages or claim live branch-protection/
  deployment behavior was verified.

### WP-125 - Require review for action digest updates

**Finding:** R12-09 (Medium)\
**Status:** ✅ Implemented

**Size:** S\
**Priority:** P1\
**Dependencies:** None

**Files**
- `renovate.json`
- `scripts/check-renovate-automerge.test.mjs`

**Change**
- Disable automatic merging for GitHub Action digest updates so human review is required; preserve
  unrelated Renovate automation.

**Acceptance**
- Configuration and regression test show that an action digest update cannot auto-merge.

**Tests**
- Extend the Renovate configuration test and validate the JSON/configuration.

**Watch out for**
- This repository change does not establish live branch protection, environment approval, or OIDC
  settings.

### WP-126 - Use a canonical logout origin

**Finding:** R12-14 (Low)\
**Status:** ✅ Implemented

**Size:** M\
**Priority:** P2\
**Dependencies:** None

**Files**
- `RefTestManagement.Api/Controllers/AccountController.cs`
- `RefTestManagement.Api/SecurityStartup.cs`
- Application configuration/options and `appsettings.json`
- `docs/CONFIGURATION.md`
- Focused controller/authentication tests

**Change**
- Add and validate one configured canonical public origin and build logout return targets from it,
  never from `Request.Host`.

**Acceptance**
- A forged/untrusted Host cannot influence the post-logout destination; missing or malformed origin
  configuration fails safely.

**Tests**
- Cover valid, missing, and malformed settings and a forged Host; run focused API/auth tests.

**Watch out for**
- Deployments must supply the real canonical origin. Do not add an actual production hostname or infer
  Auth0 tenant allowlists/proxy behavior.

### WP-127 - Align permission setup guidance

**Finding:** R12-15 (Low)\
**Status:** ✅ Implemented

**Size:** S\
**Priority:** P2\
**Dependencies:** None

**Files**
- `README.md`
- `docs/SECURITY.md`
- Verify against `RefTestManagement.Security/Permissions.cs` and
  `RefTestManagement.Api/BackgroundServices/PermissionSyncService.cs`

**Change**
- Make setup documentation match the exact wildcard constants, individual permissions, and startup
  synchronization behavior.

**Acceptance**
- README and security guide describe the same permission values and provisioning steps as the source;
  permission behavior is unchanged.

**Tests**
- Compare documentation to source constants and synchronization behavior.

**Watch out for**
- Do not claim the live Auth0 permission set was inspected.

### WP-128 - Correct the hosted-service diagram

**Finding:** R12-12 (Low)\
**Status:** ✅ Implemented

**Size:** S\
**Priority:** P2\
**Dependencies:** None

**Files**
- `docs/ARCHITECTURE-DIAGRAM.md`
- Verify against `RefTestManagement.Api/Program.cs`

**Change**
- Correct the service count/list, include export-request and withdrawal cleanup, and mark audit cleanup
  as conditional.

**Acceptance**
- The diagram distinguishes the six unconditional hosted services from conditional audit cleanup and
  matches registrations in `Program.cs`.

**Tests**
- Compare the documentation inventory against the service registrations.

**Watch out for**
- Preserve unrelated architecture details.

### WP-129 - Align IHF data-flow certainty

**Finding:** R12-13 (Low)\
**Status:** ✅ Implemented

**Size:** S\
**Priority:** P2\
**Dependencies:** WP-116

**Files**
- `docs/GDPR/GDPR-ROPA.md`
- `docs/GDPR/GDPR-PROCESSOR-REGISTER.md`
- Verify against `RefTestManagement.Application/Services/IHFRulesQuestionsService.cs` and any
  implemented WP-116 export lookup

**Change**
- Describe the technical question/selected-answer ID flow to the scoring API consistently, include any
  WP-116 lookup actually implemented (none), and keep personal-data/processor classification pending
  for controller determination.

**Acceptance**
- GDPR records agree on technical payloads and express classification uncertainty consistently without
  a legal determination.

**Tests**
- Compare documentation to the implemented service calls.

**Watch out for**
- Do not state whether the IHF identifiers legally constitute personal data.

### WP-130 - Clarify privacy-notice language availability

**Finding:** R12-19 (Low)\
**Status:** ✅ Implemented

**Size:** S\
**Priority:** P2\
**Dependencies:** None

**Files**
- `docs/PRIVACY.md`
- Relevant language-configuration statement in `README.md`

**Change**
- Describe the four privacy-notice languages as defaults and qualify availability by deployment
  language configuration.

**Acceptance**
- Privacy documentation no longer promises four enabled languages in every deployment and agrees with
  README configuration guidance.

**Tests**
- Compare the documentation against language configuration documentation/source.

**Watch out for**
- Do not change the set of supported/default languages.

## Execution order

1. WP-114, WP-115, WP-116, WP-117
2. WP-124, WP-125, WP-126
3. WP-120, WP-121, WP-122, WP-123
4. WP-118, WP-119
5. WP-127, WP-128, WP-129, WP-130
6. WP-129 follows WP-116 so data-flow documentation reflects any lookup actually delivered. Execute
   one package at a time and update status only after focused checks pass.

## Explicitly out of scope

- Live Azure, Auth0, GitHub branch-protection, environment-approval, proxy, or production configuration
  checks.
- Controller/legal determinations, processor terms, GDPR compliance conclusions, and outstanding
  operational evidence.
- Unrelated earlier-round findings, unrelated dependency upgrades, blanket npm-audit suppressions,
  commits, pushes, and pull requests.
