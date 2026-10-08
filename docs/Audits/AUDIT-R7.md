# Deep Audit — Round 7

| | |
|---|---|
| **Repository** | `handballbelgium-referees/ref-test-management` |
| **Date** | 2026-09-22 |
| **Commit audited** | `6f7dd30` (`main`) |
| **Previous reports** | [`AUDIT.md`](./AUDIT.md) (R1) · [`AUDIT-R2.md`](./AUDIT-R2.md) · [`AUDIT-R3.md`](./AUDIT-R3.md) · [`AUDIT-R4.md`](./AUDIT-R4.md) · [`AUDIT-R5.md`](./AUDIT-R5.md) · [`AUDIT-R6.md`](./AUDIT-R6.md) |
| **Remediation tracker** | [`AUDIT-R7-REMEDIATION.md`](./AUDIT-R7-REMEDIATION.md) |
| **Scope** | Full repository audit: backend, frontend, GDPR/privacy, CI/CD & supply chain, tests, and documentation parity |
| **Method** | Fresh independent read-only audit of current `main`, plus reproducible validation runs. `AUDIT-R6.md` was used only as a formatting template. |

---

## Executive summary

**Production readiness: READY — the previously identified R7 release-pipeline and operational hardening gaps have now been remediated, and the platform is ready for production sign-off.**

This round re-audited the repository from scratch at current `main`. The backend and frontend both built and tested cleanly. Authorization coverage, domain state checks, job/outbox design, and privacy-erasure mechanics are materially strong. I did not find a current critical-severity code path exposing participant data without authorization.

The original residual risks recorded in this report (CI/CD gating, artifact provenance, token persistence defaults, security-header parity, startup blocking behavior, and forwarded-header/rate-limit safety defaults) have been addressed by the completed R7 remediation wave.

**GDPR compliance status (source-verifiable): improved evidence, but still not fully verifiable end-to-end.** The codebase shows substantial technical controls (consent versioning, anonymization/retention flow, redaction safeguards, privacy notice endpoint). Since this report was drafted, controller-facing evidence templates were added and partially populated (`docs/GDPR-PROCESSOR-REGISTER.md`, `docs/GDPR-OPERATIONS-EVIDENCE.md`), and core controller records were added (`docs/GDPR-ROPA.md`, `docs/GDPR-LEGAL-BASIS-RECORD.md`, `docs/GDPR-DSAR-PROCEDURE.md`), including known hosting regions and processor context. For a single-owner hobby project processing real personal data, this now represents a practical **minimum compliance baseline**; full verification still remains blocked by pending operational/legal evidence fields (final DPA proofs, transfer safeguards, DSAR operating records, and concrete backup/logging access/retention evidence).

### Verdict by area

| Area | Verdict |
|---|---|
| Backend domain logic & invariants | Strong; status transitions, anonymization checks, and concurrency protections are explicit |
| GraphQL authn/authz | Strong field/policy coverage; no current anonymous `node(id:)` exposure path |
| Background jobs & transactions | Good overall; outbox-style behavior and unit-of-work coupling are implemented |
| Frontend correctness & i18n | Strong baseline; tests pass and locale parity check is clean |
| GDPR/privacy technical controls | Good in code; external compliance evidence remains unverifiable in repo |
| CI/CD & supply chain | Needs remediation (release gating and provenance weaknesses) |
| Documentation parity | Several accuracy drifts remain |

| Severity | Count |
|---|---:|
| 🔴 Critical | 0 |
| 🟠 High | 2 |
| 🟡 Medium | 4 |
| ⚪ Low | 2 |

## Findings

| # | Severity | Area | Finding |
|---|---|---|---|
| R7-01 | 🟠 High | CI/CD quality gate | Release workflows can deploy artifacts without running test suites |
| R7-02 | 🟠 High | Release integrity | Beta build/deploy checks out `main`, not the just-created release tag |
| R7-03 | 🟡 Medium | CI/CD token handling | Checkout credentials remain persisted while dependency graph executes |
| R7-04 | 🟡 Medium | Security headers | No HTTP CSP/HSTS emission despite UI comment claiming headers are sent by API |
| R7-05 | 🟡 Medium | Backend/Auth0 resiliency | Auth0 management token “cache” is ineffective due transient service lifetime |
| R7-06 | 🟡 Medium | Startup resilience + docs parity | Permission sync blocks startup path while README claims it is non-blocking |
| R7-07 | ⚪ Low | Availability/config defaults | Default forwarded-header trust + IP-based limiter can collapse users into one bucket |
| R7-08 | ⚪ Low | Documentation accuracy | README claims OnPush/lint/doc index coverage that current repo does not provide |

---

## High severity

### R7-01 — Release workflows can deploy without running test suites

