# Deep Audit — Round 10

| | |
|---|---|
| **Repository** | `handballbelgium-referees/ref-test-management` |
| **Date** | 2026-10-08 |
| **Commit audited** | `735d8cf6a8b402713d914c88c83502277a883b89` (`main`) |
| **Previous reports** | [R2](AUDIT-R2.md), [R3](AUDIT-R3.md), [R4](AUDIT-R4.md), [R5](AUDIT-R5.md), [R6](AUDIT-R6.md), [R7](AUDIT-R7.md), [R8](AUDIT-R8.md), [R9](AUDIT-R9.md) |
| **Scope** | Backend domain, jobs, privacy/GDPR technical controls, logging and documentation parity; GraphQL authentication/authorization, security headers and Auth0; frontend correctness, i18n and accessibility; CI/CD, release integrity and software supply chain; README audit index |
| **Method** | Fresh, read-only source review by four area reviewers; exact-line reconciliation against the baseline; prescribed build, test, dependency and recent CI checks |

---

## Executive summary

**Production readiness: NOT READY — eight Medium findings affect privacy, authorization, UI initialization and accessibility; fourteen additional Low findings identify control and documentation gaps.** The prescribed .NET and UI tests pass, but passing tests do not resolve the source-level risks. The dependency audits also report High-severity advisories; the current Critical-only blocking policy is an accepted R9 residual risk and is not counted as a new finding.

**GDPR evidence status (mixed):** Repository code and records document technical data flows, retention/anonymization behavior and privacy-request handling. They do not establish legal compliance or the deployed provider, access, retention, backup, restore, transfer, or processor-contract controls. The repository's operational-evidence checklist explicitly leaves several of these items pending.

### Verdict by area

| Area | Verdict |
|---|---|
| Backend domain logic and invariants | Needs remediation — export delivery can race with changes to the associated participant email. |
| GraphQL authentication and authorization | Needs remediation — an ID-scoped subscription is intentionally public, and permission revocation can remain stale in cache. |
| Background jobs, transactions and logging | Needs remediation — export delivery needs a stronger association check before handing data to the provider. |
| Frontend correctness, i18n and accessibility | Needs remediation — initialization and multiple accessibility-state/announcement gaps remain. |
| GDPR/privacy technical controls | Needs remediation — a privacy-export race remains; external legal and operational evidence is not established by source review. |
| CI/CD and supply chain | Needs remediation — workflow provenance does not explicitly bind the application payload to a digest, and the declared UI npm version is not enforced. |
| Documentation parity | Needs remediation — privacy records disagree on providers and legal basis; prior audit links target the wrong directory. |

| Severity | Count |
|---|---:|
| 🔴 Critical | 0 |
| 🟠 High | 0 |
| 🟡 Medium | 8 |
| ⚪ Low | 14 |

## Findings

| # | Severity | Area | Finding |
|---|---|---|---|
| R10-01 | Medium | Privacy/jobs | Export can include records no longer associated with the verified address |
| R10-02 | Medium | GraphQL authorization | Revoked permissions can remain effective during the cache window |
| R10-03 | Medium | Frontend/accessibility | Pass/fail outcome has no explicit text |
| R10-04 | Medium | Frontend/configuration | Unsupported non-empty language configuration can break UI startup |
| R10-05 | Medium | Frontend/accessibility | Create-form validation text is not associated with its field |
| R10-06 | Medium | Frontend/accessibility | Detail tabs do not expose the selected tab |
| R10-07 | Medium | Frontend/accessibility | Filter and sorting disclosures omit expanded state |
| R10-08 | Medium | Frontend/accessibility | Pending export-request state has no explicit announcement |
| R10-09 | Low | GraphQL/security | Public time-extension subscription exposes ID-scoped test activity |
| R10-10 | Low | Privacy/documentation | IHF outbound scoring fields are not reconciled with the processing inventory |
| R10-11 | Low | Privacy/documentation | Azure is absent from the documented provider list |
| R10-12 | Low | Privacy/documentation | Assessment legal-basis wording is inconsistent across records |
| R10-13 | Low | Privacy/operations | Privacy challenge rate limits are instance-local |
| R10-14 | Low | Frontend/privacy | Export confirmation copy overstates delivery certainty |
| R10-15 | Low | Frontend/accessibility | Selected status filter is conveyed only visually |
| R10-16 | Low | Frontend/i18n | Filter-scroll accessible name is hard-coded in English |
| R10-17 | Low | Frontend/correctness | Delete confirmation can dispatch repeated mutations |
| R10-18 | Low | Frontend/i18n | German locale does not follow its documented escape convention |
| R10-19 | Low | Documentation | README audit links target the wrong directory |
| R10-20 | Low | CI/CD/supply chain | Release provenance does not explicitly bind an application-payload digest |
| R10-21 | Low | CI/CD/supply chain | Declared npm version is not enforced in CI |
| R10-22 | Low | CI/CD | CodeQL C# build uses a different .NET SDK than release validation |

