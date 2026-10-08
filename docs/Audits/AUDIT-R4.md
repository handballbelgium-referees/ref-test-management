# Deep Audit & GDPR Review

| | |
|---|---|
| **Repository** | `handballbelgium-referees/ref-test-management` |
| **Commit** | `01bfc21` (`main`) |
| **Date** | 21 September 2026 |
| **Scope** | Backend, GraphQL authorization, participant flow, persistence, jobs, logging, CI/CD, frontend tests, and GDPR controls |
| **Method** | Static review of current source/configuration, targeted searches, full .NET build, and automated test execution |

## Executive summary

**Overall: 🟡 conditionally ready.** The current branch contains substantial remediation work: the prior unauthenticated `RefTest` Relay-node exposure is gone, erasure is transactional, audit data is redacted on retention, job claiming is atomic, and backend validation is healthy. I found no committed production secret and no Critical vulnerability in the reviewed code.

The main remaining concerns are privacy transparency, deployment-dependent rate limiting, unnecessary authentication-token retention, and exception-detail disclosure from one mutation. GDPR cannot be declared compliant from repository evidence alone: the software has useful controls, but the published participant wording does not consistently describe what the code actually does, and controller/processor obligations remain unverified.

| Severity | Count |
|---|---:|
| 🔴 Critical | 0 |
| 🟠 High | 0 |
| 🟡 Medium | 3 |
| ⚪ Low | 2 |

## Findings

### A-01 — Withdrawal confirmation falsely promises permanent deletion

**Severity:** 🟡 Medium  
**Area:** GDPR transparency / data-subject rights  
**Evidence:** `RefTestManagement.Ui/public/i18n/en.json:619` (same wording in `nl.json:619`, `fr.json:620`, `de.json:619`); `RefTestManagement.Ui/src/app/ref-test/take/state/ref-test.facade.ts:174-199`; `RefTestManagement.Infrastructure/Services/RefTestPrivacyErasureService.cs:135-153`

The participant-facing success message says the RefTest and personal data were “permanently deleted”. The withdrawal mutation calls `EraseAsync`, which anonymizes the row and retains the record/audit trail; permanent deletion is a separate `EraseAndDeleteAsync` operation used by staff. The UI therefore overstates the result of a GDPR action and conflicts with `docs/PRIVACY.md`, which correctly documents the two-step process.

**Impact:** A data subject is told a stronger result than the system performed. This undermines transparent information under GDPR Articles 5(1)(a), 12 and 13, and makes the consent-withdrawal/erasure workflow legally ambiguous.

**Recommendation:** Change all four translations to say that consent was withdrawn and identifying data was anonymized/redacted, or change the implementation to perform the explicitly intended deletion. The smallest correct fix is the wording change plus a regression test asserting the published message matches the chosen lifecycle.

### A-02 — Forwarded-header/rate-limit safety depends on non-default deployment configuration

**Severity:** 🟡 Medium  
**Area:** Availability / abuse resistance  
**Evidence:** `RefTestManagement.Api/Program.cs:121-130,255-285`; `RefTestManagement.Api/appsettings.json:72-75`; `docs/CONFIGURATION.md:242-258`

GraphQL rate limiting partitions by `Connection.RemoteIpAddress`. The application only trusts `X-Forwarded-For` from configured `KnownProxies` or `KnownNetworks`, but both default to empty arrays. Behind Azure App Service, nginx, Cloudflare, or another ingress, an installation that enables the limiter without supplying the real trusted proxy ranges collapses all clients into the proxy’s single bucket. Conversely, indiscriminate trust would allow client-controlled spoofing.

**Impact:** Legitimate anonymous participants can throttle one another, while the intended per-client abuse control is not achieved. This is deployment-sensitive rather than an unconditional code vulnerability.

**Recommendation:** Make the production deployment manifest/configuration supply the actual trusted proxy addresses/networks and add a startup/health check that fails or clearly warns when rate limiting is enabled behind a proxy without trusted-forwarder configuration. Keep the current allow-list model; do not trust forwarded headers from arbitrary clients.

### A-03 — OIDC refresh/access tokens are retained unnecessarily

**Severity:** 🟡 Medium  
**Area:** Security / GDPR data minimization  
**Evidence:** `RefTestManagement.Api/SecurityStartup.cs:44-53`

The OIDC flow requests `offline_access` and sets `SaveTokens = true`. The application copies permissions from the access token but does not otherwise use saved access or refresh tokens. Saving them places additional bearer credentials in the authentication ticket/cookie and extends the amount of authentication data retained by the application.

**Impact:** A stolen session cookie has a larger credential payload and potentially a refresh token, increasing blast radius. It also conflicts with data minimization when the tokens are not needed after sign-in.

**Recommendation:** Remove `offline_access` and `SaveTokens` unless a documented feature requires token renewal or downstream API calls. If retained, explicitly document the need, cookie-ticket protection, rotation, revocation, and maximum session lifetime.

### A-04 — Report mutation returns raw exception text to authorized clients