**Severity:** 🟠 High  
**Area:** CI/CD quality gate  
**Evidence:** `.github/workflows/beta-release.yml:85-188`, `.github/workflows/stable-release.yml:145-212,262-300`, contrasted with test execution only in `.github/workflows/pr.yml:69-95`

Both release workflows build and deploy, but neither executes frontend nor backend tests before deployment. The only automated test gate exists in PR checks. A push-based beta release and manual stable release should still have pre-deploy test gates, especially since suites are fast and already maintained.

**Impact:** A non-PR or late-branch change can be promoted to testing/production without any test verification in the release pipeline itself.

**Recommendation:** Add explicit release-pipeline test jobs (backend + frontend + i18n parity) and make build/deploy depend on them.

### R7-02 — Beta build/deploy is sourced from `main` instead of the released tag

**Severity:** 🟠 High  
**Area:** Release integrity  
**Evidence:** `.github/workflows/beta-release.yml:15-17,94-104`

The beta release job computes `new-release-version`, but the build job checks out `ref: main` and infers a tag later via `git describe`. This can diverge from the exact release commit if `main` advances between jobs.

**Impact:** Artifact provenance is weakened: deployed bits may not exactly match the semantic-release tag tied to that run.

**Recommendation:** Build from `ref: v${{ needs.release.outputs.new-release-version }}` (same pattern already used in stable release: `.github/workflows/stable-release.yml:154-159`).

---

## Medium severity

### R7-03 — Checkout credentials persist while dependency graph executes

**Severity:** 🟡 Medium  
**Area:** CI/CD token handling  
**Evidence:** `.github/workflows/beta-release.yml:46-50,57-68`, `.github/workflows/stable-release.yml:102-108,117-129`, `.github/workflows/pr.yml:22-31`

`actions/checkout` defaults to persisted credentials. In release jobs, that includes an app-minted write-capable token before `npm ci` and `npx semantic-release`. This is partially mitigated by short-lived GitHub App tokens and action pinning, but still enlarges blast radius if a dependency execution path is compromised.

**Impact:** Increased token-exposure window during third-party tool execution.

**Recommendation:** Set `persist-credentials: false` by default; enable persisted credentials only in the specific step that needs `git push`.

### R7-04 — HTTP CSP/HSTS are not emitted by API despite UI claim

**Severity:** 🟡 Medium  
**Area:** Security headers  
**Evidence:** `RefTestManagement.Api/Program.cs:303-310` (only `Referrer-Policy`, `X-Content-Type-Options`, `X-Frame-Options`), absence of `UseHsts` in `Program.cs`, `RefTestManagement.Ui/src/index.html:24-33`

The UI comment states these protections are sent as real HTTP headers by the API. In practice, CSP is present only as a meta tag in HTML. No HSTS middleware/header is configured.

**Impact:** Weaker transport/content hardening than documented; CSP coverage is narrower when delivered as meta and cannot protect non-HTML responses.

**Recommendation:** Emit CSP (and HSTS for non-development) as real response headers from the API, then align the comment with the actual behavior.

### R7-05 — Auth0 token cache is ineffective with transient typed client

**Severity:** 🟡 Medium  
**Area:** Backend/Auth0 resiliency  
**Evidence:** `RefTestManagement.Auth0/Services/Auth0ManagementService.cs:24-53` (instance fields `_accessToken`, `_tokenExpiry`), `RefTestManagement.Auth0/Auth0ServiceExtensions.cs:24-33` (`AddHttpClient<IAuth0ManagementService, Auth0ManagementService>`)

The class implements in-instance token caching, but the DI registration creates a transient typed client. Each resolution starts with empty cache state, causing repeated token acquisition.

**Impact:** Extra Auth0 M2M token traffic, more throttling/cost exposure, and slower permission/user lookup paths.

**Recommendation:** Move cache to shared scope (e.g., `IMemoryCache`/singleton cache collaborator) or change service lifetime/architecture so cache state is reused safely.

### R7-06 — Permission sync blocks startup despite non-blocking README claim

**Severity:** 🟡 Medium  
**Area:** Startup resilience + docs parity  
**Evidence:** `RefTestManagement.Api/BackgroundServices/PermissionSyncService.cs:18-35`, `RefTestManagement.Api/Program.cs:168`, `README.md:277`

`PermissionSyncService` is an `IHostedService` that awaits network sync in `StartAsync`. Failures are swallowed, but startup still waits for completion/timeout. README describes this as “non-blocking.”

**Impact:** Slower or timeout-prone cold starts during Auth0 outages; documentation mismatch can hide operational behavior.

**Recommendation:** Move sync out of startup critical path (background delayed run) or update docs to reflect blocking startup behavior.

---

## Low severity

