# Deep Audit — Round 12

| | |
|---|---|
| **Repository** | `handballbelgium-referees/ref-test-management` |
| **Date** | 2026-10-08 |
| **Commit audited** | `265334c974374dce6adf2dea97e1cd5ea93de13b` (`main`) |
| **Previous reports** | [R2](AUDIT-R2.md), [R3](AUDIT-R3.md), [R4](AUDIT-R4.md), [R5](AUDIT-R5.md), [R6](AUDIT-R6.md), [R7](AUDIT-R7.md), [R8](AUDIT-R8.md), [R9](AUDIT-R9.md), [R10](AUDIT-R10.md), [R11](AUDIT-R11.md) |
| **Scope** | Backend domain logic and invariants; GraphQL authentication/authorization and Auth0; background jobs, transactions, and logging; frontend correctness, i18n, and accessibility; GDPR/privacy technical controls; CI/CD, release integrity, and supply chain; documentation parity |
| **Method** | Fresh, read-only source review with four bounded area reviews, prescribed validation, and review of recent failed CI runs |

---

## Executive summary

**Production readiness: NOT READY — 19 findings require remediation or explicit risk acceptance before the repository can support a production-readiness conclusion.**

The .NET and UI builds and tests pass, and the source contains several useful privacy and job-processing controls. Material gaps remain: terminal withdrawal cleanup can remove the automatic recovery reference for failed erasure work; the self-service export omits stored progress and selected answers; provider-controlled exception detail reaches a logger before job-message masking; and the dependency gate does not block High npm advisories. Accessibility and documentation issues also remain. Passing tests do not establish deployed configuration or operational effectiveness.

**GDPR evidence status (mixed):** The repository documents technical privacy flows and records some operational checks, but the operations checklist still has pending backup, logging, and data-subject-request evidence. Processor terms and controller/legal determinations also remain pending. This audit did not inspect live deployments or provider systems and makes no legal-compliance conclusion.

### Verdict by area

| Area | Verdict |
|---|---|
| Backend domain logic and invariants | Needs remediation — export matching and scope gaps, plus loss of a failed withdrawal target's recovery reference |
| GraphQL authentication and authorization | Needs remediation — logout return-target construction relies on the request host; wildcard permission guidance is inconsistent |
| Background jobs, transactions, and logging | Needs remediation — failed withdrawal recovery state is removed, provider exception details are logged, and expiration work can be duplicated |
| Frontend correctness, i18n, and accessibility | Needs remediation — zoom, contrast, form-error association, translation, and loading-state issues |
| GDPR/privacy technical controls | Needs remediation — export and withdrawal gaps; operational and legal evidence remains incomplete |
| CI/CD and supply chain | Needs remediation — High dependency advisories are non-blocking and action digest updates can auto-merge |
| Documentation parity | Needs remediation — hosted-service, IHF data-flow, permission, and language-availability documentation needs alignment |

| Severity | Count |
|---|---:|
| 🔴 Critical | 0 |
| 🟠 High | 0 |
| 🟡 Medium | 9 |
| ⚪ Low | 10 |

## Findings

