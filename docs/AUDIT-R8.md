# Deep Audit — Round 8

| | |
|---|---|
| **Repository** | `handballbelgium-referees/ref-test-management` |
| **Date** | 2026-10-04 |
| **Commit audited** | `14af8cfa19fee71dd87c99ce84ed316fa2ca1321` (`main`) |
| **Previous reports** | [R2](./AUDIT-R2.md), [R3](./AUDIT-R3.md), [R4](./AUDIT-R4.md), [R5](./AUDIT-R5.md), [R6](./AUDIT-R6.md), [R7](./AUDIT-R7.md) |
| **Scope** | Backend domain/jobs/privacy/logging/docs; GraphQL authorization, security headers and Auth0; frontend correctness/i18n/accessibility/docs; CI/CD, release integrity and supply chain |
| **Method** | Fresh read-only source and documentation review by four area auditors, reconciliation against the audited baseline, validation commands, and recent CI failure review |

---

## Executive summary

**Production readiness: NOT READY —** the build and test suites pass, but this round found material privacy, authorization, and release-integrity risks. The root npm audit reported 17 high-severity vulnerable package entries, including privileged release tooling, and the latest beta-release run failed at its release step because the run reported missing release-app credentials. GitHub secret configuration and deployment controls were not inspected.

**GDPR evidence status (mixed):** the repository contains retention, anonymization, and withdrawal mechanisms, but a free-text rejection reason is not cleared or redacted during erasure. The operational evidence and processor registers also leave backup/restore, logging, and processor-contract evidence pending or unconfirmed. No legal or overall GDPR-compliance conclusion is drawn from repository code.

### Verdict by area

| Area | Verdict |
|---|---|
| Backend domain logic and invariants | Needs remediation — caught per-item failures can persist partial state |
| GraphQL authentication and authorization | Needs remediation — permission revocation and Auth0 audience boundaries need stronger enforcement |
| Background jobs, transactions, and logging | Needs remediation — terminal withdrawal jobs and unredacted free text can leave incomplete or identifying records |
| Frontend correctness, i18n, and accessibility | Needs remediation — language metadata and selected-state semantics are incomplete |
| GDPR/privacy technical controls | Needs remediation — erasure gaps remain; external operational evidence is incomplete |
| CI/CD and supply chain | Needs remediation — credentials, release ordering, tag identity, and dependency controls need hardening |
| Documentation parity | Needs remediation — security headers, Auth0 setup, and language-preference behavior are not fully documented |

| Severity | Count |
|---|---:|
| 🔴 Critical | 0 |
| 🟠 High | 0 |
| 🟡 Medium | 11 |
| ⚪ Low | 12 |

The npm registry classified 17 root dependency entries as high severity; the report rates their release-tool exposure as a **Medium risk** because they are development/release dependencies and advisory-specific reachability was not established. A separate UI `npm ci` summary reported 12 high vulnerabilities; their package paths were not individually assessed.

## Findings

