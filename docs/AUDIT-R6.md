# Deep Audit — Round 6

| | |
|---|---|
| **Repository** | `handballbelgium-referees/ref-test-management` |
| **Date** | 2026-09-22 |
| **Commit audited** | `db58707` (`main`) |
| **Previous reports** | [`AUDIT.md`](./AUDIT.md) (R1) · [`AUDIT-R2.md`](./AUDIT-R2.md) · [`AUDIT-R3.md`](./AUDIT-R3.md) · [`AUDIT-R4.md`](./AUDIT-R4.md) · [`AUDIT-R5.md`](./AUDIT-R5.md) |
| **Remediation tracker** | [`AUDIT-REMEDIATION.md`](./AUDIT-REMEDIATION.md) |
| **Scope** | Full-stack adversarial review: GraphQL authorization, backend job/transaction boundaries, GDPR controls and notice parity, frontend correctness, accessibility, CI/CD and test coverage |
| **Method** | Read-only audit of application code/config + reproducible validation runs. Only audit docs were modified. |

---

## Executive summary

**Production readiness: NOT READY for final sign-off — no Critical/High exploit was found, but four medium/low integrity and transparency gaps still need remediation before calling this release fully hardened.**

The codebase is materially stronger than earlier rounds. The R5 remediation landed in code: withdrawal copy is now aligned with anonymization, batch mutation error responses are normalized, report-job payloads carry `RefTestId`, and release workflows no longer fall back to `GH_PAT`. Build and test health is strong (0 build warnings/errors, full backend/frontend suites passing).

What remains is mostly “drift risk” and release integrity risk, not obvious crash bugs. The largest gap is privacy-notice source-of-truth split: consent version enforcement is configuration-driven on the backend, while the public `/privacy` page is static locale text. That can silently diverge. In addition, email mutations still log raw exceptions without the redaction pattern used elsewhere, and the stable release workflow force-rebases and force-pushes `main`, which is a governance/supply-chain footgun if branch protections are expected to be immutable.

GDPR-wise: the repository shows meaningful technical controls (anonymization, retention sweep, queued-job cancellation, audit redaction), but controller evidence (processor/DPA records, transfer safeguards, backup erasure operations, DSAR operations) remains out-of-repo and therefore not verifiable from source alone.

### Verdict by area

| Area | Verdict |
|---|---|
| Domain model & business logic | Good overall; expiration/retention and job-state handling are explicit and tested |
| GraphQL authorization | Stronger than R3; no current anonymous `node(id:)` disclosure path found in production config |
| Background jobs & transactions | Good transaction intent and scoping; remaining issue is redaction consistency in sibling mutation logs |
| GDPR / privacy | Technically improved, but privacy-notice publication can drift from the enforced notice version |
| Frontend correctness | Stable in tested paths; production build and participant flow tests pass |
| Accessibility | Dialog infrastructure appears consistent (`appDialog` usage is broad), no new blocker found in source review |
| CI/CD | Action pinning and App token usage are solid; `main` force-push release step is still risky |
| Test coverage | Good baseline, but missing regression guard for object-identification config drift |

| Severity | Count |
|---|---:|
| 🔴 Critical | 0 |
| 🟠 High | 0 |
| 🟡 Medium | 3 |
| ⚪ Low | 1 |

## Findings

| # | Severity | Area | Finding |
|---|---|---|---|
| R6-01 | 🟡 Medium | GDPR / transparency | Public privacy notice content is static i18n text while consent-version enforcement is backend-config driven; these can diverge silently |
| R6-02 | 🟡 Medium | Security / privacy logging | Staff email mutations still log raw exceptions without email/PII redaction helpers |
| R6-03 | 🟡 Medium | CI/CD integrity | Stable release workflow rewrites `main` via rebase + `--force-with-lease` |
| R6-04 | ⚪ Low | Test coverage / auth regression safety | No regression test asserts global object identification stays disabled despite node resolvers remaining on types |

---

## Medium severity

### R6-01 — Privacy notice publication and consent-version enforcement are split sources of truth

**Severity:** 🟡 Medium  
**Area:** GDPR / transparency  
**Evidence:** `RefTestManagement.Api/Graphql/Queries/RefTestQueries.cs:26-33`, `RefTestManagement.Api/appsettings.json:39-45`, `RefTestManagement.Ui/src/app/ref-test/welcome/ref-test-welcome.ts:116-129`, `RefTestManagement.Ui/src/app/privacy/privacy-notice.ts:1-12`, `RefTestManagement.Ui/public/i18n/en.json:678-682` (and locale equivalents in `nl/fr/de`)

The backend exposes a structured privacy notice contract (`PrivacyNoticeDto`) from configuration and enforces consent against the current configured `noticeVersion`. The participant start flow fetches that version and submits it. However, the public `/privacy` page is not driven by this backend contract; it renders static locale strings containing hard-coded controller/version/effective-date text.

