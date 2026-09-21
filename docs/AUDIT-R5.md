# Deep Audit & GDPR Review

| | |
|---|---|
| **Repository** | `handballbelgium-referees/ref-test-management` |
| **Commit** | `8acefb8` (`main`) |
| **Date** | 21 September 2026 |
| **Scope** | Backend, GraphQL authorization, participant flow, persistence, jobs, logging, CI/CD, frontend, accessibility, and GDPR controls |
| **Method** | Read-only static review of current source/configuration, prior-audit re-verification, targeted searches, release build, and automated test execution |

## Executive summary

**Overall: 🟡 conditionally ready.** The current branch contains substantial hardening since the earlier audit rounds: the Relay node bypass for participant records remains closed, erasure anonymizes before deletion and redacts audit data, background-job claiming is atomic, email logging is redacted, and the build/test baseline is healthy. I found no Critical or High-severity issue in the reviewed code.

Four findings were identified in the fifth audit. The remediation wave now addresses them in code:
participant withdrawal wording reflects anonymization, authorized batch mutations return safe
errors with redacted diagnostics, report-job erasure is selective with explicit legacy quarantine,
and release workflows are GitHub App-only. Deployment and controller-owned GDPR evidence remain
separate from this source-level closure.

Repository evidence cannot establish legal GDPR compliance. The application has meaningful technical controls, but controller documentation, processor agreements, international-transfer safeguards, backup erasure, incident response, and operational data-subject-request evidence remain outside the codebase.

| Severity | Count |
|---|---:|
| 🔴 Critical | 0 |
| 🟠 High | 0 |
| 🟡 Medium | 3 |
| ⚪ Low | 1 |

## Verdict by area

| Area | Verdict |
|---|---|
| Domain model & business logic | Good; expiration and retention predicates are now explicit and tested |
| GraphQL authorization | Good on reviewed entry points; participant fields are intentionally anonymous only through the token flow |
| Background jobs & transactions | Good claiming and erasure design; report-job association remains too broad |
| GDPR / privacy | Technically substantial but not transparent enough in the withdrawal UX |
| Frontend correctness | Good targeted coverage; production build succeeds |
| Accessibility | Shared dialog infrastructure is materially improved; broader manual verification remains appropriate |
| CI/CD | Strong SHA pinning and scoped permissions; long-lived PAT fallback should be removed |
| Test coverage | Healthy targeted baseline, but several mutation error and deployment behaviors are not regression-tested |

## Findings

| # | Severity | Area | Finding |
|---|---|---|---|
| R5-01 | 🟡 Medium | GDPR transparency | Withdrawal link and confirmation dialog originally promised deletion although self-service withdrawal anonymizes and retains the record — **closed in code** |
| R5-02 | 🟡 Medium | Error handling / information disclosure | Batch approval, creation, and reset mutations originally returned raw exception messages — **closed in code** |
| R5-03 | 🟡 Medium | GDPR / jobs | Erasing one participant originally could cancel unrelated queued legacy report emails — **closed in code** |
| R5-04 | ⚪ Low | CI/CD credentials | Release workflows originally retained a long-lived `GH_PAT` fallback — **closed in code; deployment verification remains** |

## Medium severity

### R5-01 — Withdrawal UX promises a stronger result than the implementation performs

**Evidence:** `RefTestManagement.Ui/public/i18n/en.json:616,663` and equivalent keys in `nl.json`, `fr.json`, and `de.json`; `RefTestManagement.Ui/src/app/ref-test/components/withdraw-consent-dialog/withdraw-consent-dialog.html`; `RefTestManagement.Infrastructure/Services/RefTestPrivacyErasureService.cs:89-153`; `docs/PRIVACY.md`

The participant-facing link says “Withdraw consent and delete my data”. The confirmation dialog says the RefTest and all associated personal data will be “permanently deleted” and that the action cannot be undone. The self-service mutation instead calls `EraseAsync`: it anonymizes the RefTest, redacts participant audit data, clears/cancels relevant jobs, and deliberately retains the record for audit and lifecycle integrity. Hard deletion is a separate administrative `EraseAndDeleteAsync` operation.

The success wording was improved in the current branch, but the initial consent-withdrawal decision text still makes an inaccurate promise before the user confirms the action. This is a transparency defect, not evidence that the erasure implementation itself failed.

**Impact:** A data subject may reasonably believe that all records have been permanently removed when the application retains an anonymized row and audit metadata. That conflicts with accurate information under GDPR Articles 5(1)(a), 12 and 13 and makes the distinction between anonymization and deletion unclear.

**Recommendation:** Change all four locale files and the dialog copy to say that consent is withdrawn and identifying data is anonymized/redacted, while explaining that a retained anonymized record may remain for audit purposes. Add a UI regression test for the published withdrawal wording and keep the wording aligned with `docs/PRIVACY.md`.

### R5-02 — Several authorized batch mutations disclose raw exception details