| # | Severity | Area | Finding |
|---|---|---|---|
| R12-01 | 🟡 Medium | GDPR/privacy technical controls | Exhausted withdrawal targets lose their automatic recovery reference |
| R12-02 | 🟡 Medium | GDPR/privacy technical controls | Whitespace-normalized export requests can miss stored addresses |
| R12-03 | 🟡 Medium | GDPR/privacy technical controls | Self-service export omits stored progress and selected answers |
| R12-04 | 🟡 Medium | Background jobs, transactions, and logging | Provider-controlled scoring errors can reach expiration-job logs |
| R12-05 | 🟡 Medium | Frontend correctness, i18n, and accessibility | Touch viewport configuration disables user zoom |
| R12-06 | 🟡 Medium | Frontend correctness, i18n, and accessibility | Home-page description text falls below normal-text contrast |
| R12-07 | 🟡 Medium | Frontend correctness, i18n, and accessibility | Creation-form validation errors are not associated with their fields |
| R12-08 | 🟡 Medium | CI/CD and supply chain | High npm advisories do not block dependency checks |
| R12-09 | 🟡 Medium | CI/CD and supply chain | Action-digest auto-merge can update production-capable workflows |
| R12-10 | ⚪ Low | Background jobs, transactions, and logging | No-work withdrawal confirmations are absent from typed audit events |
| R12-11 | ⚪ Low | Background jobs, transactions, and logging | Expiration sweeps may enqueue duplicate jobs |
| R12-12 | ⚪ Low | Documentation parity | The architecture diagram misstates the hosted-service inventory |
| R12-13 | ⚪ Low | Documentation parity | IHF data-flow records express inconsistent certainty |
| R12-14 | ⚪ Low | GraphQL authentication and authorization | Logout return-target construction uses the request host |
| R12-15 | ⚪ Low | GraphQL authentication and authorization | Wildcard permission setup guidance conflicts with implementation |
| R12-16 | ⚪ Low | Frontend correctness, i18n, and accessibility | Secondary neutral text fails contrast |
| R12-17 | ⚪ Low | Frontend correctness, i18n, and accessibility | Checkbox ARIA label references a mismatched translation key |
| R12-18 | ⚪ Low | Frontend correctness, i18n, and accessibility | Loading indicators lack programmatic status semantics |
| R12-19 | ⚪ Low | Documentation parity | Privacy-notice language availability omits deployment configurability |

### Medium findings

#### R12-01 — Exhausted withdrawal targets lose their automatic recovery reference

- **Severity:** Medium
- **Area:** GDPR/privacy technical controls
- **Evidence:** `RefTestManagement.Domain/Privacy/PrivacyWithdrawalBatchTarget.cs:45-54` stores the RefTest ID and failure category on each target; `RefTestManagement.Api/BackgroundServices/PrivacyWithdrawalCleanupService.cs:118-138` removes targets after their batch is terminal and its latest job has failed; `RefTestManagement.Domain/Privacy/PrivacyWithdrawalBatch.cs:10-11,30-35` retains batch metadata and counts rather than target IDs; `RefTestManagement.Api/Services/PrivacyWithdrawalRequestService.cs:222-230` reconciles only incomplete batches; `docs/PRIVACY.md:142` describes target removal after terminal failure.
- **Confidence:** 9/10

If all erasure attempts fail before anonymization commits, cleanup can delete the target row containing the RefTest ID and failure category. The remaining batch cannot automatically identify the RefTest for retry or operational review.

**Recommendation:** Retain a minimal failed-target reference and sanitized failure category until the target is requeued or explicitly reviewed; purge them only after resolution.

#### R12-02 — Whitespace-normalized export requests can miss stored addresses

- **Severity:** Medium
- **Area:** GDPR/privacy technical controls
- **Evidence:** `RefTestManagement.Api/Services/PersonalDataExportRequestService.cs:28-43` trims the submitted address but compares it to `RefTest.Email` without trimming the stored value; `RefTestManagement.Api/Graphql/Mutations/Update/RefTestUpdateMutations.cs:50` passes the supplied address to the domain update; `RefTestManagement.Domain/RefTests/RefTest.cs:395-397,668-671` stores the address as supplied; `docs/PRIVACY.md:78` describes trimming the request and matching it against stored addresses.
- **Confidence:** 9/10

If an address is stored with surrounding whitespace, a normal trimmed export request will not match it and no verification email will be queued.

**Recommendation:** Normalize addresses consistently at write and lookup boundaries and add a whitespace round-trip test.

#### R12-03 — Self-service export omits stored progress and selected answers