### Medium findings

#### R10-01 — Export can include records no longer associated with the verified address

- **Severity:** Medium
- **Area:** Privacy/jobs
- **Evidence:** `RefTestManagement.Api/BackgroundServices/JobHandlers/PersonalDataExportDeliveryEmailJobHandler.cs:83-89,140-143` — the handler loads records by email and its final check revalidates the export request, not that each loaded record still belongs to that address. `RefTestManagement.Domain/RefTests/RefTest.cs:375-398` permits a participant email to change.
- **Confidence:** 9/10

If a record's email changes after it is loaded for an export, the still-valid request can pass the final check and the data can be sent to the previously verified address. This is a distinct, adjacent scenario to R9's in-flight email/erasure and terminal-job cleanup findings.

**Recommendation:** Invalidate pending exports when a participant email changes, and recheck record ownership immediately before handing export data to the provider.

#### R10-02 — Revoked permissions can remain effective during the cache window

- **Severity:** Medium
- **Area:** GraphQL authorization
- **Evidence:** `RefTestManagement.Api/Services/PermissionSnapshotService.cs:37,73-74` — permission snapshots have a four-minute lifetime and a still-fresh cached snapshot is returned.
- **Confidence:** 9/10

A user whose permission is revoked may continue to pass authorization checks using a cached snapshot until it expires. The actual exposure depends on when the relevant permission is next evaluated.

**Recommendation:** Invalidate cached snapshots on permission changes where feasible, or set and document an explicit revocation-latency objective with a corresponding cache lifetime.

#### R10-03 — Pass/fail outcome has no explicit text

- **Severity:** Medium
- **Area:** Frontend/accessibility
- **Evidence:** `RefTestManagement.Ui/src/app/ref-test/take/components/ref-test-results/ref-test-results.html:4-27,34-37` — the outcome changes the icon's color/shape, followed by a generic translated heading and subtitle; no textual pass/fail result is rendered.
- **Confidence:** 9/10

Assistive-technology users are not explicitly told whether the participant passed or failed. The score alone may not communicate the outcome.

**Recommendation:** Render a translated textual pass/fail result that is available to assistive technology.

#### R10-04 — Unsupported non-empty language configuration can break UI startup

- **Severity:** Medium
- **Area:** Frontend/configuration
- **Evidence:** `RefTestManagement.Api/Program.cs:80-85` defaults an empty configured list but does not validate non-empty codes; `RefTestManagement.Api/Graphql/Queries/RefTestQueries.cs:163-164` returns the configured values; `RefTestManagement.Ui/src/app/services/language-config.ts:62-64,83-84,111-112` filters unsupported codes and throws if none remain. `docs/CONFIGURATION.md:200` lists the supported language codes.
- **Confidence:** 9/10

A non-empty configuration containing only unsupported codes can leave the frontend with no enabled language and cause its application initializer to fail. The deployed configuration was not inspected, so this is a conditional risk rather than a claim that a live deployment is affected.

**Recommendation:** Validate configured language codes at API startup and reject unsupported values before serving them to the UI.

#### R10-05 — Create-form validation text is not associated with its field

- **Severity:** Medium
- **Area:** Frontend/accessibility
- **Evidence:** `RefTestManagement.Ui/src/app/ref-tests/create/create-ref-tests.html:235-253` — the number-of-questions input has no association to the conditional validation paragraph through `aria-describedby` or an equivalent error relationship.
- **Confidence:** 8/10