**Evidence:** `RefTestManagement.Api/Graphql/Mutations/Approval/RefTestApprovalMutations.cs:60,102,185`; `RefTestManagement.Api/Graphql/Mutations/Creation/RefTestCreationMutations.cs:185,254,293`; `RefTestManagement.Api/Graphql/Mutations/Reset/RefTestResetMutations.cs:123,220`

Multiple per-item error paths assign `ex.Message` directly to GraphQL result fields, including `ApproveRefTestsError`, `RejectRefTestsError`, creation errors, reset errors, and messages prefixed with “Invitation email could not be prepared” or “Approval notification could not be prepared”. Exception messages can contain database/provider details, filesystem paths, configuration names, third-party response text, or recipient data. The report-email mutation was corrected in the prior remediation wave, but these sibling paths still use the unsafe pattern.

These mutations require staff permissions, so this is not an anonymous disclosure. It nevertheless exposes operational details and potentially personal data to any authorized caller and creates inconsistent error behavior across the API.

**Recommendation:** Log a redacted exception server-side with a correlation identifier and return stable, user-safe error codes/messages. Preserve useful domain validation messages where they are deliberately typed; do not serialize arbitrary provider or framework exception text. Add regression tests for each batch result shape.

### R5-03 — Privacy erasure over-cancels unrelated report jobs

**Evidence:** `RefTestManagement.Infrastructure/Services/RefTestPrivacyErasureService.cs:224-241`; `RefTestManagement.Application/Models/JobPayloads.cs:42-65`; `RefTestManagement.Api/Graphql/Mutations/Email/RefTestEmailMutations.cs:219-255`

Invitation, result, expiration, and approval payloads contain a RefTest identifier that can be matched during erasure. `ReportEmailPayload` instead contains a list of report rows and `RefTestReportPayloadData` has no `RefTestId`. The current fallback first selects every pending/processing `ReportEmail` job and then accepts every payload that lacks `"refTestId"` through `IsLegacyReportPayload`. Because current report payloads are legacy-shaped, erasing one participant can cancel all queued report emails, including reports for unrelated participants and unrelated administrators.

The broad cancellation avoids one earlier privacy failure mode—leaving participant data in an unidentifiable report payload—but it is not a correct association model. It can prevent legitimate reports from being delivered and does not prove that a report already handed to the provider can be recalled.

**Recommendation:** Add a participant association to report rows/jobs (preferably a normalized job-to-RefTest relation or a `RefTestId` in every row) and cancel only jobs containing the erased participant. Treat old unidentifiable payloads with an explicit migration/quarantine policy rather than cancelling all report jobs on every erasure. Add tests for two queued reports where erasing one participant leaves the other report intact.

## Low severity

### R5-04 — Release workflows keep a long-lived PAT fallback

**Evidence:** `.github/workflows/beta-release.yml:16-49`; `.github/workflows/stable-release.yml:16-49,68-91`

Both release workflows correctly prefer a short-lived GitHub App installation token, but checkout still uses `steps.release-token.outputs.token || secrets.GH_PAT`. The comments explicitly describe `GH_PAT` as a fallback until the App client is configured. A PAT is a long-lived human-linked credential with broader lifecycle and revocation risk than the installation token.

This is conditional and does not demonstrate an exposed secret. It leaves a weaker authentication path available in the most privileged workflows (`contents`, issues, and pull requests write).

**Recommendation:** Complete GitHub App rollout, remove the PAT fallback and its documentation, and fail early when the App credentials are absent. If a temporary fallback is unavoidable, scope it to a separately protected environment and set a removal deadline outside the workflow.

## Controls verified

- **Authentication:** Auth0 OIDC and JWT schemes use HTTP-only, `SameSite=Strict` cookies; the current startup configuration no longer requests `offline_access` or persists OIDC tokens.
- **Authorization:** Query and mutation roots use permission policies. `RefTestType` does not implement a Relay node resolver. The remaining global `node(id:)` path is implemented by `RefTestTitleType` and exposes title text only.
- **Participant access:** The anonymous `RefTest` field set is exercised through the token-based participant contract; the authorization schema test explicitly excludes token, correctness flags, and staff-only fields.
- **Invitation credential:** Tokens use cryptographic randomness and are excluded from audit interception.
- **Erasure:** Self-service erasure anonymizes, redacts participant audit fields, cancels relevant pending/processing work, and supports atomic anonymize-then-delete for administrative deletion.
- **Retention:** Explicit provider-translatable predicates cover completed, expired, pending-approval, and rejected records; live pending/in-progress records are not erased by the retention sweep.
- **Logging:** Email addresses are masked, exception text is email-redacted before job persistence/logging, and invitation tokens/URLs are not logged.
- **CI supply chain:** Reviewed workflow actions are SHA-pinned, PR validation handles untrusted title data through an environment variable, CodeQL is enabled, and workflow permissions are declared.
- **No tracked local artifacts or secrets:** the tracked-file check found no generated build directories, local database files, `.env` files, or populated production secrets.

## GDPR assessment

### Application-layer status

