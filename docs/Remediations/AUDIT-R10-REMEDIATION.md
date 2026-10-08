# Audit Remediation Plan — R10

> **Status at a glance —** R10-01 through R10-22 are assigned to WP-102 through WP-110.

Companion to [`AUDIT-R10.md`](Audits/AUDIT-R10.md). The audit records evidence and impact; this
tracker records the approved work packages and their verification status.

---

## R10 remediation wave

**Approval:** Approved through the repository plan gate.  
**Scope:** All R10-01 through R10-22 findings.  
**Execution:** Use GPT-6 Luna for all implementation, review, and delegated work. Do not use other
models. Execute one WP at a time and mark it implemented only after its focused checks pass.

**Approved decisions:** Invalidate permission snapshots on permission changes where feasible;
preserve the time-extension subscription as public and ID-scoped, documenting the RefTest ID as its
capability; require controller/owner confirmation before final privacy/legal-record wording; and use
a shared privacy challenge limiter across API instances.

### WP-102 — Prevent stale-owner personal-data export delivery

**Findings:** R10-01 (🟡 Medium)  
**Status:** ✅ Implemented

**Size:** M  
**Priority:** P1  
**Dependencies:** None

**Files**
- `RefTestManagement.Api/BackgroundServices/JobHandlers/PersonalDataExportDeliveryEmailJobHandler.cs`
- `RefTestManagement.Domain/RefTests/RefTest.cs`
- Relevant application/domain handler and persistence tests

**Change**
- Invalidate pending exports when a participant email changes and recheck each exported record's current ownership immediately before provider handoff.

**Acceptance**
- An email change after records are loaded cannot result in delivery of records no longer associated with the verified address; existing privacy-job and delivery behavior remains intact.

**Tests**
- Targeted personal-data export delivery and email-mutation tests; backend build.

**Watch out for**
- Coordinate with existing R9 in-flight export/erasure handling. Avoid logging participant email or other PII.

### WP-103 — Define permission revocation latency

**Findings:** R10-02 (🟡 Medium)  
**Status:** ✅ Implemented

**Size:** M  
**Priority:** P1  
**Dependencies:** None

**Files**
- `docs/SECURITY.md`
- `docs/CONFIGURATION.md`
- `RefTestManagement.UnitTests/PermissionSnapshotServiceTests.cs` and related authorization tests

**Change**
- The app has no in-application per-user grant/revoke path or Auth0 change-event callback for targeted invalidation. Document the existing four-minute revocation-latency objective and cache lifetime; preserve fail-closed refresh behavior.

**Acceptance**
- Documentation states that external permission changes are observed on the next authentication validation after snapshot expiry, bounded to four minutes per API process; expired snapshots are never reused after refresh failure.

**Tests**
- Existing four-minute boundary/revocation test and focused authorization tests; backend build.

**Watch out for**
- Do not imply immediate revocation. Revisit targeted invalidation if a permission-change event or in-application grant/revoke path is added.

### WP-104 — Expose result and interactive UI states accessibly

**Findings:** R10-03, R10-05, R10-06, R10-07, R10-08, R10-15 (🟡 Medium/⚪ Low)  
**Status:** ✅ Implemented

**Size:** M  
**Priority:** P1  
**Dependencies:** None

**Files**
- `RefTestManagement.Ui/src/app/ref-test/take/components/ref-test-results/ref-test-results.html`
- `RefTestManagement.Ui/src/app/ref-tests/create/create-ref-tests.html`
- `RefTestManagement.Ui/src/app/ref-tests/detail/ref-test-detail.html`
- `RefTestManagement.Ui/src/app/ref-tests/list/components/filters/ref-test-filters-card/ref-test-filters-card.html`
- `RefTestManagement.Ui/src/app/ref-tests/list/components/filters/sorting-panel/sorting-panel.html`
- `RefTestManagement.Ui/src/app/privacy/components/personal-data-export/request/personal-data-export-request.html`
- `RefTestManagement.Ui/src/app/ref-tests/list/components/filters/status-filter-tabs/status-filter-tabs.html`
- Corresponding specs and four locale files as needed