Assistive-technology users may not receive the validation explanation when navigating to the invalid field.

**Recommendation:** Give the error text an ID and associate it with the input; expose dynamically appearing errors appropriately.

#### R10-06 — Detail tabs do not expose the selected tab

- **Severity:** Medium
- **Area:** Frontend/accessibility
- **Evidence:** `RefTestManagement.Ui/src/app/ref-tests/detail/ref-test-detail.html:318-350` — tab roles and visual `routerLinkActive` styling are present, but the active tab does not expose `aria-selected`.
- **Confidence:** 9/10

Screen-reader users cannot reliably identify which detail route is selected from the tab semantics.

**Recommendation:** Expose the active route as the selected tab and associate tabs with their panels, or use navigation semantics if these links are not intended to behave as tabs.

#### R10-07 — Filter and sorting disclosures omit expanded state

- **Severity:** Medium
- **Area:** Frontend/accessibility
- **Evidence:** `RefTestManagement.Ui/src/app/ref-tests/list/components/filters/ref-test-filters-card/ref-test-filters-card.html:11-15` and `RefTestManagement.Ui/src/app/ref-tests/list/components/filters/sorting-panel/sorting-panel.html:2-14` — buttons toggle panels without exposing `aria-expanded`.
- **Confidence:** 9/10

Assistive technology is not given the disclosures' current expanded/collapsed state.

**Recommendation:** Bind `aria-expanded` to each panel's state and associate each disclosure button with its panel.

#### R10-08 — Pending export-request state has no explicit announcement

- **Severity:** Medium
- **Area:** Frontend/accessibility
- **Evidence:** `RefTestManagement.Ui/src/app/privacy/components/personal-data-export/request/personal-data-export-request.html:79-91` — the submit button is disabled and its label changes while the request is pending, but there is no live region or status role.
- **Confidence:** 8/10

A screen-reader user may not be informed that the request is in progress. No browser/screen-reader testing was performed; the finding is based on the source's lack of an explicit status announcement.

**Recommendation:** Expose the pending message through a translated status/live region.

### Low findings

#### R10-09 — Public time-extension subscription exposes ID-scoped test activity

- **Severity:** Low
- **Area:** GraphQL/security
- **Evidence:** `RefTestManagement.Api/Graphql/Subscriptions/RefTestSubscriptions.cs:25-37` explicitly documents the subscription as public and accepts a RefTest ID without a participant credential. `RefTestManagement.Api/Graphql/Subscriptions/RefTestTimeExtended.cs:5-10` returns the test ID, new time limit, added minutes and event time; `RefTestManagement.Infrastructure/Services/RefTestSubscriptionService.cs:159,161-170` publishes to an ID-specific topic.
- **Confidence:** 8/10

Anyone who knows a RefTest ID can observe its time-extension metadata. The subscription is intentionally public for test takers; repository evidence does not establish whether the ID can be linked to a person outside the API, and the payload contains no name or email.

**Recommendation:** Require a participant-bound credential for the subscription, or explicitly document and accept the RefTest ID as the capability for observing this event.

#### R10-10 — IHF outbound scoring fields are not reconciled with the processing inventory

- **Severity:** Low
- **Area:** Privacy/documentation
- **Evidence:** `RefTestManagement.Application/Services/IHFRulesQuestionsService.cs:185-197` sends question IDs, selected-answer IDs and scoring configuration. `docs/GDPR/GDPR-ROPA.md:26` says no personal data is sent, while `docs/GDPR/GDPR-PROCESSOR-REGISTER.md:21` lists the outbound fields and leaves their legal classification unconfirmed.
- **Confidence:** 9/10

The repository does not consistently distinguish the technical payload from the unresolved legal classification of those fields. This does not establish that the IDs constitute personal data.

**Recommendation:** Reconcile the field inventory and document the technical data flow separately from the controller's legal classification.

#### R10-11 — Azure is absent from the documented provider list

- **Severity:** Low
- **Area:** Privacy/documentation
- **Evidence:** `docs/PRIVACY.md:43-49` lists Auth0, Brevo and IHF Rules Questions as service providers; `docs/GDPR/GDPR-ROPA.md:15,22` identifies Azure as hosting.
- **Confidence:** 9/10

