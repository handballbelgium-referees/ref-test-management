# Deep Audit — Round 9

| | |
|---|---|
| **Repository** | `handballbelgium-referees/ref-test-management` |
| **Date** | 2026-10-06 |
| **Commit audited** | `ea5df9c99f4edbe433181361af935f57430ed715` (`main`) |
| **Previous reports** | [Initial audit](./AUDIT.md), [R2](./AUDIT-R2.md), [R3](./AUDIT-R3.md), [R4](./AUDIT-R4.md), [R5](./AUDIT-R5.md), [R6](./AUDIT-R6.md), [R7](./AUDIT-R7.md), [R8](./AUDIT-R8.md) |
| **Scope** | Backend domain/jobs/privacy/logging and documentation; GraphQL authorization, Auth0, and security headers; frontend correctness, i18n, and accessibility; CI/CD, releases, and supply chain |
| **Method** | Fresh, read-only review by four area auditors; source and documentation checks, prior-round reconciliation, build/test/dependency validation, and review of the latest 20 GitHub Actions runs |

**Post-baseline note:** At report preparation, `HEAD` was `664348760427e72f125c0ebed6c88bc89c2a3788`, a direct child of the audited commit. That commit only updates `badges/pre-release.png`; all findings and line references below are anchored to the audited commit, and no source files changed after it.

---

## Executive summary

**Production readiness: NOT READY —** 41 repository findings remain: 21 medium and 20 low. The most material gaps concern mutation failure handling, email delivery state, privacy-erasure races and recovery, frontend submission/error recovery, and dependency-audit enforcement. No GraphQL operation-coverage authorization bypass was established in this review. Automated build and test coverage is strong, but passing tests do not resolve the identified runtime and control gaps.

**GDPR evidence status (mixed):** The repository documents technical anonymization, retention, and erasure controls, but also retains some anonymized test and question/answer identifiers. Whether those identifiers can be linked to participants is unknown and was not established during this audit. The repository includes a processor register and an operational-evidence checklist, but it cannot establish the live provider, legal, backup, log-retention, or transfer safeguards. This is not a legal-compliance determination.

### Verdict by area

| Area | Verdict |
|---|---|
| Backend domain logic and invariants | Needs remediation — mutation failures can leave staged state persisted, and retention eligibility is not rechecked at erasure time. |
| GraphQL authentication and authorization | Needs remediation — account blocking is not rechecked during permission refresh; live Auth0 tenant behavior remains unverified. No operation-coverage bypass was found. |
| Background jobs, transactions, and logging | Needs remediation — provider rejection can be recorded as successful delivery, and some failed or in-flight jobs have no reliable recovery path. |
| Frontend correctness, i18n, and accessibility | Needs remediation — submission and lookup failures can lose recoverability or resemble empty results; schedule controls have keyboard and labeling gaps. |
| GDPR/privacy technical controls | Needs remediation — erasure races, exhausted withdrawal work, and uncertain identifier linkability need resolution or evidence. |
| CI/CD and supply chain | Needs remediation — high dependency advisories do not block workflows, the UI dependency tree is omitted from release audits, and release tooling can be auto-merged. |
| Documentation parity | Needs remediation — privacy, Auth0, permissions, release, and audit-log wording does not consistently match implementation. |

| Severity | Count |
|---|---:|
| 🔴 Critical | 0 |
| 🟠 High | 0 |
| 🟡 Medium | 21 |
| ⚪ Low | 20 |

These counts classify the triaged repository findings below; raw dependency-advisory severities are reported separately under Validation performed.

## Findings