- **Severity:** Medium
- **Area:** GDPR/privacy technical controls
- **Evidence:** `docs/PRIVACY.md:20` lists assessment progress and answers among processed data; `RefTestManagement.Application/Models/PersonalDataExportPayloads.cs:16-35` does not include the current question or selected-answer IDs; `RefTestManagement.Infrastructure/Services/PersonalDataExportPdfService.cs:311-336` renders the fields present in that export model; `RefTestManagement.Domain/RefTests/RefTest.cs:122,128,304-306` stores the current question index and selected-answer IDs.
- **Confidence:** 9/10

The self-service PDF omits these stored, participant-associated fields. This is a repository-level completeness issue, not a legal classification.

**Recommendation:** Include the fields in the export, or document an approved exclusion and align the stated export scope.

#### R12-04 — Provider-controlled scoring errors can reach expiration-job logs

- **Severity:** Medium
- **Area:** Background jobs, transactions, and logging
- **Evidence:** `RefTestManagement.Application/Services/IHFRulesQuestionsService.cs:195-199` throws a provider error message; `RefTestManagement.Api/BackgroundServices/JobHandlers/RefTestExpirationJobHandler.cs:69-72,114-117` logs the exception object with a RefTest ID; `RefTestManagement.Infrastructure/Logging/ServiceLoggerMessages.cs:247-248` defines that log event; `RefTestManagement.Api/BackgroundServices/BackgroundJobService.cs:349-353` masks the persisted job error after the handler has logged and rethrown.
- **Confidence:** 8/10

If an upstream exception contains participant-linked assessment details, this path sends that unredacted exception to the logger before the job service masks the message it stores. Whether the provider returns such details or what the logging sink retains was not verified.

**Recommendation:** Log a fixed failure category and a reference only; sanitize or suppress provider-controlled exception details before logging.

#### R12-05 — Touch viewport configuration disables user zoom

- **Severity:** Medium
- **Area:** Frontend correctness, i18n, and accessibility
- **Evidence:** `RefTestManagement.Ui/src/main.ts:6-13`.
- **Confidence:** 9/10

The viewport settings prevent zoom on some touch devices, which can make the interface harder to use for people who rely on magnification.

**Recommendation:** Remove restrictive zoom settings and rely on responsive layout behavior.

#### R12-06 — Home-page description text falls below normal-text contrast

- **Severity:** Medium
- **Area:** Frontend correctness, i18n, and accessibility
- **Evidence:** `RefTestManagement.Ui/src/app/home/home.html:98,114`; `RefTestManagement.Ui/src/styles.css:38,43-44`.
- **Confidence:** 9/10

The description text and its background have a measured contrast ratio of 2.74:1, below the 4.5:1 WCAG AA threshold for normal-sized text.

**Recommendation:** Adjust the text or background color to meet at least 4.5:1 contrast.

#### R12-07 — Creation-form validation errors are not associated with their fields

- **Severity:** Medium
- **Area:** Frontend correctness, i18n, and accessibility
- **Evidence:** `RefTestManagement.Ui/src/app/ref-tests/create/components/ref-test-user-list-item/ref-test-user-list-item.html:14-29,42-57,70-86`.
- **Confidence:** 9/10

Validation messages are rendered without a programmatic association to their corresponding inputs, so assistive-technology users may not be told which field has an error.

**Recommendation:** Associate each message with its input and expose the invalid state programmatically.

#### R12-08 — High npm advisories do not block dependency checks

- **Severity:** Medium
- **Area:** CI/CD and supply chain
- **Evidence:** `docs/SECURITY.md:23`; `scripts/check-npm-audit.mjs:12`.
- **Confidence:** 9/10

The repository's npm audit check blocks Critical advisories but permits High and Moderate advisories. The root audit on the audited commit returned 11 High and 2 Moderate advisories; the UI install reported 14 High advisories.

**Recommendation:** Make High advisories fail the required check or require an explicit, time-bounded risk acceptance for each exception.

#### R12-09 — Action-digest auto-merge can update production-capable workflows