| # | Severity | Area | Finding |
|---|---|---|---|
| R8-01 | 🟡 Medium | Backend domain logic | Caught per-item failures can commit partial state |
| R8-02 | 🟡 Medium | Privacy jobs | Exhausted withdrawal batches recover only after another request |
| R8-03 | 🟡 Medium | GDPR erasure | Free-text rejection reasons survive anonymization |
| R8-04 | 🟡 Medium | Authentication | Permission revocations do not refresh existing cookie claims |
| R8-05 | 🟡 Medium | Auth0 authorization | Approver lookup ignores resource-server identity |
| R8-06 | 🟡 Medium | Frontend accessibility | Document language remains English after locale changes |
| R8-07 | 🟡 Medium | Frontend accessibility | Selected answers expose no programmatic state |
| R8-08 | 🟡 Medium | CI/CD credentials | Write-scoped checkout credentials persist during dependency/build steps |
| R8-09 | 🟡 Medium | Release integrity | Release jobs build mutable tags without confirming the tested SHA |
| R8-10 | 🟡 Medium | Release integrity | Release publication precedes candidate tests |
| R8-11 | 🟡 Medium | Supply chain | High npm advisories include privileged release tooling |
| R8-12 | ⚪ Low | Rate limiting | Rate-limit partitioning can trust an unverified raw client-IP header |
| R8-13 | ⚪ Low | GraphQL authorization | Question IDs are available without the question-specific permission |
| R8-14 | ⚪ Low | Security headers | CSP permits inline scripts |
| R8-15 | ⚪ Low | Documentation parity | Security-header reference omits CSP and HSTS |
| R8-16 | ⚪ Low | Documentation parity | Quick Start omits required Auth0 Management API settings |
| R8-17 | ⚪ Low | Background jobs | Permission sync has no retry after failure |
| R8-18 | ⚪ Low | Frontend i18n | Initial language can fall back to disabled English |
| R8-19 | ⚪ Low | Frontend accessibility | The “Close” accessible name is English-only |
| R8-20 | ⚪ Low | Frontend accessibility | Stream ID label is not associated with its input |
| R8-21 | ⚪ Low | Documentation parity | README overstates language-preference scope |
| R8-22 | ⚪ Low | CI/CD dependencies | NuGet lockfiles are not enforced in CI |
| R8-23 | ⚪ Low | CI/CD dependencies | Dependency advisories are not a blocking PR check |

### Medium findings

#### R8-01 — Caught per-item failures can commit partial state

- **Severity:** Medium
- **Area:** Backend domain logic
- **Type:** Confirmed defect
- **Evidence:** `RefTestManagement.Api/Graphql/Mutations/Reset/RefTestResetMutations.cs:195-201,217-229` stages job cancellation before `Revive()` and saves after catching an item failure; `RefTestManagement.Domain/RefTests/RefTest.cs:582-588` shows `Revive()` can reject a non-expired test. In creation, `RefTestManagement.Api/Graphql/Mutations/Creation/RefTestCreationMutations.cs:61,71-74,312-321` adds RefTests, catches invitation-preparation failures, and later saves the context; `RefTestManagement.Infrastructure/Services/JobEnqueueService.cs:115-127` can fail before adding the invitation job.

A failed revive can therefore commit staged job cancellations. An invitation-preparation exception can leave a persisted RefTest without its invitation job.

**Recommendation:** Validate before staging side effects and roll back failed items, or otherwise commit each RefTest and its required outbox changes atomically.

#### R8-02 — Exhausted withdrawal batches recover only after another request

- **Severity:** Medium
- **Area:** Privacy jobs
- **Type:** Missing control
- **Evidence:** `RefTestManagement.Api/BackgroundServices/JobHandlers/PrivacyWithdrawalBatchJobHandler.cs:56-63` throws when a target fails; `RefTestManagement.Domain/Jobs/Job.cs:93-96` makes a job terminal at its retry limit; `RefTestManagement.Api/BackgroundServices/BackgroundJobService.cs:109-112` selects only Pending/Processing jobs. `RefTestManagement.Api/BackgroundServices/PrivacyWithdrawalCleanupService.cs:16-26,78-95` cleans expired challenges and completed targets, while recovery is triggered from `RefTestManagement.Api/Services/PrivacyWithdrawalRequestService.cs:81-88,219-224`.

A repeatedly failing target can remain identifiable and unerased after the batch job becomes terminal, until another request or confirmation triggers recovery.

**Recommendation:** Add scheduled reconciliation and alerting for incomplete withdrawal batches, independent of participant resubmission.

#### R8-03 — Free-text rejection reasons survive anonymization