This creates silent drift risk: configuration can change the legally enforced notice version while the publicly displayed notice remains stale in one or more locales.

**Impact:** Participants can accept version `X` enforced server-side while reading outdated version/copy on `/privacy`, weakening Art. 12–14 transparency and auditability of “what was shown when consent was collected.”

**Recommendation:** Make `/privacy` consume the backend `privacyNotice` payload as the source of truth (version, effective date, controller contact, retention years), then layer translated explanatory text around those dynamic values. Add an automated parity/regression test that fails if published notice metadata diverges from backend configuration.

### R6-02 — Email mutations log raw exceptions without redaction

**Severity:** 🟡 Medium  
**Area:** Security / privacy logging  
**Evidence:** `RefTestManagement.Api/Graphql/Mutations/Email/RefTestEmailMutations.cs:79-82`, `RefTestManagement.Api/Graphql/Mutations/Email/RefTestEmailMutations.cs:163-166`, `RefTestManagement.Api/Graphql/Mutations/Email/RefTestEmailMutations.cs:265-268`, contrasted with redaction usage in `RefTestManagement.Api/Graphql/Mutations/Shared/MutationErrorHandling.cs:30-36`

The invitation/result/report mutations correctly return generic user-facing errors, but their catch blocks still call `logger.LogError(exception, ...)` directly. Other mutation paths already use `MutationErrorHandling.LogMutationFailure` with `LogRedaction.MaskEmails(...)` before logging.

So response disclosure is mitigated, but server logs in these paths can still persist raw exception text (provider response fragments, addresses, and other contextual data).

**Impact:** Redaction policy is inconsistent across adjacent mutation families. Sensitive data can leak into logs for operators and downstream sinks where retention/export controls differ from primary database retention.

**Recommendation:** Align these three catch paths with the shared redaction helper (or equivalent centralized logging wrapper), preserving diagnostics via correlation IDs while avoiding raw exception payload persistence.

### R6-03 — Stable release sync rewrites `main` history

**Severity:** 🟡 Medium  
**Area:** CI/CD integrity  
**Evidence:** `.github/workflows/stable-release.yml:252-257`

The stable workflow step “Sync release tag to main” runs:
- `git checkout main`
- `git rebase origin/release`
- `git push --force-with-lease origin main`

This rewrites `main` in automation rather than merging forward. Even with `--force-with-lease`, it is still a history-rewrite operation on the primary branch.

**Impact:** Accidental or unexpected branch divergence can be masked by forced history rewrite, weakening repository integrity controls, auditability, and branch-protection expectations.

**Recommendation:** Replace force-rebase sync with a non-rewriting promotion strategy (fast-forward only merge or explicit merge commit) and enforce protection so workflow automation cannot rewrite protected branch history.

---

## Low severity

### R6-04 — Missing regression guard for global object-identification drift

**Severity:** ⚪ Low  
**Area:** Test coverage / authorization safety  
**Evidence:** `RefTestManagement.Api/Program.cs:237`, `RefTestManagement.Api/Graphql/Types/RefTestType.cs:21-24`, `RefTestManagement.UnitTests/AuthorizationSchemaTests.cs:62-75`

Production currently disables global object identification (`AddGlobalObjectIdentification(false)`), which is the key control that keeps the old anonymous `node(id:)` exposure closed. But `RefTestType` still configures a node resolver, and existing schema tests do not assert production-level global object-identification settings.

This is not an active exposure today; it is a regression-detection gap.

**Impact:** A future config change re-enabling global object identification could re-open a high-impact authorization path without a failing test signal.

**Recommendation:** Add an integration-level schema/auth test that boots the production GraphQL configuration and asserts anonymous `node(id:)` access is unavailable (or unauthorized) for `RefTest`.

---

## Verification of prior audit claims

### Re-check of R4/R5-era packages (WP-52 → WP-60)