- **Severity:** Medium
- **Area:** CI/CD and supply chain
- **Evidence:** `renovate.json:49-54` enables automatic merging for GitHub Action digest updates; `.github/workflows/stable-release.yml:525,574-575` contains production-capable release permissions and Azure/OIDC authentication.
- **Confidence:** 7/10

An automatically merged action update can enter a workflow that obtains production-capable credentials. A compromised or undesired action revision could therefore reach a sensitive release path. Branch-protection rules, environment approvals, and OIDC trust restrictions were not independently verified.

**Recommendation:** Require human review or an equivalent explicit approval gate for action digest changes affecting production-capable workflows.

### Low findings

#### R12-10 — No-work withdrawal confirmations are absent from typed audit events

- **Severity:** Low
- **Area:** Background jobs, transactions, and logging
- **Evidence:** `RefTestManagement.Domain/Privacy/PrivacyWithdrawalChallenge.cs:13,193-195,246-260` implements `IHasDomainEvents`, exposes no domain events, and consumes the challenge; `RefTestManagement.Api/Services/PrivacyWithdrawalRequestService.cs:318-328` saves a successful confirmation even when no batch is queued; `RefTestManagement.AuditLog/AuditSaveChangesInterceptor.cs:157-165` skips eventless changes for entities implementing `IHasDomainEvents`.
- **Confidence:** 9/10

A successful mailbox-verified confirmation that creates no new withdrawal work changes challenge state without a typed audit event, reducing traceability of that request.

**Recommendation:** Record a minimal non-PII confirmation event or document the intentional exclusion.

#### R12-11 — Expiration sweeps may enqueue duplicate jobs

- **Severity:** Low
- **Area:** Background jobs, transactions, and logging
- **Evidence:** `RefTestManagement.Api/BackgroundServices/RefTestExpirationService.cs:81-105` enqueues work for every currently due RefTest; `RefTestManagement.Infrastructure/Services/JobEnqueueService.cs:174-180` creates and adds a new job without coalescing in this path.
- **Confidence:** 9/10

If a due RefTest still has a pending or retrying expiration job at the next sweep, another job can be added, increasing queue and database load.

**Recommendation:** Add an idempotency key or atomic per-RefTest/action enqueue claim.

#### R12-12 — The architecture diagram misstates the hosted-service inventory

- **Severity:** Low
- **Area:** Documentation parity
- **Evidence:** `RefTestManagement.Api/Program.cs:227-232,250-252` registers six hosted services unconditionally and audit cleanup conditionally; `docs/ARCHITECTURE-DIAGRAM.md:7-16` says “all five,” omits export-request and withdrawal cleanup, and shows audit cleanup without noting its condition.
- **Confidence:** 10/10

The diagram gives maintainers an inaccurate inventory of in-process background and privacy-cleanup services.

**Recommendation:** Update the count and service list, and mark audit cleanup as conditional.

#### R12-13 — IHF data-flow records express inconsistent certainty

- **Severity:** Low
- **Area:** Documentation parity
- **Evidence:** `docs/GDPR/GDPR-ROPA.md:26` says no personal data is sent; `docs/GDPR/GDPR-PROCESSOR-REGISTER.md:8,21` says classification is not established while listing question and selected-answer IDs; `RefTestManagement.Application/Services/IHFRulesQuestionsService.cs:195-199` sends question and selected-answer IDs to the scoring API.
- **Confidence:** 9/10

The records give inconsistent certainty about the technical data flow. This finding does not determine whether the transmitted IDs constitute personal data.

**Recommendation:** Align the technical payload description and keep classification consistently marked as pending until the controller determines it.

#### R12-14 — Logout return-target construction uses the request host

- **Severity:** Low
- **Area:** GraphQL authentication and authorization
- **Evidence:** `RefTestManagement.Api/Controllers/AccountController.cs:50,52` constrains the requested post-logout path and starts sign-out; `RefTestManagement.Api/SecurityStartup.cs:146-149` expands a relative post-logout URI using `Request.Host`; `RefTestManagement.Api/appsettings.json:9` sets `AllowedHosts` to `*`.
- **Confidence:** 7/10