- **Severity:** Medium
- **Area:** GDPR erasure
- **Type:** Confirmed defect
- **Evidence:** `RefTestManagement.Domain/RefTests/RefTest.cs:130,222-236,620-631` stores `RejectionReason` but does not clear it in `Anonymize()`. `RefTestManagement.Domain/RefTests/Events/RefTestSimpleEvents.cs:11-14` serializes the reason, and `RefTestManagement.Infrastructure/Services/RefTestPrivacyErasureService.cs:214-222` applies `AuditPiiRedactor`. Its key set at `RefTestManagement.AuditLog/AuditPiiRedactor.cs:24-38,85` does not include `reason`. `docs/PRIVACY.md:58` states that nothing identifying remains after delete.

If staff include participant-identifying details in a rejection reason, that text remains in the anonymized record and retained audit event.

**Recommendation:** Clear the reason during anonymization and redact the audit key, or replace free text with non-identifying structured reason codes.

#### R8-04 — Permission revocations do not refresh existing cookie claims

- **Severity:** Medium
- **Area:** Authentication
- **Type:** Missing control
- **Evidence:** `RefTestManagement.Api/SecurityStartup.cs:54-73` copies Auth0 permission claims into the cookie identity at token validation. `RefTestManagement.Api/Controllers/AccountController.cs:25-28` and `RefTestManagement.Security/TaskPermissionHandler.cs:25-28` use the current principal's claims; `RefTestManagement.Ui/src/app/auth/services/permissions.ts:15-17,27,34-38` refreshes from that endpoint.

A permission revoked after login can remain effective for an existing cookie session until its principal is renewed or rejected. The deployed session duration was not verified.

**Recommendation:** Revalidate or refresh permissions during cookie-ticket validation, and ensure the UI refresh obtains updated grants rather than the same cookie claims.

#### R8-05 — Approver lookup ignores resource-server identity

- **Severity:** Medium
- **Area:** Auth0 authorization
- **Type:** Conditional risk
- **Evidence:** `RefTestManagement.Auth0/Services/Auth0ManagementService.cs:69-70,87-88,117-118,285-286` matches only `PermissionName` even though `ResourceServerIdentifier` is available. `RefTestManagement.Api/BackgroundServices/JobHandlers/ApprovalNotificationEmailJobHandler.cs:24-26,41-43` sends approval emails to the matched users.

If the live Auth0 tenant reuses a permission name on another resource server, users granted that permission there may be treated as approvers and receive participant details. The live tenant configuration was not verified.

**Recommendation:** Filter both role and direct-user grants by the configured resource-server audience before matching permission names.

#### R8-06 — Document language remains English after locale changes

- **Severity:** Medium
- **Area:** Frontend accessibility
- **Type:** Confirmed defect
- **Evidence:** `RefTestManagement.Ui/src/index.html:2` declares `lang="en"`; `RefTestManagement.Ui/src/app/app.ts:102-104` changes the translation language and browser-local storage without updating the document language.

Assistive technologies can continue using English pronunciation rules for non-English content.

**Recommendation:** Set the document's `lang` attribute on initialization and whenever the selected language changes.

#### R8-07 — Selected answers expose no programmatic state

- **Severity:** Medium
- **Area:** Frontend accessibility
- **Type:** Missing control
- **Evidence:** `RefTestManagement.Ui/src/app/ref-test/take/components/answer-option/answer-option.html:1-9,12-18` changes styling and renders a checkmark for selection, but exposes no pressed or checked state.

Screen-reader users can activate an answer but cannot reliably review which answer is selected before submission.

**Recommendation:** Expose selection with `aria-pressed` or native checkbox semantics.

#### R8-08 — Write-scoped checkout credentials persist during dependency/build steps

- **Severity:** Medium
- **Area:** CI/CD credentials
- **Type:** Risk
- **Evidence:** `.github/workflows/beta-release.yml:38-50,57-68` and `.github/workflows/stable-release.yml:94-107,117-128` mint write-scoped tokens, pass them to checkouts, and then run `npm ci`; `.github/workflows/codeql.yml:3-5,21-26,32-33,41-42` checks out pull-request code with `security-events: write` before C# autobuild. These checkouts do not disable credential persistence. The workflow convention at `.github/instructions/workflows.instructions.md:8` says to disable it unless a reviewed step needs credentials; the R7 tracker marks WP-67 implemented at `docs/AUDIT-R7-REMEDIATION.md:63-77`.

