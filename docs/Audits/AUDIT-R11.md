# Deep Audit — Round 11

| | |
|---|---|
| **Repository** | `handballbelgium-referees/ref-test-management` |
| **Date** | 2026-10-08 |
| **Commit audited** | `3b6c8dec9e402dd6339f9878d60e0f9da602418e` (`main`) |
| **Previous reports** | [R1](AUDIT.md), [R2](AUDIT-R2.md), [R3](AUDIT-R3.md), [R4](AUDIT-R4.md), [R5](AUDIT-R5.md), [R6](AUDIT-R6.md), [R7](AUDIT-R7.md), [R8](AUDIT-R8.md), [R9](AUDIT-R9.md), [R10](AUDIT-R10.md) |
| **Scope** | Backend domain, jobs, privacy and logging; GraphQL authorization, security headers and Auth0; frontend correctness, i18n and accessibility; CI/CD and software supply chain |
| **Method** | Fresh read-only review of the current source and related documentation, plus the validation checks listed below |

---

## Executive summary

**Production readiness: NOT READY — three Medium findings concern incomplete privacy evidence and a missing NuGet vulnerability audit; two Low findings concern accessibility and privacy-request documentation.** The backend and UI validation suites pass locally. This does not establish the external provider, legal, or deployed operational controls, and a recent pull-request workflow failure is noted below.

**GDPR evidence status (mixed):** The repository documents application data flows and privacy controls, but the operational evidence register still marks log-retention, log-access, deletion/rotation, backup/restore and related controls as pending or unknown. Processor terms and the legal-basis record remain provisional. Neither source review nor the repository establishes legal compliance or independently verifies live provider and hosting settings.

### Verdict by area

| Area | Verdict |
|---|---|
| Backend domain logic and invariants | No new code defect identified in the bounded review |
| GraphQL authentication and authorization | Strong within repository evidence; deployed Auth0 and ingress controls remain unverified |
| Background jobs, transactions, and logging | No new code defect identified in the bounded review |
| Frontend correctness, i18n, and accessibility | Needs remediation; two Low findings; locale key parity passed |
| GDPR/privacy technical controls | Needs remediation; operational evidence and controller decisions remain incomplete |
| CI/CD and supply chain | Needs remediation; no full-tree NuGet vulnerability audit is present |
| Documentation parity | Needs remediation; privacy evidence and request-route wording need attention |

| Severity | Count |
|---|---:|
| 🔴 Critical | 0 |
| 🟠 High | 0 |
| 🟡 Medium | 3 |
| ⚪ Low | 2 |

## Findings

| # | Severity | Area | Finding |
|---|---|---|---|
| R11-01 | Medium | GDPR/privacy operations | Operational privacy evidence remains incomplete |
| R11-02 | Medium | GDPR/privacy documentation | Processor and legal-basis records remain provisional |
| R11-03 | Medium | CI/CD and supply chain | CI has no full-tree NuGet vulnerability audit |
| R11-04 | Low | Frontend/accessibility | Export email required state is not exposed accessibly |
| R11-05 | Low | Documentation parity | Privacy instructions point to the notice, not directly to the request form |

### Medium findings

#### R11-01 — Operational privacy evidence remains incomplete

- **Severity:** Medium
- **Area:** GDPR/privacy operations
- **Evidence:** `docs/GDPR/GDPR-OPERATIONS-EVIDENCE.md:16-20,33-37` — backup retention needs portal evidence; restore roles, procedures and drills remain pending; App Service log-retention values, access roles, redaction checks, export destinations and deletion/rotation behavior are pending or unknown.
- **Confidence:** 10/10

These records do not verify who can access operational logs and backups, how long they are retained, or how restoration and deletion are handled. This is an evidence and operational-control gap; the repository cannot establish the live Azure configuration.

**Recommendation:** Record and periodically review the controller-approved hosting configuration, access/retention evidence, and restore/cleanup verification in the existing operations register.

#### R11-02 — Processor and legal-basis records remain provisional

- **Severity:** Medium
- **Area:** GDPR/privacy documentation
- **Evidence:** `docs/GDPR/GDPR-PROCESSOR-REGISTER.md:8,18-21` — provider status or terms and transfer details are described as assumed, pending, or unconfirmed; the IHF processor and personal-data classification are not established. `docs/GDPR/GDPR-LEGAL-BASIS-RECORD.md:12-16` labels participant execution and scoring as “Consent and/or service operation.”
- **Confidence:** 9/10

The register and legal-basis record do not show a settled controller determination for the open provider and participant-processing questions. The R10 remediation tracker records the related privacy/documentation work as partial pending owner/controller confirmation (`docs/Remediations/AUDIT-R10-REMEDIATION.md:160-163`).

**Recommendation:** Obtain and record controller review of the provider relationships, terms and legal bases, then align the privacy notice and internal registers to those decisions. This audit makes no legal conclusion about adequacy.

#### R11-03 — CI has no full-tree NuGet vulnerability audit

- **Severity:** Medium
- **Area:** CI/CD and software supply chain
- **Evidence:** `.github/workflows/pr.yml:17-28` limits dependency review to Critical severity; `.github/workflows/pr.yml:109-120` restores, builds and tests .NET dependencies but does not audit the restored NuGet dependency tree for known vulnerabilities.
- **Confidence:** 8/10