If an untrusted Host value reaches the application and the deployed Auth0 tenant does not constrain the return target, a caller may influence the post-logout destination. Proxy behavior and Auth0 allowed-logout settings were not verified.

**Recommendation:** Build logout targets from a configured canonical origin or a strict allowlist rather than the request host.

#### R12-15 — Wildcard permission setup guidance conflicts with implementation

- **Severity:** Low
- **Area:** GraphQL authentication and authorization
- **Evidence:** `RefTestManagement.Security/Permissions.cs:37,58-60`; `RefTestManagement.Api/BackgroundServices/PermissionSyncService.cs:55`; `README.md:65`; `docs/SECURITY.md:50,97`.
- **Confidence:** 8/10

The permission constants define wildcard scopes separately from the individual-permission list passed to startup synchronization, while the README and security guide describe wildcard scopes and manual registration differently. This can lead to configuration drift; the live Auth0 permission set was not verified.

**Recommendation:** Align the setup guidance with the exact permission values and synchronization behavior.

#### R12-16 — Secondary neutral text fails contrast

- **Severity:** Low
- **Area:** Frontend correctness, i18n, and accessibility
- **Evidence:** `RefTestManagement.Ui/src/app/ref-tests/detail/ref-test-detail.html:297`; `RefTestManagement.Ui/src/app/ref-test/take/components/ref-test-results/ref-test-results.html:99`; `RefTestManagement.Ui/src/styles.css:54`.
- **Confidence:** 9/10

The neutral text color measures approximately 2.42:1 to 2.52:1 against the cited light backgrounds, below the 4.5:1 normal-text contrast threshold.

**Recommendation:** Darken the neutral text color or use a higher-contrast text token for these surfaces.

#### R12-17 — Checkbox ARIA label references a mismatched translation key

- **Severity:** Low
- **Area:** Frontend correctness, i18n, and accessibility
- **Evidence:** `RefTestManagement.Ui/src/app/ref-tests/create/create-ref-tests.html:276-277` uses `randomQuestionsForEachUser`; `RefTestManagement.Ui/public/i18n/en.json:543-544` defines the key as `random_questions_for_each_user`.
- **Confidence:** 9/10

The explicit ARIA label can resolve to a missing translation even though the visible label uses the catalog's existing key.

**Recommendation:** Use the same canonical translation key for the accessible label and visible text.

#### R12-18 — Loading indicators lack programmatic status semantics

- **Severity:** Low
- **Area:** Frontend correctness, i18n, and accessibility
- **Evidence:** `RefTestManagement.Ui/src/app/home/home.html:58-61`; `RefTestManagement.Ui/src/app/ref-test/take/take-ref-test.html:4-11`.
- **Confidence:** 8/10

The loading states are visually presented without programmatic status semantics, so assistive technologies may not announce that content is loading.

**Recommendation:** Expose loading text through a status role or equivalent accessible live-region semantics.

#### R12-19 — Privacy-notice language availability omits deployment configurability

- **Severity:** Low
- **Area:** Documentation parity
- **Evidence:** `docs/PRIVACY.md:14` describes the notice as available in four languages; `README.md:90` states enabled languages are configurable per deployment.
- **Confidence:** 8/10

The privacy documentation can be read as promising four languages in every deployment, although the enabled language list is configurable.

**Recommendation:** Describe the four languages as defaults or qualify the notice's availability by deployment configuration.

## Validation performed