If dependency-install or build code is compromised, it may be able to reuse persisted checkout credentials to alter repository or release state.

**Recommendation:** Disable checkout credential persistence and expose write credentials only to the specific release or push step that needs them.

#### R8-09 — Release jobs build mutable tags without confirming the tested SHA

- **Severity:** Medium
- **Area:** Release integrity
- **Type:** Risk
- **Evidence:** `.github/workflows/beta-release.yml:71-72,88-99,156-170` and `.github/workflows/stable-release.yml:131-132,148-159,218-229` pass a release version and independently check out its tag for validation and build; the handoff does not compare a commit SHA.

If a tag is retargeted between jobs, the built or deployed commit can differ from the commit that passed validation. Tag rules and protections were not inspected.

**Recommendation:** Pass the validated commit SHA through job outputs, use it for subsequent checkouts, and verify the tag still resolves to that SHA.

#### R8-10 — Release publication precedes candidate tests

- **Severity:** Medium
- **Area:** Release integrity
- **Type:** Missing control
- **Evidence:** `.releaserc.mjs:106` configures the GitHub release plugin. The beta and stable release jobs run semantic-release before their validation jobs (`.github/workflows/beta-release.yml:57-72,88-89,156-160,192-208`; `.github/workflows/stable-release.yml:117-132,148-149,215-219,251-267`).

A tag or GitHub release can be published before candidate tests pass. Deployment is gated on the later validation/build chain, but the release itself may already exist when validation fails.

**Recommendation:** Run the test gate before publication, or keep the release as a draft until validation passes.

#### R8-11 — High npm advisories include privileged release tooling

- **Severity:** Medium
- **Area:** Supply chain
- **Type:** Conditional risk
- **Evidence:** `package.json:23-29` declares direct release-tool dependencies; `.releaserc.mjs:101-106` uses semantic-release plugins; `.github/workflows/beta-release.yml:38-50,57-68` and `.github/workflows/stable-release.yml:94-107,117-128` install and run release tooling with write-scoped credentials. The root `npm audit --json` run reported 17 high-severity vulnerable package entries (4 direct, 13 transitive).

If an advisory affects the active release-tool path, exploitation during a release could compromise release or repository integrity. The report classifies this as Medium because the detailed advisory paths and exploitability were not established; npm's severity classification is not a reachability assessment.

**Recommendation:** Patch affected resolved versions and block privileged releases while high-severity advisories remain in the release-tool dependency graph.

### Low findings

#### R8-12 — Rate-limit partitioning can trust an unverified raw client-IP header

- **Severity:** Low
- **Area:** Rate limiting
- **Type:** Conditional risk
- **Evidence:** `RefTestManagement.Api/appsettings.json:79-84` leaves trusted headers and proxy/network lists empty by default. `RefTestManagement.Api/Program.cs:291-303` rejects the no-source configuration in non-development unless explicitly overridden, but permits any configured trusted header; the rate limiter uses the resolver at `RefTestManagement.Api/Program.cs:149-151`, and `RefTestManagement.Api/Services/ClientIpResolver.cs:16-20` reads that raw request header. Proxy/network allow-lists are configured separately at `RefTestManagement.Api/Program.cs:314-331`.

If a configured header is not overwritten or stripped by ingress, a client that can reach the API may vary its rate-limit partition key. The deployed ingress behavior was not verified. This partially carries forward R7-07; the default no-source guard is now fail-closed.

**Recommendation:** Require a known proxy/network for header-based keys, or enforce and document an ingress that overwrites the header and blocks direct API access.

#### R8-13 — Question IDs are available without the question-specific permission