The repository's privacy provider list and processing record are not aligned. The participant-facing notice and actual deployment were not independently verified.

**Recommendation:** Reconcile the provider lists and verify the participant-facing notice against the confirmed deployment.

#### R10-12 — Assessment legal-basis wording is inconsistent across records

- **Severity:** Low
- **Area:** Privacy/documentation
- **Evidence:** `docs/PRIVACY.md:24-28` states explicit consent for an optional assessment; `docs/GDPR/GDPR-LEGAL-BASIS-RECORD.md:15-17` describes consent and/or service operation as the proposed basis.
- **Confidence:** 10/10

The repository records do not state a consistent legal basis. This audit makes no legal determination about which basis is appropriate.

**Recommendation:** Obtain the controller's legal determination and align the notice and internal records.

#### R10-13 — Privacy challenge rate limits are instance-local

- **Severity:** Low
- **Area:** Privacy/operations
- **Evidence:** `RefTestManagement.Api/Services/PrivacyChallengeRateLimiter.cs:17-24,37-44` creates in-memory partitioned limiters on the service instance; no shared limiter is shown.
- **Confidence:** 9/10

If the API is scaled across instances, a client's effective per-window allowance can multiply across them. The deployed instance count and topology were not verified.

**Recommendation:** Use a shared limiter when scaling out, or document and verify a single-instance deployment constraint.

#### R10-14 — Export confirmation copy overstates delivery certainty

- **Severity:** Low
- **Area:** Frontend/privacy
- **Evidence:** `RefTestManagement.Ui/src/app/privacy/components/personal-data-export/confirmation/personal-data-export-confirmation.ts:41-47` treats a confirmed request as confirmation success; `RefTestManagement.Ui/public/i18n/en.json:757` says the PDF “will be emailed.” `docs/PRIVACY.md:85` states delivery can fail and provider acceptance does not prove receipt.
- **Confidence:** 8/10

Participants may understand confirmation of their request as assurance that the export was delivered.

**Recommendation:** Clarify that the request is confirmed and delivery will be attempted, without promising delivery before it is known.

#### R10-15 — Selected status filter is conveyed only visually

- **Severity:** Low
- **Area:** Frontend/accessibility
- **Evidence:** `RefTestManagement.Ui/src/app/ref-tests/list/components/filters/status-filter-tabs/status-filter-tabs.html:20-27` applies selected styling based on `selectedStatus()` but does not expose a pressed/selected state.
- **Confidence:** 8/10

Assistive-technology users may not be able to identify the active status filter.

**Recommendation:** Expose the selected state, for example with `aria-pressed`.

#### R10-16 — Filter-scroll accessible name is hard-coded in English

- **Severity:** Low
- **Area:** Frontend/i18n
- **Evidence:** `RefTestManagement.Ui/src/app/ref-tests/list/components/filters/status-filter-tabs/status-filter-tabs.html:7` sets `aria-label="Scroll filters left"` instead of using a translation.
- **Confidence:** 10/10

Users of the Dutch, French or German UI receive an English accessible name for this control.

**Recommendation:** Use a translated accessible label.

#### R10-17 — Delete confirmation can dispatch repeated mutations

- **Severity:** Low
- **Area:** Frontend/correctness
- **Evidence:** `RefTestManagement.Ui/src/app/ref-tests/list/components/dialogs/delete-ref-tests-dialog/delete-ref-tests-dialog.html:78-89` leaves the confirm button enabled while loading; `RefTestManagement.Ui/src/app/shared/utils/dialog-utils.ts:27-29` has no in-flight guard before invoking the callback; `RefTestManagement.Ui/src/app/ref-tests/list/services/ref-test-operation-manager.ts:57-70` connects it to deletion.
- **Confidence:** 9/10

Repeated confirmation clicks can dispatch concurrent delete requests for the same selection.

**Recommendation:** Disable confirmation while loading and guard repeated confirmation in the dialog operation helper.

#### R10-18 — German locale does not follow its documented escape convention

- **Severity:** Low
- **Area:** Frontend/i18n
- **Evidence:** `RefTestManagement.Ui/public/i18n/de.json:13` contains raw non-ASCII text; `.github/instructions/ui.instructions.md:16` says `de.json` uses `\u` escapes and notes that the hook warns about raw non-ASCII.
- **Confidence:** 10/10