| # | Severity | Area | Finding |
|---|---|---|---|
| R9-01 | Medium | Backend | Per-item mutation failures can persist staged state |
| R9-02 | Medium | Jobs/email | Provider rejection can be recorded as successful delivery |
| R9-03 | Medium | Privacy | Stale retention candidates can erase reactivated tests |
| R9-04 | Medium | Privacy/jobs | Exhausted withdrawal targets have no recovery path |
| R9-05 | Medium | Privacy/configuration | Retention periods accept unsafe values |
| R9-06 | Medium | Privacy/jobs | In-flight email can outlast erasure |
| R9-07 | Medium | Privacy/jobs | Final-attempt export jobs can strand request data |
| R9-08 | Medium | Logging/privacy | Provider response bodies may expose invitation tokens |
| R9-09 | Medium | GraphQL/Auth0 | Permission refresh does not recheck blocked accounts |
| R9-10 | Medium | Frontend | Failed test submission has no retry path |
| R9-11 | Medium | Frontend | Leaving during answer debounce can lose the latest answer |
| R9-12 | Medium | Frontend | Passing threshold defaults to zero before configuration loads |
| R9-13 | Medium | Frontend | Create-test loading state is captured only once |
| R9-14 | Medium | Frontend | Lookup failures appear as empty search results |
| R9-15 | Medium | Frontend | Query failures are not distinguishable from empty data |
| R9-16 | Medium | Frontend | Authentication-check errors have no recovery state |
| R9-17 | Medium | Frontend/accessibility | Schedule picker has keyboard, naming, and localization gaps |
| R9-18 | Medium | Privacy/i18n | Localized erasure wording conflicts with retained anonymized records |
| R9-19 | Medium | CI/CD | Dependency checks do not block high-severity advisories |
| R9-20 | Medium | CI/CD | Release validation omits the UI dependency tree |
| R9-21 | Medium | Supply chain | Renovate can auto-merge release tooling used with write credentials |
| R9-22 | Low | Privacy | Retained identifiers may be linkable to participants |
| R9-23 | Low | Privacy/jobs | One retention-sweep failure delays later records |
| R9-24 | Low | Privacy/performance | Public withdrawal matching materializes all eligible records |
| R9-25 | Low | Documentation | README overstates audit-log immutability |
| R9-26 | Low | Security headers | CSP permits broad HTTPS fallbacks and inline styles |
| R9-27 | Low | Security headers | HSTS is configured/documented for only 30 days |
| R9-28 | Low | Auth0/documentation | Configuration docs conflate OIDC client ID and API audience |
| R9-29 | Low | GraphQL/documentation | Permissions endpoint documentation omits its 503 response |
| R9-30 | Low | Auth0/performance | Approver lookup scans users and checks grants per user |
| R9-31 | Low | Auth0 | Permission synchronization may overwrite customized scope descriptions |
| R9-32 | Low | Frontend/accessibility | Screen readers are not notified when the test question changes |
| R9-33 | Low | Frontend | Filtered empty state uses create-first wording |
| R9-34 | Low | Frontend/accessibility | Autocomplete does not expose active-option state |
| R9-35 | Low | Frontend/accessibility | Language menu roles are not matched by menu keyboard handling |
| R9-36 | Low | CI/CD | Stable deployment does not wait for main-branch synchronization |
| R9-37 | Low | CI/CD/documentation | CODEOWNERS patterns do not cover the intended defaults and active release config |
| R9-38 | Low | CI/CD | Fork pull requests may fail the README-sync checkout |
| R9-39 | Low | Supply chain | Provenance omits exact toolchain and runner versions |
| R9-40 | Low | Documentation | README implies every main push publishes a pre-release |
| R9-41 | Low | CI/CD/documentation | Badge update is best-effort despite README wording |

### Medium findings

#### R9-01 — Per-item mutation failures can persist staged state

- **Severity:** Medium
- **Area:** Backend domain logic and invariants
- **Type:** Confirmed defect
- **Evidence:** `RefTestManagement.Api/Graphql/Mutations/Reset/RefTestResetMutations.cs:65-146`; `RefTestManagement.Api/Graphql/Mutations/Approval/RefTestApprovalMutations.cs:65-184` — per-item failures are handled while the shared mutation context can still be saved.

A failed reset or approval sub-operation can leave some changes staged and then persisted with the successful items. Callers may receive a result that does not match the committed state, including approval without its required invitation job.

**Recommendation:** Make each item atomic, or stage and persist only after all required per-item work succeeds; ensure returned counts reflect committed outcomes.

#### R9-02 — Provider rejection can be recorded as successful delivery

- **Severity:** Medium
- **Area:** Background jobs and email reliability
- **Type:** Confirmed defect
- **Evidence:** `RefTestManagement.Infrastructure/Services/EmailService.cs:57-100,190-227`; `RefTestManagement.Api/BackgroundServices/JobHandlers/InvitationEmailJobHandler.cs:22-56`; `RefTestManagement.Api/BackgroundServices/JobHandlers/ResultEmailJobHandler.cs:29-63` — the email service can return an unsuccessful result, but the handlers proceed to mark the email job as sent.

When the provider rejects a request or the email service cannot submit it, the job can still be treated as delivered and will not be retried. The recipient may never receive the invitation or result.

**Recommendation:** Propagate provider acceptance as a typed result or exception and mark jobs sent only after confirmed submission.

#### R9-03 — Stale retention candidates can erase reactivated tests

- **Severity:** Medium
- **Area:** Privacy and retention
- **Type:** Confirmed defect
- **Evidence:** `RefTestManagement.Api/BackgroundServices/PrivacyRetentionService.cs:49-62`; `RefTestManagement.Infrastructure/Services/RefTestPrivacyErasureService.cs:151-174`; `RefTestManagement.Infrastructure/Queries/PrivacyRetentionQueries.cs:30-55` — candidates are selected before erasure, but eligibility is not revalidated after loading the candidate.

If a test's state changes after candidate selection, the erasure worker can anonymize it using stale eligibility. A rejection that has since been approved or otherwise reactivated may therefore be erased.

**Recommendation:** Recheck the current status and retention cutoff immediately before anonymization, in the same transaction as the erasure.

#### R9-04 — Exhausted withdrawal targets have no recovery path

- **Severity:** Medium
- **Area:** Privacy and background-job recovery
- **Type:** Missing control
- **Evidence:** `RefTestManagement.Domain/Privacy/PrivacyWithdrawalBatchTarget.cs:55-104`; `RefTestManagement.Api/BackgroundServices/JobHandlers/PrivacyWithdrawalBatchJobHandler.cs:90-112,215-236`; `RefTestManagement.Api/Services/PrivacyWithdrawalRequestService.cs:423-454` — attempts are capped and exhausted targets are excluded from normal retries.