- **Severity:** Low
- **Area:** GraphQL authorization
- **Type:** Confirmed defect
- **Evidence:** `RefTestManagement.Api/Graphql/Types/RefTestType.cs:31-32,109-111` allows the `questions` field with `ViewDetail` or `ViewDetailQuestions`. `RefTestManagement.Api/Graphql/Types/QuestionType.cs:16-21` does not authorize the ID field, while `docs/SECURITY.md:79` assigns the field to `view-detail-questions`.

A user with only `ref-tests:view-detail` can retrieve question IDs, although question content fields are separately gated.

**Recommendation:** Apply the intended question permission to the field and ID, or document an explicit ID-only exception.

#### R8-14 — CSP permits inline scripts

- **Severity:** Low
- **Area:** Security headers
- **Type:** Missing control
- **Evidence:** `RefTestManagement.Api/Program.cs:357,369` includes `'unsafe-inline'` in `script-src` and emits the CSP response header.

If script injection reaches the served origin, this policy does not block inline script execution.

**Recommendation:** Replace the inline-script allowance with nonces or hashes and remove `'unsafe-inline'` from `script-src`.

#### R8-15 — Security-header reference omits CSP and HSTS

- **Severity:** Low
- **Area:** Documentation parity
- **Type:** Documentation mismatch
- **Evidence:** `RefTestManagement.Api/Program.cs:369,377-379` emits CSP and enables HSTS outside Development; `docs/SECURITY.md:294-302` lists only three other response headers.

Reviewers using the security reference do not see the complete header policy. Header emission resolves the main R7-04 defect; the documentation portion remains incomplete.

**Recommendation:** Add CSP and environment-specific HSTS to the security-header inventory.

#### R8-16 — Quick Start omits required Auth0 Management API settings

- **Severity:** Low
- **Area:** Documentation parity
- **Type:** Documentation mismatch
- **Evidence:** `README.md:223-232` omits the Management API M2M settings; `docs/CONFIGURATION.md:126-129` marks them required. `RefTestManagement.Auth0/Auth0ServiceExtensions.cs:20-21` defaults missing values to empty strings, which `RefTestManagement.Auth0/Services/Auth0ManagementService.cs:33-36` sends in a Management API token request.

Following Quick Start alone leaves permission sync and permission-based approver lookup without required credentials.

**Recommendation:** Add both Management API settings to Quick Start or explicitly document the affected functionality.

#### R8-17 — Permission sync has no retry after failure

- **Severity:** Low
- **Area:** Background jobs
- **Type:** Missing control
- **Evidence:** `RefTestManagement.Api/BackgroundServices/PermissionSyncService.cs:24-37` makes one sync call, catches and logs failure, then exits.

A transient startup failure can leave new Auth0 permissions unsynchronized until another sync or manual action. The R7-06 startup-blocking behavior is resolved; this is a separate recovery gap.

**Recommendation:** Keep startup non-blocking but add a bounded retry or health alert for unsuccessful sync.

#### R8-18 — Initial language can fall back to disabled English

- **Severity:** Low
- **Area:** Frontend i18n
- **Type:** Confirmed defect
- **Evidence:** `RefTestManagement.Ui/src/app/services/language-config.ts:78` falls back to `'en'` when the browser language is not enabled; `docs/CONFIGURATION.md:153` documents configurable `EnabledLanguages`.

If English is disabled and the browser language is also disabled, the UI still selects English.

**Recommendation:** Fall back to the first enabled language or explicitly require English in configuration.

#### R8-19 — The “Close” accessible name is English-only

- **Severity:** Low
- **Area:** Frontend accessibility
- **Type:** Confirmed defect
- **Evidence:** `RefTestManagement.Ui/src/app/ref-test/take/take-ref-test.html:72` uses the literal `aria-label="Close"`.

Participants using another UI language receive an English accessible name for the progress-dismiss button.

**Recommendation:** Bind the label to a translated key.

#### R8-20 — Stream ID label is not associated with its input