| Package | Claimed status | R6 verification |
|---|---|---|
| WP-52 | Closed in R4 wave | **Closed.** Withdrawal text now consistently describes anonymization/redaction, not hard deletion (`RefTestManagement.Ui/public/i18n/en.json:616-665` and locale parity check passed) |
| WP-53 | Closed | **Closed.** OIDC no longer requests `offline_access` and does not persist tokens (`RefTestManagement.Api/SecurityStartup.cs:43-47`) |
| WP-54 | Closed | **Closed.** Report mutation returns stable generic error text (`RefTestManagement.Api/Graphql/Mutations/Email/RefTestEmailMutations.cs:269-274`) |
| WP-55 | Closed in code, deployment evidence pending | **Holds closed in code.** Forwarded headers now process `XForwardedFor` with proxy/network controls (`RefTestManagement.Api/Program.cs:260-290`) |
| WP-56 | Closed | **Closed.** Unit-test SQL helper now uses allowlisted column names + parameterized values (`RefTestManagement.UnitTests/PrivacyRetentionQueriesTests.cs:72-84`) |
| WP-57 | Closed | **Closed in code.** Locale copy aligns with anonymization lifecycle (`RefTestManagement.Ui/public/i18n/en.json:616-665`) |
| WP-58 | Closed | **Closed for response disclosure path.** Shared safe-message handling is applied in approval/creation/reset (`RefTestManagement.Api/Graphql/Mutations/Approval/RefTestApprovalMutations.cs:64-68`, `.../Creation/RefTestCreationMutations.cs:263-267`, `.../Reset/RefTestResetMutations.cs:129-133`) |
| WP-59 | Closed | **Closed.** Report payload rows now carry `RefTestId` and handler quarantines legacy payloads (`RefTestManagement.Application/Models/JobPayloads.cs:51-66`, `RefTestManagement.Api/BackgroundServices/JobHandlers/ReportEmailJobHandler.cs:19-23`) |
| WP-60 | Closed in code, operational verification pending | **Closed in code.** Release workflows require App credentials and use only minted installation tokens (`.github/workflows/beta-release.yml:24-51`, `.github/workflows/stable-release.yml:31-55,82-108`) |

### Re-check of R3-era reopened packages (WP-15, WP-20, WP-35)

| Package | R3 status | R6 verification |
|---|---|---|
| WP-15 | Reopened in R3 | **Holds closed.** Deferred enqueue paths pass caller unit-of-work context and save once (`RefTestManagement.Api/Graphql/Mutations/Creation/RefTestCreationMutations.cs:258-260,299-301`; `RefTestManagement.Infrastructure/Services/JobEnqueueService.cs:81-83`) |
| WP-20 | Reopened in R3 | **Holds closed.** Dialogs use shared `appDialog` directive and focus-trap/escape/restore behavior (`RefTestManagement.Ui/src/app/shared/components/dialog/dialog.ts:29-90`; dialog templates include `appDialog`) |
| WP-35 | Reopened in R3 | **Holds closed.** In-app privacy notice now includes internal recipient categories (admins/approvers) (`RefTestManagement.Ui/public/i18n/en.json:695-699`) |

No previously closed package above needed to be re-opened by this round. The tracker should, however, add a new R6 remediation wave for the four findings in this report.

---

## Validation performed

| Check | Result |
|---|---|
| `dotnet restore RefTestManagement.slnx` | ✅ Passed |
| `dotnet build RefTestManagement.slnx --configuration Release --no-restore` | ✅ Passed, 0 warnings, 0 errors |
| `dotnet test RefTestManagement.slnx --configuration Release --no-build` | ❌ 0 tests (MTP invocation mismatch) |
| `dotnet test --solution RefTestManagement.slnx --configuration Release --no-build -p:TestingPlatformShowTestsFailure=true` | ✅ 177 passed, 0 failed |
| `npm ci` (repo root) | ✅ Completed (dev-tool audit warnings reported by npm) |
| `npm ci` (`RefTestManagement.Ui`) | ✅ Completed |
| `npm run check:i18n` (`RefTestManagement.Ui`) | ✅ 637 keys in each locale, 0 missing/orphaned |
| `npm run test -- --watch=false --no-progress` (`RefTestManagement.Ui`) | ✅ 36 passed / 3 files |
| `npm run build -- --configuration production` (`RefTestManagement.Ui`) | ✅ Passed |
| `dotnet publish RefTestManagement.Api/RefTestManagement.Api.csproj --configuration Release --no-build` | ✅ Passed |
| GitHub Actions run review (`list_workflow_runs` + `get_job_logs`) | ✅ Reviewed latest PR-check run metadata; no failed jobs returned for run `35669504348` |

---

## Recommended next steps (priority order)

1. **WP-61 (R6-01):** unify public privacy notice metadata with backend-configured notice contract and add parity tests.
2. **WP-62 (R6-02):** redact mutation exception logs in email mutation paths, aligned with existing shared helper.
3. **WP-63 (R6-03):** remove automated `main` force-push release path; enforce non-rewriting promotion.
4. **WP-64 (R6-04):** add production-config GraphQL regression test for object-identification/anonymous node exposure.
5. Keep operational GDPR evidence collection separate: processor/DPA records, transfer safeguards, backup-erasure operations, DSAR handling logs.

## Source-verifiable vs non-repository evidence

**Source-verifiable in this audit:** implementation behavior, authorization boundaries, job payload handling, workflow YAML behavior, and automated build/test outcomes listed above.

**Not verifiable from source alone:** legal basis determination, signed DPAs, international-transfer mechanisms, backup purge execution, production proxy trust lists as deployed, and DSAR operational SLA evidence.