A target that repeatedly fails can remain unresolved without an automatic retry, operator alert, or explicit repair flow. Personal-data withdrawal may therefore remain incomplete without a visible escalation.

**Recommendation:** Add an observable terminal state and operator recovery/escalation path for exhausted targets.

#### R9-05 — Retention periods accept unsafe values

- **Severity:** Medium
- **Area:** Privacy configuration
- **Type:** Missing control
- **Evidence:** `RefTestManagement.Api/Program.cs:43-58,86-110`; `RefTestManagement.Application/Configurations/PrivacyConfiguration.cs:1-11`; `RefTestManagement.AuditLog/AuditLogOptions.cs:1-20` — configured retention values are bound without a positive, bounded range validation.

Zero or negative values can make retention and audit cleanup immediately eligible or otherwise produce unintended cutoffs, increasing the risk of premature deletion or redaction.

**Recommendation:** Validate retention settings at startup against explicit, positive supported ranges and fail startup with a clear configuration error.

#### R9-06 — In-flight email can outlast erasure

- **Severity:** Medium
- **Area:** Privacy and background jobs
- **Type:** Conditional race risk
- **Evidence:** `RefTestManagement.Infrastructure/Services/RefTestPrivacyErasureService.cs:190-216,360-375`; `RefTestManagement.Api/BackgroundServices/BackgroundJobService.cs:226-249`; `RefTestManagement.Api/BackgroundServices/JobHandlers/InvitationEmailJobHandler.cs:22-56`; `RefTestManagement.Api/BackgroundServices/JobHandlers/ResultEmailJobHandler.cs:29-63`.

If erasure races with a handler that has already loaded a recipient or test result, that in-flight handler can still submit participant data after the erasure transaction completes. The race depends on timing and provider submission.

**Recommendation:** Recheck cancellation and erasure state immediately before provider submission, and cancel or invalidate pending mail work as part of erasure.

#### R9-07 — Final-attempt export jobs can strand request data

- **Severity:** Medium
- **Area:** Privacy and background-job recovery
- **Type:** Confirmed recovery gap
- **Evidence:** `RefTestManagement.Api/BackgroundServices/BackgroundJobService.cs:104-120,145-180,312-330`; `RefTestManagement.Infrastructure/Queries/PersonalDataExportRequestCleanupQueries.cs:9-22`; `RefTestManagement.Domain/Privacy/PersonalDataExportRequest.cs:41-49`.

If processing fails on the final attempt, a request can remain associated with a non-reclaimable processing job while cleanup only selects other eligible states. Request data, including its contact address, can then outlive the intended cleanup path.

**Recommendation:** Transition expired final-attempt jobs to an explicit terminal state and ensure the linked export request is securely cleaned up or escalated.

#### R9-08 — Provider response bodies may expose invitation tokens

- **Severity:** Medium
- **Area:** Logging and privacy
- **Type:** Conditional disclosure risk
- **Evidence:** `RefTestManagement.Infrastructure/Services/EmailService.cs:57-100,190-227,440-475`; `RefTestManagement.Infrastructure/Logging/ServiceLoggerMessages.cs:24-62,106-142`; `docs/PRIVACY.md:58-82`.

Invitation requests can contain token-bearing links, and provider response bodies are logged. If a provider echoes request content or a token-bearing URL in an error body, people with access to application logs could obtain it. Actual provider responses were not available to verify whether that reflection occurs.

**Recommendation:** Do not log provider response bodies; retain only a bounded status, safe error code, and correlation identifier.

#### R9-09 — Permission refresh does not recheck blocked accounts

- **Severity:** Medium
- **Area:** GraphQL authentication and Auth0
- **Type:** Conditional authorization risk
- **Evidence:** `RefTestManagement.Api/Services/PermissionSnapshotService.cs:130-138`; `RefTestManagement.Auth0/Services/Auth0ManagementService.cs:155-185`; `RefTestManagement.Api/SecurityStartup.cs:25-40`.

Permission refresh reads role or grant data without rechecking whether the account is blocked. If the Auth0 tenant allows those reads or refreshes for a blocked identity, a still-valid application session may continue to receive permissions. The tenant's exact behavior was not verified.

**Recommendation:** Check account status during refresh and fail closed or invalidate the session when the account is blocked.

#### R9-10 — Failed test submission has no retry path

- **Severity:** Medium
- **Area:** Frontend correctness
- **Type:** Missing recovery control
- **Evidence:** `RefTestManagement.Ui/src/app/ref-test/take/state/ref-test.facade.ts:227`; `RefTestManagement.Ui/src/app/ref-test/take/take-ref-test.html:13-15`; `RefTestManagement.Ui/src/app/ref-test/components/ref-test-error/ref-test-error.html:33-42`.

When submission fails, the error component replaces the test without an in-app retry or resume action. The participant cannot recover the submission from that state and may lose the opportunity to submit their answers.

**Recommendation:** Preserve the completed answers and provide a retry/resume action that reuses the same submission data.