- **Severity:** Low
- **Area:** Frontend accessibility
- **Type:** Missing control
- **Evidence:** `RefTestManagement.Ui/src/app/audit-logs/components/audit-log-filter/audit-log-filter.html:12-20` has a visible label without `for` and an input without `id`.

Assistive technology cannot reliably associate the visible label with the field; the translated placeholder is not a substitute for a persistent label.

**Recommendation:** Give the input an ID and connect the label with `for`.

#### R8-21 — README overstates language-preference scope

- **Severity:** Low
- **Area:** Documentation parity
- **Type:** Documentation mismatch
- **Evidence:** `README.md:85` says language preference persists “per user”; `RefTestManagement.Ui/src/app/app.ts:104` and `RefTestManagement.Ui/src/app/services/language-config.ts:70` store and read it through browser-local storage.

Readers may expect account-level persistence although the source shows browser-local storage.

**Recommendation:** Describe the preference as browser-local unless account-level persistence is intended.

#### R8-22 — NuGet lockfiles are not enforced in CI

- **Severity:** Low
- **Area:** CI/CD dependencies
- **Type:** Missing control
- **Evidence:** `Directory.Build.props:3` enables package lock files, while `.github/workflows/pr.yml:90-91`, `.github/workflows/beta-release.yml:201-202`, and `.github/workflows/stable-release.yml:260-261` use plain `dotnet restore` without `--locked-mode`.

CI can resolve a changed package graph without failing specifically on lockfile drift.

**Recommendation:** Use `dotnet restore --locked-mode` in PR and release workflows, and commit lockfile updates with dependency changes.

#### R8-23 — Dependency advisories are not a blocking PR check

- **Severity:** Low
- **Area:** CI/CD dependencies
- **Type:** Missing control
- **Evidence:** `.github/workflows/pr.yml:62-99` installs, builds, and tests without a dependency-review or explicit blocking audit step. `renovate.json:24-31,58-66` configures vulnerability-alert PRs but not a required failing CI status.

`npm ci` reports known advisories but does not fail solely because vulnerabilities were found; a PR can pass the repository-defined build and test checks while introducing a vulnerable dependency.

**Recommendation:** Add a blocking dependency-review or advisory audit check for npm and NuGet at a defined severity threshold.

## Validation performed

| Check | Result |
|---|---|
| `dotnet restore RefTestManagement.slnx` | Passed |
| `dotnet build RefTestManagement.slnx --configuration Release --no-restore` | Passed; 0 warnings, 0 errors |
| `dotnet test --solution RefTestManagement.slnx --configuration Release --no-build -p:TestingPlatformShowTestsFailure=true` | Passed; 304 passed, 0 failed |
| `npm ci` (repository root) | Passed; install summary reported 17 high vulnerabilities |
| `npm ci` (`RefTestManagement.Ui`) | Passed; install summary reported 12 high vulnerabilities |
| `npm run check:i18n` (`RefTestManagement.Ui`) | Passed; all locales expose the same 698 keys |
| `npm test -- --watch=false` (`RefTestManagement.Ui`) | Passed; 17 files, 96 tests |
| `npm run build -- --configuration production` (`RefTestManagement.Ui`) | Passed; initial bundle 604.93 kB exceeds the 600 kB warning by 4.93 kB, below the 1 MB error threshold |
| `npm audit --json` (repository root) | Exit code 1 because of 17 high entries: 4 direct, 13 transitive; 0 critical, moderate, low, or info |
| Recent GitHub Actions failures | `[Beta Pre-release run 37194496992](https://github.com/handballbelgium-referees/ref-test-management/actions/runs/37194496992)` failed on `main`; its release guard reported release-app credentials unavailable to the run. `[Pull Request Checks run 37194090626](https://github.com/handballbelgium-referees/ref-test-management/actions/runs/37194090626)` on a Renovate branch failed the README version-sync step. Secret settings were not inspected. |