**Partially implemented; not enough evidence for a compliance declaration.** The code contains controls for consent-version capture, withdrawal, anonymization, retention, audit redaction, queued-job cancellation, and permission-based staff access. The R5 source findings are closed, but deployment and controller-owned GDPR evidence remain outstanding.

The application processes names, email addresses, invitation bearer tokens, progress, selected answers, scores, language, timestamps, administrator identity, audit events, and email/report payloads. These are personal data. Assessment answers and scores may require an additional risk and sensitivity assessment in the controller's context.

### Unverified controller obligations

Repository review cannot establish:

- a current record of processing activities, purposes, lawful bases, and retention justification;
- processor agreements and subprocessor terms for Auth0, Brevo, IHF Rules Questions, Azure hosting, logging, monitoring, and backups;
- international-transfer mechanisms and hosting/data-residency decisions;
- backup restoration and erasure procedures;
- incident response and GDPR Articles 33/34 notification procedures;
- verified data-subject identity handling, request tracking, and one-month SLA operations;
- whether controller identity, contact details, privacy-notice version, and effective date are current;
- deletion or recall guarantees after data has been transmitted to Brevo or other external processors.

These are evidence gaps and operational/legal obligations, not conclusions that the repository is non-compliant.

## Remediation work-package verification

The prior remediation wave is substantially reflected in the current source:

| Prior area | Current result |
|---|---|
| Unauthenticated participant Relay node | **Closed in code.** `RefTestType` no longer calls `ImplementsNode()`; only the title type is globally identifiable |
| OIDC token minimization | **Closed in code.** `offline_access` and `SaveTokens` are absent |
| Report mutation raw error | **Closed for that mutation.** The same pattern remains in sibling batch mutations (R5-02) |
| Forwarded-header/rate-limit handling | **Improved.** `XForwardedFor` and trusted proxy/network allow-lists are present; deployment values still require release evidence |
| Withdrawal wording | **Closed in code.** Link, dialog, success copy, and locale regression checks describe anonymization/redaction (R5-01) |
| Report-job erasure | **Closed in code.** Stable RefTest IDs are matched selectively; unidentifiable legacy payloads are quarantined and cleared (R5-03) |

The remediation tracker should add R5-01 through R5-04 as a new follow-up wave rather than marking the current audit fully closed.

## Rejected candidates

### Global Relay `node(id:)` participant exposure — not a current finding

`Program.cs` still enables global object identification, but that setting is not sufficient by itself to make every object a node. `RefTestType` has no `ImplementsNode()` configuration. `RefTestTitleType` is the only reviewed RefTest-related type implementing a node resolver, and it returns title text rather than participant details. The current authorization schema test also asserts the intended anonymous participant field contract separately from staff-only fields. A staging request should still be retained as release evidence, but the source does not support reopening the old R3 finding.

### OIDC refresh-token retention — closed

`SecurityStartup.cs` no longer requests `offline_access` or sets `SaveTokens = true`; no current finding was raised.

### Build/test failures caused by parallel invocation — environmental

An initial parallel build/test attempt contended for the same `obj` outputs and produced file-lock errors. Re-running sequentially passed; this was not treated as a repository defect.

## Validation performed

| Check | Result |
|---|---|
| `dotnet build RefTestManagement.slnx --configuration Release --no-restore` | ✅ Passed, 0 warnings, 0 errors |
| `dotnet test RefTestManagement.slnx --configuration Release --no-restore` | ✅ 172 passed, 0 failed |
| `npm exec ng -- test --watch=false --no-progress` | ✅ 36 passed across 3 files |
| `npm run build -- --configuration production` | ✅ Passed |
| `npm run check:i18n` | ✅ 637 keys in all four locales; no missing/orphaned keys |
| Tracked generated/local artifact and secret search | ✅ No in-scope tracked artifacts or populated secrets found |

## Recommended sequencing

1. Correct the withdrawal link/dialog wording and add a regression test (R5-01).
2. Replace raw exception response fields across approval, creation, and reset mutations with stable safe errors and redacted server logging (R5-02).
3. Give report jobs explicit participant associations and test that erasure is selective rather than global (R5-03).
4. Remove the `GH_PAT` release fallback after confirming GitHub App permissions in beta and stable environments (R5-04).
5. Before release, collect the external GDPR evidence listed above and run an anonymous staging `node(id:)` request against a representative participant/title identifier.

## Outstanding handover item

The code-level review is complete, but production readiness still depends on deployment and controller evidence. In particular, verify trusted proxy/network configuration for the enabled rate limiter, confirm the GitHub App token can perform the required release operations without `GH_PAT`, and record the controller's processor/backup/transfer/DSAR evidence.

## Method and limitations

This was a read-only repository audit. The review covered tracked application source, migrations, tests, documentation, workflows, configuration templates, and the current audit/remediation history. Generated/local artifacts were excluded unless directly relevant. Automated tests and builds ran against the current checkout; no production or staging database, identity tenant, Brevo account, Azure subscription, log sink, backup, or external IHF service was accessed. Static evidence can identify implementation behavior and gaps but cannot prove legal compliance, runtime deployment configuration, processor contracts, or operational effectiveness.