#### R9-11 — Leaving during answer debounce can lose the latest answer

- **Severity:** Medium
- **Area:** Frontend correctness
- **Type:** Conditional data-loss risk
- **Evidence:** `RefTestManagement.Ui/src/app/ref-test/take/state/ref-test.facade.ts:46-51`; `RefTestManagement.Ui/src/app/ref-test/take/take-ref-test.ts:126,157-159`.

Answer persistence is debounced. If the participant leaves or the component is destroyed during the debounce window, the pending save can be cancelled before it reaches the server.

**Recommendation:** Flush pending answer persistence before navigation, or persist immediately when the participant confirms leaving.

#### R9-12 — Passing threshold defaults to zero before configuration loads

- **Severity:** Medium
- **Area:** Frontend correctness
- **Type:** Confirmed unsafe default
- **Evidence:** `RefTestManagement.Ui/src/app/ref-test/take/take-ref-test.ts:70-74`; `RefTestManagement.Ui/src/app/ref-test/take/take-ref-test.html:24-25`; `RefTestManagement.Ui/src/app/ref-test/take/components/ref-test-results/ref-test-results.html:6`.

The passing percentage falls back to zero when configuration is not yet available. A result rendered during a loading or configuration-error state can therefore be shown as passing when it should be unknown.

**Recommendation:** Keep the result indeterminate until the passing threshold has loaded successfully; do not substitute zero for unavailable configuration.

#### R9-13 — Create-test loading state is captured only once

- **Severity:** Medium
- **Area:** Frontend correctness
- **Type:** Confirmed defect
- **Evidence:** `RefTestManagement.Ui/src/app/ref-tests/create/create-ref-tests.ts:433`; `RefTestManagement.Ui/src/app/ref-tests/create/create-ref-tests.html:390`; `RefTestManagement.Ui/src/app/shared/utils/apollo-utils.ts:50-56`.

The component copies the mutation's loading value once rather than tracking its changing signal. The submit button can remain enabled while a create request is in flight, allowing duplicate submissions.

**Recommendation:** Bind the template to the mutation loading signal directly, or update component state reactively for the full request lifecycle.

#### R9-14 — Lookup failures appear as empty search results

- **Severity:** Medium
- **Area:** Frontend correctness
- **Type:** Confirmed error-state defect
- **Evidence:** `RefTestManagement.Ui/src/app/ref-tests/create/components/title-autocomplete/title-autocomplete.ts:94,271`; `RefTestManagement.Ui/src/app/ref-tests/create/components/question-search-autocomplete/question-search-autocomplete.ts:76`; `RefTestManagement.Ui/src/app/ref-tests/create/components/question-search-autocomplete/question-search-autocomplete.html:51-56`; `RefTestManagement.Ui/src/app/ref-tests/create/create-ref-tests.ts:362-364`.

Search failures are converted into empty results or the no-results state. In title lookup, that state can lead to manual creation even though the existing title may simply be unavailable because the lookup failed.

**Recommendation:** Render a distinct error state with retry, and allow manual fallback only after a successful search returns no match.

#### R9-15 — Query failures are not distinguishable from empty data

- **Severity:** Medium
- **Area:** Frontend correctness
- **Type:** Missing error state
- **Evidence:** `RefTestManagement.Ui/src/app/ref-tests/services/ref-test-data.ts:78`; `RefTestManagement.Ui/src/app/ref-tests/list/list-ref-tests.html:77-78`; `RefTestManagement.Ui/src/app/audit-logs/audit-log-data.ts:30`; `RefTestManagement.Ui/src/app/audit-logs/list-audit-logs.html:34`; `RefTestManagement.Ui/src/app/privacy/privacy-notice.ts:21-22`; `RefTestManagement.Ui/src/app/privacy/privacy-notice.html:21-22`.

Several read pages have empty/loading behavior without a recoverable query-error state. A failed request can resemble a genuinely empty list or leave the privacy notice absent.

**Recommendation:** Distinguish loading, empty, and failed states on each page and provide a retry action for failures.

#### R9-16 — Authentication-check errors have no recovery state

- **Severity:** Medium
- **Area:** Frontend authentication
- **Type:** Missing error state
- **Evidence:** `RefTestManagement.Ui/src/app/auth/services/auth.ts:11-15`; `RefTestManagement.Ui/src/app/auth/guards/auth-guard.ts:10-11`; `RefTestManagement.Ui/src/app/home/home.ts:44`.

The authentication check has no explicit error path for a failed request. The guard and home page can leave the user in a non-recoverable loading or unauthenticated state rather than explaining the failure.

**Recommendation:** Model authentication-check errors explicitly and offer a retry or sign-in recovery action.

#### R9-17 — Schedule picker has keyboard, naming, and localization gaps