The bundle-budget warning is retained as a validation note, not a separate finding: it is 0.82% over the warning threshold and below the configured error threshold.

## Previous-round status

| Prior finding | Status | Current evidence |
|---|---|---|
| R7-01 | ✅ Resolved | Build depends on validation and deployment depends on build: `.github/workflows/beta-release.yml:88-89,213-216`; `.github/workflows/stable-release.yml:148-149,320-323`. Publication-before-tests is a separate R8 finding. |
| R7-02 | ✅ Resolved | Validation/build check out the release tag rather than `main`: `.github/workflows/beta-release.yml:98-99,169-170`; `.github/workflows/stable-release.yml:158-159,228-229`. |
| R7-03 | 🔄 Partial | Some non-pushing checkouts disable persistence, but release jobs still pass write-scoped tokens to checkout before `npm ci`: `.github/workflows/beta-release.yml:38-50,57-63`; `.github/workflows/stable-release.yml:94-107,117-123`. |
| R7-04 | 🔄 Partial | CSP and non-development HSTS are now emitted by `RefTestManagement.Api/Program.cs:369,377-379`; `docs/SECURITY.md:294-302` still omits them from its header inventory. |
| R7-05 | ✅ Resolved | `Auth0ManagementTokenCache` is registered singleton and injected into the management service: `RefTestManagement.Auth0/Auth0ServiceExtensions.cs:24,26`; `RefTestManagement.Auth0/Services/Auth0ManagementService.cs:17,27`. |
| R7-06 | ✅ Resolved | Permission sync yields before its Auth0 call and catches failure so startup continues: `RefTestManagement.Api/BackgroundServices/PermissionSyncService.cs:22-37`; README describes it as non-blocking at `README.md:279`. |
| R7-07 | 🔄 Partial | Empty trusted-source defaults fail closed in non-development (`RefTestManagement.Api/appsettings.json:79-84`; `RefTestManagement.Api/Program.cs:291-303`), but configured raw headers are used directly for partitioning (`RefTestManagement.Api/Program.cs:149-151`; `RefTestManagement.Api/Services/ClientIpResolver.cs:16-20`). |
| R7-08 | ✅ Resolved | README now describes signal-based state and indexes R7 at `README.md:89,317`; its PR-check description corresponds to `.github/workflows/pr.yml:68-81`. |

## Source-verifiable vs non-repository evidence

### Source-verifiable

- Current source and documentation support all cited findings and prior-round statuses above.
- The audited baseline was captured with `git rev-parse HEAD` as `14af8cfa19fee71dd87c99ce84ed316fa2ca1321` on `main`; the working tree was clean before validation.
- The report's build, test, i18n, bundle, and npm audit results are from commands run during this audit. npm advisory severity and package attribution depend on the external registry data returned at that time.
- `docs/GDPR-OPERATIONS-EVIDENCE.md:15-20,33-37` marks backup/restore and App Service logging evidence pending. `docs/GDPR-PROCESSOR-REGISTER.md:18-20` leaves processor DPA/transfer evidence pending or unconfirmed.

### Non-repository or operational evidence

- Live Auth0 resource-server grants, Management API scopes, permission-name uniqueness, deployed cookie-session lifetime, and tenant settings were not inspected. The R8-05 impact is conditional on permission-name reuse.
- GitHub rulesets, immutable-tag protections, effective fork-PR token scopes, required checks, environment approvals, App installation grants, and release-app secret configuration were not inspected.
- Production ingress behavior that overwrites or strips client-IP headers was not verified.
- Processor agreements, transfer terms, backup/restore drills and permissions, log retention/access, and deployed GDPR operations require controller/provider evidence. Repository documentation alone does not establish legal compliance.
- No live browser/assistive-technology session or deployment-specific enabled-language configuration was tested.

## Remediation status

No remediation code changes were made as part of this audit. Findings are ready for `/deliver R8`; each remediation requires its own approved plan and work packages.