| Check | Result |
|---|---|
| `dotnet restore RefTestManagement.slnx` | Pass |
| `dotnet build RefTestManagement.slnx --configuration Release --no-restore` | Pass |
| `dotnet test --solution RefTestManagement.slnx --configuration Release --no-build -p:TestingPlatformShowTestsFailure=true` | Pass; no test failures |
| Root `npm ci` | Pass |
| UI `npm ci` | Pass; install reported 14 High advisories |
| Root `npm audit --json` | Exit 1; 0 Critical, 11 High, 2 Moderate advisories |
| UI `npm run check:i18n` | Pass |
| UI `npm test -- --watch=false` | Pass; 37 files and 214 tests |
| UI `npm run build -- --configuration production` | Pass with an initial-bundle warning: 609.29 kB against a 600 kB budget |
| Recent CI failure review (`gh run list --limit 20` and failed logs) | Two reviewed failures were on older non-main branches: title-autocomplete test failure on `feat/audit-r10` (run `37773395517`) and release-safety assertion failure on `chore/audit-r9` (run `37679383918`); local main validation passed |

## Previous-round status

| Prior finding | Status | Current evidence |
|---|---|---|
| R11-01 | 🔄 Partial | `docs/GDPR/GDPR-OPERATIONS-EVIDENCE.md:16-20,33-37` still lists pending operational evidence; `docs/GDPR/GDPR-PROCESSOR-REGISTER.md:56-57` records a dated configuration check but does not close the remaining checklist |
| R11-02 | ❌ Open | `docs/GDPR/GDPR-PROCESSOR-REGISTER.md:18-21`; `docs/GDPR/GDPR-LEGAL-BASIS-RECORD.md:12-15` continue to mark controller/legal and processor determinations as pending |
| R11-03 | ⚪ Not rechecked | The remediation tracker marks it implemented at `docs/Remediations/AUDIT-R11-REMEDIATION.md:20`; no independent current-source check was performed in this pass |
| R11-04 | ✅ Resolved (spot-checked) | The tracker marks it implemented at `docs/Remediations/AUDIT-R11-REMEDIATION.md:47`; `RefTestManagement.Ui/src/app/privacy/components/personal-data-export/request/personal-data-export-request.html:58` exposes `aria-required="true"` |
| R11-05 | ✅ Resolved (spot-checked) | The tracker marks it implemented at `docs/Remediations/AUDIT-R11-REMEDIATION.md:72`; `RefTestManagement.Ui/src/app/app.routes.ts:19-22` routes `/privacy/export-request` to the request component, and `RefTestManagement.Ui/src/app/privacy/privacy-notice.html:109` links to it |

## Source-verifiable vs non-repository evidence

### Source-verifiable

- The audited commit and branch were confirmed locally; the worktree was clean before report creation.
- The findings above are supported by the cited repository source, configuration, and documentation.
- The required .NET and UI validation passed as recorded above; the root npm audit reported the listed advisories.
- `docs/GDPR/GDPR-OPERATIONS-EVIDENCE.md:16-20,33-37,45,51` records pending operational evidence. `docs/GDPR/GDPR-PROCESSOR-REGISTER.md:18-21` and `docs/GDPR/GDPR-LEGAL-BASIS-RECORD.md:12-15` record open processor and legal-basis questions.
- The apparent frontend empty-language initialization issue was excluded: `RefTestManagement.Api/Program.cs:81-82` replaces an empty configured language list with defaults.

### Non-repository or operational evidence

- Live Azure App Service, database, backup, logging, and monitoring configuration was not independently inspected; repository records are not live verification.
- Auth0 tenant logout allowlists, production host/proxy behavior, GitHub branch protections, required reviews, environment approvals, and Azure OIDC trust were not verified.
- Provider-side IHF behavior, logging, retention, storage, and processor terms were not independently confirmed.
- Actual production queue state, retry monitoring, restoration tests, DSAR operations, and controller/legal determinations were not verified.
- No conclusion about GDPR compliance or the legal classification of the IHF payload is made.

## Remediation status

No application or configuration remediation was performed as part of this audit. The findings are ready for a separately approved `/deliver R12` remediation plan; see [the R11 remediation tracker](../Remediations/AUDIT-R11-REMEDIATION.md) for prior-round status.