- **Severity:** Medium
- **Area:** Frontend accessibility and i18n
- **Type:** Accessibility and localization defect
- **Evidence:** `RefTestManagement.Ui/src/app/ref-tests/create/create-ref-tests.html:345-351`; `RefTestManagement.Ui/src/app/shared/components/datepicker/datepicker.html:6`; `RefTestManagement.Ui/src/app/shared/components/datepicker/components/datepicker-calendar/components/calendar-view/components/days-grid/days-grid.html:28`; `RefTestManagement.Ui/src/app/shared/components/datepicker/components/datepicker-input/datepicker-input.html:11`; `RefTestManagement.Ui/src/app/shared/components/datetime-picker/components/time-picker/time-picker.html:11`.

The schedule picker does not provide a complete keyboard-operable calendar grid, and some controls have missing or hard-coded accessible names. Keyboard and assistive-technology users may not be able to select dates or understand the time control consistently across locales.

**Recommendation:** Implement the expected calendar keyboard/focus model, associate labels with picker inputs, and localize all accessible names.

#### R9-18 — Localized erasure wording conflicts with retained anonymized records

- **Severity:** Medium
- **Area:** Privacy and localization
- **Type:** Documentation mismatch
- **Evidence:** `RefTestManagement.Ui/public/i18n/en.json:702`; `RefTestManagement.Ui/public/i18n/nl.json:702`; `RefTestManagement.Ui/public/i18n/fr.json:703`; `RefTestManagement.Ui/public/i18n/de.json:702`; `docs/PRIVACY.md:57,60`.

Localized privacy wording describes erasure in a way that does not match the technical privacy documentation, which says some anonymized records are retained. This can create an inaccurate expectation about what is deleted.

**Recommendation:** Align all four locales with the documented erasure/anonymization behavior and distinguish deletion of identifiers from retention of anonymized records.

#### R9-19 — Dependency checks do not block high-severity advisories

- **Severity:** Medium
- **Area:** CI/CD and dependency security
- **Type:** Missing enforcement control
- **Evidence:** `.github/workflows/pr.yml:18,27`; `.github/workflows/beta-release.yml:52-53`; `.github/workflows/stable-release.yml:337-338` — checks fail only at Critical severity.

High-severity dependency advisories can pass pull-request and release checks. The audit snapshot found 11 high advisories in the root dependency tree, including direct release-tool packages.

**Recommendation:** Make high-severity findings blocking, or define and enforce a documented exception process with owners and expiry dates.

#### R9-20 — Release validation omits the UI dependency tree

- **Severity:** Medium
- **Area:** CI/CD and dependency security
- **Type:** Missing enforcement control
- **Evidence:** `.github/workflows/beta-release.yml:52-57`; `.github/workflows/stable-release.yml:337-342`.

The release workflows audit the root npm dependency tree but do not audit the Angular UI dependency tree. The UI audit snapshot reported 33 high and one critical advisory, including a transitive `shell-quote` advisory with a fix available.

**Recommendation:** Add a blocking audit of `RefTestManagement.Ui` dependencies to pull-request and release validation.

#### R9-21 — Renovate can auto-merge release tooling used with write credentials

- **Severity:** Medium
- **Area:** Supply-chain security
- **Type:** Conditional supply-chain risk
- **Evidence:** `renovate.json:69-75`; `package.json:21-28`; `.github/workflows/beta-release.yml:125-150`.

Patch development-dependency updates can be auto-merged, including packages used by semantic release. Release tooling later runs in a workflow with write-capable credentials, increasing impact if an unsafe update is accepted.

**Recommendation:** Exclude privileged release tooling from automatic merge or require human review before it runs with write credentials.

### Low findings

#### R9-22 — Retained identifiers may be linkable to participants

- **Severity:** Low
- **Area:** Privacy and data classification
- **Type:** Conditional, unverified privacy risk
- **Evidence:** `RefTestManagement.Domain/RefTests/RefTest.cs:116-140,628-658`; `RefTestManagement.Application/Services/IHFRulesQuestionsService.cs:194-222`; `docs/PRIVACY.md:124-136`; `docs/GDPR-PROCESSOR-REGISTER.md:15-25`.

Some identifiers remain after anonymization and are sent to the external rules service, while the processor register describes the transferred data as non-personal. Whether those identifiers can be linked back to a participant was not established; the user was unavailable to confirm this data-flow detail. This is a conditional classification risk, not a finding that the identifiers are personal data.

**Recommendation:** Confirm linkability with the data owner and processor, document the resulting classification, and minimize or remove identifiers that are not required.

#### R9-23 — One retention-sweep failure delays later records

- **Severity:** Low
- **Area:** Privacy and background jobs
- **Type:** Confirmed resilience gap
- **Evidence:** `RefTestManagement.Api/BackgroundServices/PrivacyRetentionService.cs:18-43,44-74`.

A failure processing one record can exit the sweep before later candidates are handled; those records wait until a subsequent scheduled run.

**Recommendation:** Isolate failures per record and ensure independent cleanup/repair tasks still run.

#### R9-24 — Public withdrawal matching materializes all eligible records

- **Severity:** Low
- **Area:** Privacy and performance
- **Type:** Confirmed scalability risk
- **Evidence:** `RefTestManagement.Api/Services/PrivacyWithdrawalRequestService.cs:112-150,540-559`; `RefTestManagement.Infrastructure/Queries/PrivacyWithdrawalQueries.cs:5-14`; `RefTestManagement.Api/Graphql/Mutations/Privacy/PrivacyWithdrawalMutations.cs:1-30`; `RefTestManagement.Api/Services/PrivacyChallengeRateLimiter.cs:14-55`.