**Severity:** 🟡 Medium  
**Area:** Error handling / information disclosure  
**Evidence:** `RefTestManagement.Api/Graphql/Mutations/Email/RefTestEmailMutations.cs:255-263`

`SendReportAsync` catches every exception and puts `ex.Message` directly into the GraphQL response. Unlike the centralized GraphQL error filter, this path is not masked or normalized. Provider, database, filesystem, configuration, or serialization messages can reveal internal implementation details and may contain addresses or third-party response content.

**Impact:** Any caller with the send-report permission can receive operational details intended for server logs. Depending on the exception source, personal data can also cross the API boundary.

**Recommendation:** Log a redacted exception server-side and return a stable generic failure message with a correlation identifier. Preserve the existing success/error result shape, but never serialize arbitrary exception text.

### A-05 — Unit-test SQL helper interpolates an identifier into raw SQL

**Severity:** ⚪ Low  
**Area:** Test safety / maintainability  
**Evidence:** `RefTestManagement.UnitTests/PrivacyRetentionQueriesTests.cs:71-74`

The test helper uses `ExecuteSqlRawAsync` with an interpolated `column` identifier. Current callers pass hard-coded test column names, so this is not a production injection path, but the build emits EF1002 and the helper is unsafe if later reused with external input.

**Recommendation:** Replace the raw SQL helper with a fixed-column switch or provider-safe API. Do not suppress the warning without preserving the invariant that the identifier is constrained.

## Controls verified

- **Authentication:** Auth0 OIDC and JWT schemes use secure, HTTP-only, `SameSite=Strict` cookies (`SecurityStartup.cs:20-25`).
- **Authorization:** GraphQL query/mutation roots are policy-protected; the previous `RefTest` global-node exposure is no longer present. `RefTestTitle` still implements a public Relay node, but it exposes only title text, not participant data (`RefTestTitleType.cs:15-18`).
- **Invitation credential:** Invitation tokens are generated with `RandomNumberGenerator` and are excluded from the audit interceptor (`RefTest.cs`, `RefTestConfiguration.cs`).
- **Erasure:** `EraseAndDeleteAsync` always anonymizes before deleting, in a transaction; queued/processing jobs are cancelled and payloads are cleared (`RefTestPrivacyErasureService.cs:89-133`).
- **Retention:** RefTest retention uses a dedicated predicate and audit cleanup redacts `Data`, `ActorName`, and `ActorEmail` after the configured window (`PrivacyRetentionService.cs:45-62`, `AuditLogCleanupService.cs:77-121`).
- **Logging:** Email addresses are masked and exception text is wrapped before centralized logging; invitation tokens and URLs are not logged (`LogRedaction`, `ServiceLoggerMessages`, `EmailService`).
- **CI supply chain:** GitHub Actions are SHA-pinned, CodeQL covers C# and TypeScript, and the PR workflow invokes both backend and frontend test stages.
- **No repository secrets found:** configuration files contain placeholders; no private key or populated API secret was found in tracked source.

## GDPR assessment

### Application-layer status

**Partially implemented, not enough evidence for a compliance declaration.** The code demonstrates controls for consent versioning, withdrawal, anonymization, retention, audit redaction, queued-job cancellation, and access control. The participant UI wording in A-01 is materially inaccurate and must be corrected before claiming transparent consent/withdrawal handling.

The application also processes names, email addresses, invitation bearer tokens, progress, answers, scores, language, timestamps, administrator identity, audit events, and email/report payloads. These are personal data; scores and assessment answers may also require a documented sensitivity/risk assessment depending on the controller’s context.

### Unverified controller obligations

Repository review cannot establish:

- a current record of processing activities, documented purposes, and lawful-basis balancing;
- processor agreements and subprocessor terms for Auth0, Brevo, IHF Rules Questions, Azure hosting, logging, backups, and monitoring;
- international-transfer mechanisms and hosting/data-residency decisions;
- backup restoration erasure procedures;
- incident response and Article 33/34 breach-notification procedures;
- verified data-subject identity handling, response tracking, or one-month SLA operations;
- whether the stated controller identity/contact details are current and legally sufficient.

These are release evidence and operational/legal work, not conclusions that can be proved from source alone.

## Validation performed

| Check | Result |
|---|---|
| `dotnet build RefTestManagement.slnx --configuration Release --no-restore` | ✅ Passed, 0 warnings, 0 errors |
| `dotnet test RefTestManagement.slnx --configuration Release --no-restore` | ✅ 172 passed |
| `Push-Location RefTestManagement.Ui; npm test` | ✅ 36 passed |
| Secret/configuration search | ✅ Only placeholders/examples found |

## Priority actions

1. Correct the four withdrawal-success translations and align the public notice wording with the anonymize-then-delete lifecycle.
2. Remove unused OIDC token persistence, or document and constrain it if required.
3. Normalize report-mutation errors and add a redacted-error regression test.
4. Verify production proxy trust/rate-limit configuration and complete the controller/processor GDPR evidence outside the repository.