**Change**
- Add translated pass/fail text; associate create-form errors with inputs; expose selected tab/panel semantics appropriate to actual navigation; bind disclosure expanded state and panel relationships; announce pending export status; and expose selected status-filter state.

**Acceptance**
- Assistive technology can identify the result and listed form, route, disclosure, request, and filter states; semantics match actual component behavior.

**Tests**
- Focused Angular specs; `npm run check:i18n`; production UI build.

**Watch out for**
- Do not apply tab roles to ordinary navigation if navigation semantics better fit. Preserve all four locales and accessible dynamic updates.

### WP-105 — Validate language configuration at startup

**Findings:** R10-04 (🟡 Medium)  
**Status:** ✅ Implemented

**Size:** S  
**Priority:** P1  
**Dependencies:** None

**Files**
- `RefTestManagement.Api/Program.cs`
- Language configuration tests
- `docs/CONFIGURATION.md`

**Change**
- Validate non-empty configured language codes against the supported set at startup and fail with a clear configuration error for unsupported values.

**Acceptance**
- The API rejects unsupported configuration before serving values that leave the UI with no enabled language; supported configuration and the empty-list default remain unchanged.

**Tests**
- Focused API/configuration tests and backend build.

**Watch out for**
- Keep server-supported and UI-supported codes aligned; do not silently filter unsupported entries.

### WP-106 — Correct export copy, translated labels, and duplicate delete dispatch

**Findings:** R10-14, R10-16, R10-17, R10-18 (⚪ Low)  
**Status:** ✅ Implemented

**Size:** M  
**Priority:** P2  
**Dependencies:** None

**Files**
- `RefTestManagement.Ui/src/app/privacy/components/personal-data-export/confirmation/personal-data-export-confirmation.ts`
- `RefTestManagement.Ui/src/app/ref-tests/list/components/filters/status-filter-tabs/status-filter-tabs.html`
- `RefTestManagement.Ui/src/app/ref-tests/list/components/dialogs/delete-ref-tests-dialog/delete-ref-tests-dialog.html`
- `RefTestManagement.Ui/src/app/shared/utils/dialog-utils.ts`
- `RefTestManagement.Ui/public/i18n/{en,nl,fr,de}.json`
- Relevant component/helper specs

**Change**
- Clarify that delivery will be attempted rather than guaranteed; translate the filter-scroll accessible label; disable and guard repeated delete confirmation while an operation is in flight; restore the documented `\u` escape convention for German locale text.

**Acceptance**
- Confirmation copy does not promise delivery; accessible labels are translated; rapid repeated confirmation dispatches at most one mutation; German locale remains valid JSON with escaped non-ASCII characters.

**Tests**
- Focused Angular specs; `npm run check:i18n`; production UI build.

**Watch out for**
- Preserve all locale formatting and line endings; do not reserialize locale JSON.

### WP-107 — Reconcile subscription capability and privacy records

**Findings:** R10-09, R10-10, R10-11, R10-12 (⚪ Low)  
**Status:** ⚠️ Partial; privacy wording awaits owner/controller confirmation

**Size:** M  
**Priority:** P2  
**Dependencies:** None

**Files**
- `docs/SECURITY.md`
- `RefTestManagement.Api/Graphql/Subscriptions/RefTestSubscriptions.cs` (documentation only if needed)
- `RefTestManagement.Application/Services/IHFRulesQuestionsService.cs` (field inventory verification only if needed)
- `docs/GDPR/GDPR-ROPA.md`
- `docs/GDPR/GDPR-PROCESSOR-REGISTER.md`
- `docs/PRIVACY.md`
- `docs/GDPR/GDPR-LEGAL-BASIS-RECORD.md`

**Change**
- Document the public, ID-scoped time-extension subscription and treat its RefTest ID as its capability. Reconcile technical fields, provider lists, participant-facing wording, and legal-basis records only after controller/owner confirmation of classification, deployed providers, and legal basis.