The rate-limited public matching flow loads eligible records and performs matching in application memory. Cost grows with the total eligible dataset for each admitted request.

**Recommendation:** Push matching into a bounded database query with an appropriate normalized lookup/index.

#### R9-25 — README overstates audit-log immutability

- **Severity:** Low
- **Area:** Documentation parity
- **Type:** Documentation mismatch
- **Evidence:** `README.md:77-86`; `RefTestManagement.Api/BackgroundServices/AuditLogCleanupService.cs:102-154`.

The README describes audit records as immutable, while privacy cleanup can redact or rewrite fields. The statement does not explain this privacy-driven exception.

**Recommendation:** Describe audit records as append-oriented and clarify which fields may be redacted during privacy cleanup.

#### R9-26 — CSP permits broad HTTPS fallbacks and inline styles

- **Severity:** Low
- **Area:** Security headers
- **Type:** Defense-in-depth gap
- **Evidence:** `RefTestManagement.Api/Program.cs:363-372`.

The policy permits broad HTTPS sources and inline styles, and does not explicitly restrict object and frame sources. This weakens the protection provided by the content-security policy if markup or a dependency is compromised.

**Recommendation:** Define explicit source lists and restrictive `object-src` and `frame-src` directives; remove `unsafe-inline` where the application can support nonces or hashes.

#### R9-27 — HSTS is configured/documented for only 30 days

- **Severity:** Low
- **Area:** Security headers
- **Type:** Defense-in-depth gap
- **Evidence:** `RefTestManagement.Api/Program.cs:384-386`; `docs/SECURITY.md:317`.

The documented HSTS max age is 30 days. This gives browsers a shorter HTTPS-only policy than the common one-year baseline.

**Recommendation:** Consider a longer HSTS max age after confirming all relevant subdomains and deployment endpoints support HTTPS.

#### R9-28 — Configuration docs conflate OIDC client ID and API audience

- **Severity:** Low
- **Area:** Auth0 documentation
- **Type:** Documentation mismatch
- **Evidence:** `docs/CONFIGURATION.md:123`; `RefTestManagement.Api/SecurityStartup.cs:45,107`.

The configuration documentation describes the client ID and API audience as interchangeable, while the application uses them for different OIDC and JWT validation settings. This can lead to incorrect deployments or authentication failures.

**Recommendation:** Document the client ID and API audience separately, with the setting used by each flow.

#### R9-29 — Permissions endpoint documentation omits its 503 response

- **Severity:** Low
- **Area:** GraphQL/API documentation
- **Type:** Documentation mismatch
- **Evidence:** `docs/SECURITY.md:208`; `RefTestManagement.Api/Controllers/AccountController.cs:41-42`.

The endpoint documentation describes empty grants or an authentication error but omits the service-unavailable response returned when permission resolution fails.

**Recommendation:** Document the 503 response and the client recovery behavior.

#### R9-30 — Approver lookup scans users and checks grants per user

- **Severity:** Low
- **Area:** Auth0 performance and reliability
- **Type:** Confirmed scalability risk
- **Evidence:** `RefTestManagement.Auth0/Services/Auth0ManagementService.cs:103-120,263-290`; `RefTestManagement.Api/BackgroundServices/JobHandlers/ApprovalNotificationEmailJobHandler.cs:25-27`.

Approver discovery pages through users and checks grants for individual users. As the tenant grows, the lookup can increase latency and Auth0 API usage and become sensitive to tenant quotas.

**Recommendation:** Use a bounded, cached lookup or a query that resolves approvers without scanning the full user directory.

#### R9-31 — Permission synchronization may overwrite customized scope descriptions

- **Severity:** Low
- **Area:** Auth0 configuration
- **Type:** Conditional configuration risk
- **Evidence:** `RefTestManagement.Auth0/Services/Auth0ManagementService.cs:243-246,350-351`.

Synchronization rebuilds managed scope data from the scope value. If the provider update replaces the full scope list, customized descriptions may be overwritten. The exact effect depends on Auth0 API update semantics and tenant configuration, which were not verified.

**Recommendation:** Preserve existing descriptions when reconciling scopes, or confirm and document the provider's patch behavior before replacing scope data.

#### R9-32 — Screen readers are not notified when the test question changes

- **Severity:** Low
- **Area:** Frontend accessibility
- **Type:** Accessibility gap
- **Evidence:** `RefTestManagement.Ui/src/app/ref-test/take/take-ref-test.ts:130`; `RefTestManagement.Ui/src/app/ref-test/take/components/question-card/question-card.html:6`.

The active question changes without an explicit announcement or focus update, so screen-reader users may not know that the test advanced.

**Recommendation:** Announce question changes through a suitable live region or move focus to the new question heading.

#### R9-33 — Filtered empty state uses create-first wording