This is a locale-format consistency issue only; raw Unicode is valid JSON, and no user-visible localization or parsing defect was identified.

**Recommendation:** Keep German locale non-ASCII characters escaped as documented.

#### R10-19 — README audit links target the wrong directory

- **Severity:** Low
- **Area:** Documentation
- **Evidence:** `README.md:324-331` links R2-R9 to `docs/AUDIT-R*.md`; the checked-in reports are under `docs/Audits/` (for example, `docs/Audits/AUDIT-R9.md`), and `docs/AUDIT-R9.md` does not exist.
- **Confidence:** 10/10

The README links to prior audit reports are broken. This report adds a correctly located R10 link but does not alter the existing rows.

**Recommendation:** Update the existing report links to use `docs/Audits/`.

#### R10-20 — Release provenance does not explicitly bind an application-payload digest

- **Severity:** Low
- **Area:** CI/CD/supply chain
- **Evidence:** `.github/workflows/beta-release.yml:302,359-367` and `.github/workflows/stable-release.yml:329,539-543` write and compare source/toolchain/runner provenance metadata, but the workflow-defined check contains no explicit digest calculation or verification binding the application payload to that metadata.
- **Confidence:** 9/10

The workflow-defined validation would not detect a changed payload if the provenance metadata remained unchanged. This finding is limited to the checked-in workflow check; GitHub artifact-service integrity controls were not assessed.

**Recommendation:** Record a cryptographic digest for the published application payload and verify it before deployment.

#### R10-21 — Declared npm version is not enforced in CI

- **Severity:** Low
- **Area:** CI/CD/supply chain
- **Evidence:** `RefTestManagement.Ui/package.json:26` declares `npm@12.2.0`; `.github/workflows/beta-release.yml:13,52-54` pins Node and runs `npm ci`, but does not select or verify npm 12.2.0.
- **Confidence:** 9/10

Dependency installation and builds may use a different npm version than the manifest declares, reducing reproducibility. The repository does not establish which npm version ran in a particular deployment.

**Recommendation:** Explicitly install and verify npm 12.2.0 in workflows that install UI dependencies.

#### R10-22 — CodeQL C# build uses a different .NET SDK than release validation

- **Severity:** Low
- **Area:** CI/CD
- **Evidence:** `.github/workflows/codeql.yml:49` configures SDK `10.0.401`; `.github/workflows/beta-release.yml:12` configures release validation for `10.0.303`.
- **Confidence:** 8/10

C# analysis builds under a different SDK than the release build, which can reduce build and scan parity. No resulting build or scan failure was established.

**Recommendation:** Align the CodeQL SDK with release validation, or document and verify the intended difference.

## Validation performed