**Acceptance**
- Documentation accurately describes the public subscription and technical data flow; provider and legal-basis statements are consistent and grounded in owner/controller-confirmed facts, without inferring a legal conclusion from code. Public subscription capability is documented; remaining privacy-record changes are blocked on owner/controller evidence.

**Tests**
- Documentation consistency/link review; focused subscription tests only if behavior-adjacent code documentation changes require them.

**Watch out for**
- Confirmation is a prerequisite to finalizing provider/legal-basis wording. No change to subscription authorization or payload is included; do not infer personal linkability.

### WP-108 — Share privacy challenge rate limits across instances

**Findings:** R10-13 (⚪ Low)  
**Status:** ⚠️ Operationally partial — shared Redis enforcement is implemented; multi-replica rollout remains blocked until Redis is configured and smoke-tested.

**Size:** L  
**Priority:** P2  
**Dependencies:** None

**Files**
- `RefTestManagement.Api/Services/PrivacyChallengeRateLimiter.cs`
- `RefTestManagement.Api/Program.cs` and privacy mutation call paths
- Relevant privacy challenge tests and package metadata
- `docs/CONFIGURATION.md` and `docs/SECURITY.md`

**Change**
- Add atomic Redis fixed-window counters for multi-instance hosting. Keep process-local enforcement as the single-instance default; require authenticated TLS Redis and HMAC configuration before scale-out, and reject the local backend in Azure Container Apps.

**Acceptance**
- Requests from the same client across API instances share quota/reset behavior; outages deny only privacy challenge processing while preserving generic request acknowledgement and confirmation denial.
- Keep operational status partial until smoke testing validates shared quotas, outage denial, and recovery across replicas.

**Tests**
- Focused shared-store/multi-instance tests; backend build; configuration/deployment documentation checks.

**Watch out for**
- Verify trusted proxy configuration and client-IP resolution before rollout. Redis must be reachable from App Service and future Container Apps before deployment; this work package does not provision it.

### WP-109 — Fix README audit report links

**Findings:** R10-19 (⚪ Low)  
**Status:** ✅ Implemented

**Size:** S  
**Priority:** P2  
**Dependencies:** None

**Files**
- `README.md`

**Change**
- Update existing audit report links to target the checked-in `docs/Audits/` directory.

**Acceptance**
- Every audit link in the README points to an existing report path.

**Tests**
- Verify each linked target exists.

**Watch out for**
- Preserve the current index structure and descriptions.

### WP-110 — Bind release checks to payload digest and toolchain

**Findings:** R10-20, R10-21, R10-22 (⚪ Low)  
**Status:** ✅ Implemented

**Size:** M  
**Priority:** P2  
**Dependencies:** None

**Files**
- `.github/workflows/beta-release.yml`
- `.github/workflows/stable-release.yml`
- `.github/workflows/codeql.yml`
- Workflow validation as available

**Change**
- Record and verify a cryptographic digest for the published application payload; explicitly install and verify npm 12.2.0 where UI dependencies are installed; align CodeQL's .NET SDK with release validation.

**Acceptance**
- Deployment validation detects payload changes independently of unchanged metadata; applicable CI jobs use the declared npm version; CodeQL and release validation use the same .NET SDK.

**Tests**
- Validate workflow YAML, triggers, job/artifact dataflow, and action pinning; run available workflow checks.

**Watch out for**
- Preserve R7 provenance, artifact, and deployment gates. Keep actions SHA-pinned with version comments and permissions least-privilege; digest the actual published payload.

## Execution order

1. WP-102 through WP-110 are independently reviewable; execute one package at a time.
2. WP-107 privacy-record reconciliation cannot be finalized until controller/owner confirmation is available.
3. Mark a WP implemented only after its acceptance criteria and focused checks pass.

## Explicitly out of scope

- Changing the public time-extension subscription's authorization or payload.
- Making legal determinations, asserting whether IHF identifiers are personal data, or claiming a provider is deployed without controller/owner confirmation.
- Remediating R9 residual dependency-audit policy findings.
- Unrelated cleanup, new infrastructure, commits, pushes, or pull requests.