- **Severity:** Low
- **Area:** Frontend correctness and localization
- **Type:** Misleading empty-state behavior
- **Evidence:** `RefTestManagement.Ui/src/app/ref-tests/list/list-ref-tests.html:77-78`; `RefTestManagement.Ui/src/app/ref-tests/list/components/ref-test-empty-state/ref-test-empty-state.html:16,19,26`; `RefTestManagement.Ui/public/i18n/en.json:191-193` and corresponding `nl`, `fr`, and `de` entries.

When a filter returns no matches, the shared empty state can still tell the user to create their first test. That is misleading when tests exist but are filtered out.

**Recommendation:** Provide a distinct localized no-filter-results state with a clear-filter action.

#### R9-34 — Autocomplete does not expose active-option state

- **Severity:** Low
- **Area:** Frontend accessibility
- **Type:** Accessibility gap
- **Evidence:** `RefTestManagement.Ui/src/app/ref-tests/create/components/title-autocomplete/title-autocomplete.html:2-23,63-78`; `RefTestManagement.Ui/src/app/ref-tests/create/components/question-search-autocomplete/question-search-autocomplete.html:4-16,25-38`.

The autocomplete controls do not expose the active option and popup relationship through the expected combobox/listbox state, limiting usability with assistive technology and keyboard navigation.

**Recommendation:** Implement the combobox pattern, including popup/listbox semantics, active descendant state, and consistent keyboard selection.

#### R9-35 — Language menu roles are not matched by menu keyboard handling

- **Severity:** Low
- **Area:** Frontend accessibility
- **Type:** Accessibility gap
- **Evidence:** `RefTestManagement.Ui/src/app/app.html:98-108`; `RefTestManagement.Ui/src/app/app.ts:111-120`.

The language selector uses menu/menuitem roles but its interaction is click-based and does not implement the corresponding arrow-key and Escape behavior.

**Recommendation:** Implement the menu keyboard interaction model or use a simpler native disclosure/list pattern that matches the actual behavior.

#### R9-36 — Stable deployment does not wait for main-branch synchronization

- **Severity:** Low
- **Area:** CI/CD release ordering
- **Type:** Missing dependency
- **Evidence:** `.github/workflows/stable-release.yml:371-375,455-457`; `README.md:305`.

The workflow contains a main-branch synchronization job, but stable deployment does not depend on that job. Deployment can therefore proceed before the synchronization step completes, despite the README describing the sync as part of the release path.

**Recommendation:** Add the synchronization job to the deployment dependency graph if promotion is intended to wait for it, or clarify that the steps are independent.

#### R9-37 — CODEOWNERS patterns do not cover the intended defaults and active release config

- **Severity:** Low
- **Area:** CI/CD ownership controls
- **Type:** Configuration mismatch
- **Evidence:** `.github/CODEOWNERS:5-7,23`; `.releaserc.mjs:1` — the active release configuration is a different file from the CODEOWNERS pattern.

The default/root ownership pattern is ineffective, and a release-configuration rule refers to a different file name than the active `.releaserc.mjs`. Required review coverage depends on the repository's branch-protection settings, which were not available for verification.

**Recommendation:** Correct the patterns to target the root and active release configuration, then confirm required code-owner review is enabled in repository settings.

#### R9-38 — Fork pull requests may fail the README-sync checkout

- **Severity:** Low
- **Area:** CI/CD
- **Type:** Conditional workflow reliability risk
- **Evidence:** `.github/workflows/pr.yml:3-6,120-124`.

The README-sync path checks out the pull-request branch without explicitly selecting the fork repository. For a pull request from a fork, that ref may not exist in the base repository and the sync step can fail.

**Recommendation:** Use the pull-request head repository and ref for fork checkouts, or skip the write-oriented sync path for forks.

#### R9-39 — Provenance omits exact toolchain and runner versions

- **Severity:** Low
- **Area:** Supply-chain provenance
- **Type:** Missing provenance detail
- **Evidence:** `.github/workflows/beta-release.yml:12-18,269-270`; `.github/workflows/stable-release.yml:337-342`.

The workflows use moving runner/toolchain selectors, while the generated provenance records the source revision and tag without the exact toolchain and runner versions. This makes historical release reproduction less precise.

**Recommendation:** Record exact SDK/runtime and runner image versions in release provenance, and pin tool versions where practical.

#### R9-40 — README implies every main push publishes a pre-release

- **Severity:** Low
- **Area:** Release documentation
- **Type:** Documentation mismatch
- **Evidence:** `README.md:294`; `.releaserc.mjs:58-60`.

The README wording can be read as saying every push to `main` publishes a pre-release, while semantic-release rules exclude some commit types/scopes.

**Recommendation:** Clarify that main pushes trigger the release workflow but publication depends on semantic-release rules.

#### R9-41 — Badge update is best-effort despite README wording

- **Severity:** Low
- **Area:** Release documentation
- **Type:** Documentation mismatch
- **Evidence:** `README.md:309`; `.github/workflows/beta-release.yml:166-167`.

The README says the badge is regenerated automatically, but the workflow treats an update failure as a warning and continues.

**Recommendation:** Describe the update as best-effort, or make badge-update failure block the release if the badge is required.

## Validation performed