A vulnerable NuGet package already present in the dependency graph may therefore pass these validation jobs unless another control detects it. Repository configuration does not establish whether an external dependency-monitoring control is enabled.

**Recommendation:** Add a full-tree NuGet vulnerability audit to PR and release validation, or document the intended equivalent control and its coverage.

### Low findings

#### R11-04 — Export email required state is not exposed accessibly

- **Severity:** Low
- **Area:** Frontend/accessibility
- **Evidence:** `RefTestManagement.Ui/src/app/privacy/components/personal-data-export/request/personal-data-export-request.html:54-62` does not mark the email input as required or expose `aria-required`; the form's required validator is defined in `RefTestManagement.Ui/src/app/privacy/components/personal-data-export/request/personal-data-export-request.ts:20-23`.
- **Confidence:** 9/10

The form validates the field, but assistive technology may not communicate its required state before submission.

**Recommendation:** Expose the required state programmatically on the email input, consistent with the equivalent privacy-withdrawal field.

#### R11-05 — Privacy instructions point to the notice, not directly to the request form

- **Severity:** Low
- **Area:** Documentation parity
- **Evidence:** `docs/PRIVACY.md:75` describes the self-service form as being at `/privacy`; the request form route is `RefTestManagement.Ui/src/app/app.routes.ts:19`, `/privacy/export-request`, and the privacy notice links to that route at `RefTestManagement.Ui/src/app/privacy/privacy-notice.html:101-105`.
- **Confidence:** 9/10

Users following the instructions arrive at the notice page and must follow its link to reach the export request form.

**Recommendation:** Clarify that `/privacy` is the notice and that it links to the request form at `/privacy/export-request`.

## Validation performed

| Check | Result |
|---|---|
| `dotnet restore RefTestManagement.slnx` | Passed |
| `dotnet build RefTestManagement.slnx --configuration Release --no-restore` | Passed; 0 warnings, 0 errors |
| `dotnet test --solution RefTestManagement.slnx --configuration Release --no-build -p:TestingPlatformShowTestsFailure=true` | Passed; 450 passed, 0 failed |
| `npm ci` (repository root) | Passed |
| `npm ci` (`RefTestManagement.Ui`) | Passed |
| `npm run check:i18n` (`RefTestManagement.Ui`) | Passed; all four locales have 733 keys with identical key sets |
| `npm test -- --watch=false` (`RefTestManagement.Ui`) | Passed; 37 files and 213 tests |
| `npm run build -- --configuration production` (`RefTestManagement.Ui`) | Passed; initial bundle exceeded the 600 kB budget by 9.27 kB |
| `npm audit --json` (repository root) | Exit 1; 11 High, 2 Moderate, 0 Critical advisories |
| Recent GitHub Actions failures | Of the recent failures inspected, run [37773395517](https://github.com/handballbelgium-referees/ref-test-management/actions/runs/37773395517) on `feat/audit-r10` failed one Angular title-autocomplete test (`searchError()` remained false); this was not the audited `main` baseline. The local UI suite passed. |

## Previous-round status

| Prior finding | Status | Current evidence |
|---|---|---|
| R10-09 through R10-12 | Partial; controller confirmation remains outstanding | `docs/Remediations/AUDIT-R10-REMEDIATION.md:160-163`; current processor and legal-basis records at `docs/GDPR/GDPR-PROCESSOR-REGISTER.md:8,18-21` and `docs/GDPR/GDPR-LEGAL-BASIS-RECORD.md:12-16`. R11-02 records the still-provisional evidence. |
| R10-13 | Operationally partial; not repeated as a new finding | The tracker says production rollout remains blocked pending shared Redis configuration and smoke testing (`docs/Remediations/AUDIT-R10-REMEDIATION.md:190-193`); `docs/CONFIGURATION.md:97-100` documents that Local rate limiting is safe only for one API instance. |
| R10-20 through R10-22 | Implemented per tracker; no recurrence identified in this review | `docs/Remediations/AUDIT-R10-REMEDIATION.md:242-246`. |
| R9-19 and R9-20 | Accepted residual risk; not counted as a new finding | The R9 decision retains non-blocking High and lower dependency advisories (`docs/Remediations/AUDIT-R9-REMEDIATION.md:18-20,306-310`); the current root npm audit still reports 11 High and 2 Moderate advisories. |

## Source-verifiable vs non-repository evidence

### Source-verifiable

- The current commit, branch, tracked-file baseline, source locations, and CI workflow configuration are repository-verifiable.
- Local restore, build, test, i18n, production-build, package-install, and root npm audit results are recorded above.
- The README audit-doc table and the R10 remediation tracker were checked for previous-round status.

### Non-repository or operational evidence

- Live Azure log/backup retention, access roles, export destinations, restore procedures, deployment topology, and production behavior were not independently verified for this audit.
- Provider contracts/DPAs, international-transfer safeguards, Auth0 tenant settings, controller-approved legal bases, and external legal advice were not independently verified.
- The repository's operational and processor registers contain owner-provided statements and pending evidence; they are not independent proof of production configuration or legal compliance.
- GitHub branch-protection requirements and any external NuGet monitoring were not verified.

## Remediation status

No application or configuration fixes were made as part of this read-only audit. Findings are ready for `/deliver R11`; remediation requires a separate approved plan.