### R7-07 — Default forwarded-header trust config can distort per-client rate limiting

**Severity:** ⚪ Low  
**Area:** Availability/config defaults  
**Evidence:** `RefTestManagement.Api/appsettings.json:67-76`, `RefTestManagement.Api/Program.cs:120-129,251-258,268-294`

Rate limiting keys on `RemoteIpAddress`. Default config enables rate limiting while leaving `KnownProxies`/`KnownNetworks` empty. Behind reverse proxies, this can group many users into one limiter bucket unless deployment config is overridden.

**Impact:** Potential shared throttling under certain deployments (availability degradation, not data exposure).

**Recommendation:** Make forwarded-header trust config mandatory when rate limiting is enabled, or fail fast when unsafe defaults are detected.

### R7-08 — README contains current-state inaccuracies (OnPush/lint/docs index)

**Severity:** ⚪ Low  
**Area:** Documentation accuracy  
**Evidence:** `README.md:87`, `README.md:291`, `README.md:308-310`; no `ChangeDetectionStrategy` usage in `RefTestManagement.Ui/src` (repo search), no lint step in `.github/workflows/pr.yml:49-95`

README claims “OnPush change detection throughout” and “Build/lint/test validation,” while the code/workflow do not currently substantiate these statements. The docs table also omits newer audit rounds.

**Impact:** Contributor/operator expectations can drift from reality.

**Recommendation:** Update README claims or implement the advertised behavior.

---

## Validation performed

| Check | Result |
|---|---|
| `dotnet restore RefTestManagement.slnx` | ✅ Passed |
| `dotnet build RefTestManagement.slnx --configuration Release --no-restore` | ✅ Passed, 0 warnings, 0 errors |
| `dotnet test --solution RefTestManagement.slnx --configuration Release --no-build -p:TestingPlatformShowTestsFailure=true` | ✅ 180 passed, 0 failed |
| `npm ci` (repo root) | ✅ Completed; audit reported 7 vulnerabilities (6 high, 1 moderate) |
| `npm ci` (`RefTestManagement.Ui`) | ✅ Completed |
| `npm run check:i18n` (`RefTestManagement.Ui`) | ✅ 637 keys in each locale, 0 missing/orphaned |
| `npm run test -- --watch=false --no-progress` (`RefTestManagement.Ui`) | ✅ 37 passed / 4 files |
| `npm run build -- --configuration production` (`RefTestManagement.Ui`) | ✅ Passed (initial total 586.88 kB raw / 149.94 kB transfer) |
| `npm audit --json` (repo root) | ⚠️ 7 advisories: 0 critical, 6 high, 1 moderate |
| GitHub Actions review (`list_workflow_runs` + `get_job_logs`) | ✅ Retrieved failed-job logs; latest historical failure reviewed was PR-title length (`header-max-length`) |

---

## Remediation status

All R7 remediation packages are complete:

1. ✅ **WP-65 (R7-01):** release-pipeline test gates before deploy.
2. ✅ **WP-66 (R7-02):** beta artifacts built from immutable release tag.
3. ✅ **WP-67 (R7-03):** checkout credential persistence tightened by default.
4. ✅ **WP-68 (R7-04):** CSP/HSTS emitted as HTTP headers; docs/comments aligned.
5. ✅ **WP-69 (R7-05):** Auth0 management token caching moved to shared lifetime.
6. ✅ **WP-70 (R7-06):** permission sync moved off startup blocking path; docs aligned.
7. ✅ **WP-71 (R7-07):** forwarded-header/rate-limit safety defaults enforced.
8. ✅ **WP-72 (R7-08):** README parity gaps closed.

## Source-verifiable vs non-repository evidence

**Source-verifiable in this audit:** all finding evidence lines above, workflow behavior, code-level privacy/auth logic, all command outputs in the validation table, newly documented controller inputs in `docs/GDPR-PROCESSOR-REGISTER.md` / `docs/GDPR-OPERATIONS-EVIDENCE.md` (Azure regions, Auth0 tenant location tier context, Brevo tier context, and IHF no-personal-data statement), and the presence of baseline controller records in `docs/GDPR-ROPA.md`, `docs/GDPR-LEGAL-BASIS-RECORD.md`, and `docs/GDPR-DSAR-PROCEDURE.md`.

**Not verifiable from source alone:** legal basis determination, signed/accepted DPA evidence links, formal transfer safeguard artifacts, production reverse-proxy topology/trust chain, backup-erasure execution evidence, DSAR operational SLA records/procedures, and environment branch-protection settings as configured in GitHub.

**GDPR compliance status summary:** **still not complete for full compliance verification**. Technical safeguards are materially present, and a practical minimum documentation baseline for a hobby project with real-user personal data is now in place; however, several controller/legal/operational proof points remain open.