| Check | Result |
|---|---|
| `dotnet restore RefTestManagement.slnx` | Passed. |
| `dotnet build RefTestManagement.slnx --configuration Release --no-restore` | Passed; 0 warnings and 0 errors. |
| `dotnet test --solution RefTestManagement.slnx --configuration Release --no-build -p:TestingPlatformShowTestsFailure=true` | Passed; 377 passed, 0 failed, 0 skipped. |
| Root `npm ci` | Passed. |
| UI `npm ci` | Passed. |
| UI `npm run check:i18n` | Passed; 698 keys in each of `en`, `nl`, `fr`, and `de`, with no missing or orphaned keys. |
| UI `npm test -- --watch=false` | Passed; 22 files and 159 tests. |
| UI `npm run build -- --configuration production` | Passed; initial bundle 606.51 kB, 6.51 kB above the 600 kB budget. |
| Root `npm audit --json` | Exit 1; 13 advisories (11 high, 2 moderate, 0 critical), including 2 direct and 11 transitive vulnerable package entries. |
| UI `npm audit --json` | Exit 1; 34 advisories (33 high, 1 critical), including a transitive `shell-quote` critical advisory with a fix available. |
| Latest 20 GitHub Actions runs | No failed runs found. |

## Previous-round status

| Prior finding | Status | Current evidence |
|---|---|---|
| R8-01 | 🔄 Partial; recurrence confirmed | The R8 tracker records WP-73 as implemented, but the current mutation patterns still allow per-item errors followed by persistence; see R9-01 and `docs/AUDIT-R8-REMEDIATION.md:22-44`. |
| R8-02 | ✅ Resolved per tracker; related recovery gap remains | The tracker records the original job-recovery work as implemented. Exhausted withdrawal targets remain outside the normal retry path; see R9-04 and `docs/AUDIT-R8-REMEDIATION.md:60-89`. |
| R8-03, R8-04, R8-05, R8-13 | ✅ Implemented per tracker; no recurrence identified in this review | `docs/AUDIT-R8-REMEDIATION.md:104-228`. |
| R8-06, R8-07, R8-08, R8-09, R8-10, R8-18, R8-19, R8-20 | ✅ Implemented per tracker; no recurrence identified in this review | `docs/AUDIT-R8-REMEDIATION.md:266-342`. |
| R8-11 | ❌ Open | Root dependency audit still reports 11 high advisories; workflows fail only at Critical. See R9-19 and `docs/AUDIT-R8-REMEDIATION.md:383-388`. |
| R8-12 | ⚪ Implementation marked; production configuration unverified | Repository implementation is recorded in `docs/AUDIT-R8-REMEDIATION.md:429-435`; production proxy/CIDR behavior could not be verified from this checkout. |
| R8-14, R8-15 | ⚪ Implementation marked; deployed behavior unverified | `docs/AUDIT-R8-REMEDIATION.md:461-466`; live deployment behavior was not available for verification. |
| R8-16, R8-17, R8-21 | ⚪ Implementation marked; tenant behavior unverified | `docs/AUDIT-R8-REMEDIATION.md:498-532`; live Auth0 scope, account, and quota behavior was not available for verification. |
| R8-22 | ✅ Implemented per tracker; no recurrence identified in this review | `docs/AUDIT-R8-REMEDIATION.md:383-388`. |
| R8-23 | ❌ Open | High-severity dependency findings are still not blocking under the critical-only threshold; see R9-19 and `docs/AUDIT-R8-REMEDIATION.md:383-388`. |

## Source-verifiable vs non-repository evidence

### Source-verifiable

- The findings above are based on the application source, repository documentation, workflow/configuration files, and UI templates at audited commit `ea5df9c99f4edbe433181361af935f57430ed715`.
- The R8 remediation tracker records implementation statuses cited above; tracker status alone does not prove live production behavior.
- Build, test, i18n, and dependency-audit results are recorded in the validation table. The audit results are snapshots from the registries available on 2026-10-06, not a guarantee that advisory counts remain unchanged.
- The current `HEAD` is a direct child of the audited commit and changes only `badges/pre-release.png`; the source files and cited line references are unchanged.

### Non-repository or operational evidence

- GitHub branch-protection rules, required-review settings, CODEOWNERS enforcement, workflow secrets, app permissions, and Azure OIDC scope were not available to inspect.
- Production proxy behavior, deployed security headers, Auth0 tenant account/scope behavior, provider response contents, and external-service quotas were not available to verify.
- Actual production logs, backups, restoration tests, and their retention/deletion behavior were not available. The repository's evidence checklist does not itself prove that controls have been performed.
- Signed processor agreements, international-transfer safeguards, and legal assessments were not available. This audit makes no legal-compliance determination.
- Whether retained or transferred question/answer identifiers can be linked to participants remains unknown; no conclusion about their personal-data classification is made here.

## Remediation status

No application or configuration fixes were made as part of this read-only audit. Findings are ready for `/deliver R9`; remediation should be separately planned and approved. The prior-round tracker is [AUDIT-R8-REMEDIATION.md](./AUDIT-R8-REMEDIATION.md).