| Check | Result |
|---|---|
| `dotnet restore RefTestManagement.slnx` | Passed; all projects were up to date. |
| `dotnet build RefTestManagement.slnx --configuration Release --no-restore` | Passed; 0 warnings, 0 errors. |
| `dotnet test --solution RefTestManagement.slnx --configuration Release --no-build -p:TestingPlatformShowTestsFailure=true` | Passed; 441 succeeded, 0 failed, 0 skipped. |
| Root `npm ci` | Passed. |
| `RefTestManagement.Ui`: `npm ci` | Passed. |
| `RefTestManagement.Ui`: `npm run check:i18n` | Passed; 731 keys in each locale, no missing or orphaned keys. |
| `RefTestManagement.Ui`: `npm test -- --watch=false` | Passed; 36 test files, 211 tests. |
| `RefTestManagement.Ui`: `npm run build -- --configuration production` | Passed with a bundle-budget warning: initial bundle 609.27 kB versus 600.00 kB budget (9.27 kB over). |
| Root `npm audit --json` | Exit code 1 due to advisories: 2 Moderate, 11 High, 0 Critical (13 total). |
| `RefTestManagement.Ui`: `npm audit --json` (additional check) | Exit code 1 due to advisories: 14 High, 0 Critical. |
| Recent GitHub Actions failures (`gh run list --limit 20`; failed logs inspected) | Six failures were on `chore/audit-r9`, not the audited `main` baseline: two dependency-audit gate failures, three `npm ci` lockfile/package mismatch failures, and one release-safety assertion failure. The runs were [37666957124](https://github.com/handballbelgium-referees/ref-test-management/actions/runs/37666957124), [37667693894](https://github.com/handballbelgium-referees/ref-test-management/actions/runs/37667693894), [37678228772](https://github.com/handballbelgium-referees/ref-test-management/actions/runs/37678228772), [37678395312](https://github.com/handballbelgium-referees/ref-test-management/actions/runs/37678395312), [37679236234](https://github.com/handballbelgium-referees/ref-test-management/actions/runs/37679236234), and [37679383918](https://github.com/handballbelgium-referees/ref-test-management/actions/runs/37679383918). |

## Previous-round status

The R9 remediation tracker assigns all 41 findings to WP-87 through WP-101 and marks the reviewed work packages implemented. Current source was rechecked for the prior findings most relevant to R10:

| Prior finding | Status | Current evidence |
|---|---|---|
| R9-06, R9-07 | Implemented per tracker; R10-01 is an adjacent but distinct email-change race, not the prior erasure/final-job-cleanup scenario. | `docs/Remediations/AUDIT-R9-REMEDIATION.md:94-95`; current export handler and email-update evidence in R10-01. |
| R9-19, R9-20 | WP-96 implemented a Critical-only blocking policy and audits both dependency trees. The non-blocking High-advisory policy is an accepted residual risk, not a new R10 finding. | `docs/Remediations/AUDIT-R9-REMEDIATION.md:293,306-310`; current npm audit results above. |
| R9-22 | WP-95 records the accepted technical non-linkability classification for the IHF-side payload. R10 review observed retained outcomes but found no new evidence establishing linkability, so this is not repeated as a finding. | `docs/Remediations/AUDIT-R9-REMEDIATION.md:266-268`; current domain and event sources. |
| R9-39 | WP-100 implemented source/toolchain/runner provenance. R10-20 and R10-21 are distinct residual controls: payload-digest binding and enforcement of the declared npm version. | `docs/Remediations/AUDIT-R9-REMEDIATION.md:399-401,412-416`; current beta/stable release workflows and UI manifest. |
| R9-17, R9-32, R9-35 | No direct recurrence was identified in the reviewed controls. The question-change announcement and language-menu semantics are present; R10-08 and R10-16 concern different controls. | Current `RefTestManagement.Ui/src/app/ref-test/take/components/question-card/question-card.html:2-8`, `RefTestManagement.Ui/src/app.html:54-62,96-109`, and the R10 frontend evidence above. |

## Source-verifiable vs non-repository evidence

### Source-verifiable

- The findings and their cited implementation/documentation evidence are present at the audited commit.
- R9's tracker records the implementation statuses noted above; these are tracker claims, not proof of live deployment controls.
- Local restore/build/test and UI validation outcomes are recorded in the validation table.
- The local dependency audit counts are 2 Moderate and 11 High in the root tree, and 14 High in the UI tree. The CI policy remains Critical-only for blocking.
- The checked-in operational evidence checklist marks restore, logging, access, redaction and export evidence as pending (`docs/GDPR/GDPR-OPERATIONS-EVIDENCE.md:16-19,32-36`).

### Non-repository or operational evidence

- The deployed Auth0 issuer/audience, tenant scopes and grants, Azure settings, provider contracts, international-transfer safeguards, backup/log retention, access roles, restore drills and actual processor behavior were not inspected. Repository records identify evidence still pending, but do not prove the live controls are absent.
- The controller's legal basis and the legal classification/linkability of retained outcomes and IHF-side fields require an appropriate controller determination. This report makes no legal conclusion.
- The deployed `EnabledLanguages` values and API instance count/topology were not checked.
- GitHub branch/ruleset protections, release environment approvals, secrets, and artifact-service integrity settings were not independently verified. The inspected Actions logs are operational evidence from `chore/audit-r9`, not test results for the audited `main` commit.
- No browser or assistive-technology testing was performed; accessibility findings are based on source review.

## Remediation status

R10 findings are ready for `/deliver R10`. Each remediation requires its own approved plan and work packages; this report does not authorize application-code or configuration changes.
