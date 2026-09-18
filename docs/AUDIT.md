# RefTest Management — Deep Audit & GDPR Compliance Report

**Repository:** `handballbelgium-referees/ref-test-management`
**Audit date:** 18 September 2026
**Commit audited:** `8763854` (`main`)
**Scope:** full-stack — security, GDPR, backend architecture, frontend/a11y, testing, CI/CD
**Type:** read-only audit. No code was changed.

> **Superseded.** This report remains the baseline record of the original 34 findings, but their
> current status is tracked in [`AUDIT-R2.md`](./AUDIT-R2.md), a re-audit at commit `6c574ce`
> after the Phase 1 remediation. Six findings here are now fixed and one is partially fixed; the
> re-audit also records twelve new findings, two of them High severity.

> This is a point-in-time assessment. Findings reference `file:line` positions as they existed at
> the commit above; line numbers drift as the code evolves. Re-run the audit after significant
> changes rather than treating this document as a live status page.

---

## 1. Executive summary

> **Production readiness: 🟡 Conditional** — safe to keep running, but **not GDPR-compliant**
> until four contained erasure and retention gaps are closed. No Critical findings, no
> exploitable vulnerability, no committed secrets. Full breakdown in
> [Production readiness](#production-readiness) below.

This is a well-engineered codebase. The domain model is disciplined, the state machine is
enforced server-side rather than in the UI, the GraphQL authorization surface is almost
completely covered, and the privacy design is more thoughtful than most commercial products
handling the same data. The documentation is unusually good.

The findings below are concentrated in two places:

1. **The erasure path has a hole that the documentation does not describe.** The privacy
   design is a two-step anonymize → delete. `docs/PRIVACY.md` states that the administrative
   `deleteRefTests` operation routes through that same path. **It does not.** It calls the
   permanent-delete step directly, skipping anonymization. Because audit events are only ever
   *soft-archived* and never deleted, an administrator deleting a participant's RefTest leaves
   that participant's name and email in the database permanently. This is the most consequential
   finding in the report.

2. **There are no automated tests at all** — zero backend test projects, zero frontend specs —
   and the CI gate compiles but never tests. For a system processing personal data with a
   hand-rolled erasure path, retention scheduler, and job queue, this is the single largest
   source of long-term risk.

No Critical findings. No exploitable vulnerability was identified. No committed secrets were found.

| Severity | Count |
| --- | ---: |
| 🔴 Critical | 0 |
| 🟠 High | 5 |
| 🟡 Medium | 15 |
| ⚪ Low | 13 |

**GDPR verdict: ❌ not currently compliant — compliant by design, non-compliant as implemented.**
The privacy architecture is sound and in places exemplary, but **four gaps produce demonstrable
failures of Art. 5(1)(e) and Art. 17**, and one published claim in `docs/PRIVACY.md` is factually
untrue. None are catastrophic; all are contained fixes. Controller-level obligations (processor
agreements, transfer safeguards, ROPA) are correctly disclaimed as out of scope by
`docs/PRIVACY.md` and are not assessed here.

### Production readiness

> **Verdict: production-grade engineering, with a data-protection posture that is not yet
> production-ready.** Nothing found here warrants taking the system offline. Three items should
> be treated as time-bound live exposures and closed promptly; the rest is ordinary backlog.

This is not a pre-launch assessment. At `v5.5.6` with semantic-release automation and Azure
deployment workflows, the platform is already operating and already processing real participant
data. The findings below are therefore **live exposures**, not launch gates — which raises the
urgency of the erasure gaps specifically, because erasure requests may already have been
accepted and reported as fulfilled without the data actually being erased.

| Dimension | Status | Note |
| --- | --- | --- |
| Functional completeness | ✅ Ready | Feature-complete, coherent domain model |
| Authentication & authorization | ✅ Ready | Complete coverage; policy provider fails closed |
| Application security | ✅ Ready | No exploitable vulnerability; two hardening items (§5) |
| Release & deployment | ✅ Ready | SHA-pinned actions, OIDC, environment-gated production |
| Operational resilience | 🟡 Acceptable | Safe on a single instance; job claim races under scale-out (§6) |
| Assessment integrity | 🟡 Acceptable | ~5-minute post-deadline submission window (§7) |
| **Data protection (GDPR)** | ❌ **Not ready** | Erasure and retention do not do what the notice promises (§4) |
| **Regression safety** | ❌ **Not ready** | Zero tests; CI gates on compilation only (§8) |

**Close these three before the next release** — each is a one-clause change, and together they
are roughly a half-day of work:

1. Branch admin delete on `IsAnonymized` so it anonymizes first (§4.2). Until this ships, every
   deletion performed through the admin UI leaves participant name and email in `AuditEvents`
   permanently, while presenting as a completed erasure.
2. Add `PendingApproval` and `Rejected` to the retention predicate (§4.5). Until this ships,
   those records accumulate participant PII and live invitation tokens with no expiry.
3. Remove the invitation token, URL, email addresses, and scores from the log messages (§4.4).
   The token is a bearer credential currently written to Application Insights at default
   verbosity.

Alongside them, either fix the code or correct the three statements in `docs/PRIVACY.md` that
the code contradicts. A published privacy notice that overstates what the system does is a
distinct exposure from the technical gap itself, and the cheaper half to fix.

**Not blocking, but the reason these gaps existed:** there are no tests. Each of the four GDPR
findings is a single assertion away from being caught automatically. Adding that small suite is
what converts this from a system that is *currently* correct once patched into one that *stays*
correct.

---

## 2. Method

The repository is ~35k lines across 8 .NET 10 projects and an Angular 22 SPA — too large for a
single reading pass. It was audited by subsystem, with every finding required to cite a
`file:line` that was actually read. Claims that could not be evidenced were dropped.

Two findings raised during the audit were **investigated and retracted** as false positives:

- *"Migration parity gap across the four providers."* SQL Server has 11 migrations, the other
  three have one `Initial` each. Reading the actual migration bodies and all four
  `ModelSnapshot.cs` files confirms the other providers are **squashed baselines** that already
  contain every later column (`RejectionReason`, `ScheduledAt`, `AuditEvents`,
  `PrivacyNoticeAcceptedAt`/`Version`, `ExpiredAt`, `IsAnonymized`, `AnonymizedAt`). Parity is
  intact. Retracted.
- *"Anonymized records collapse to a shared `***` token."* `RefTest.Anonymize()` writes
  `erased-{Guid:N}`, not a constant, specifically to preserve the unique index. The obvious bug
  this design invites was already anticipated and avoided. Retracted.

---

## 3. Findings summary

| # | Severity | Area | File | Finding |
|---|---|---|---|---|
| 1 | 🟠 High | GDPR | `RefTestDeletionMutations.cs:59` | Admin delete skips anonymization; audit trail keeps PII forever |
| 2 | 🟠 High | GDPR | `AuditLogCleanupService.cs:60-63` | Audit events are soft-archived, never deleted |
| 3 | 🟠 High | GDPR | `ServiceLoggerMessages.cs:31-141` | Participant email, scores, and invitation token logged at Information |
| 4 | 🟠 High | GDPR | `RefTestExpirationService.cs:137-142` | `PendingApproval`/`Rejected` tests never expire or get erased |
| 5 | 🟠 High | Testing | `RefTestManagement.slnx:11-21` | No automated tests exist anywhere; CI gate is build-only |
| 6 | 🟡 Medium | GDPR | `RefTestPrivacyErasureService.cs:110-120` | In-flight jobs not cancelled; payload PII retained, contrary to docs |
| 7 | 🟡 Medium | Security | `Program.cs:147-153` | `EnforceCostLimits = false` and no rate limiting on a public endpoint |
| 8 | 🟡 Medium | Integrity | `RefTestLifecycleMutations.cs:147-196` | Up to ~5 min grace window to submit after the deadline |
| 9 | 🟡 Medium | CI/CD | `pr.yml:29-30` | Script injection via PR title interpolated into `run:` |
| 10 | 🟡 Medium | CI/CD | `pr.yml:33-70` | PR workflow runs no tests |
| 11 | 🟡 Medium | CI/CD | `RefTestManagement.Ui/package.json:47-61` | README advertises Vitest; no config, no specs exist |
| 12 | 🟡 Medium | CI/CD | `beta-release.yml:24-28` | Long-lived `GH_PAT` with repo write used for releases |
| 13 | 🟡 Medium | Arch | `RefTestManagement.Domain.csproj:11` | Domain transitively depends on EF Core via AuditLog |
| 14 | 🟡 Medium | Arch | `BackgroundJobService.cs:108-143` | Job claim is not atomic; two instances can double-process |
| 15 | 🟡 Medium | Arch | `RefTestConfiguration.cs:38,134` | No concurrency token on `RefTest` or `Job` |
| 16 | 🟡 Medium | Arch | `RefTestCreationMutations.cs:58-65` | DB write and job enqueue are not atomic (no outbox) |
| 17 | 🟡 Medium | Arch | `Auth0ServiceExtensions.cs:14-24` | Auth0 and IHF HTTP clients have no timeout or resilience policy |
| 18 | 🟡 Medium | Perf | `RefTestExpirationService.cs:76-94` | Loads all non-completed RefTests into memory every 5 minutes |
| 19 | 🟡 Medium | A11y | `create-ref-tests.html:47,84,136…` | Positive `tabindex` values (up to `2106`) break focus order |
| 20 | 🟡 Medium | i18n | `can-deactivate-ref-test.guard.ts:6` | Hardcoded English `confirm()` in a 4-language app |
| 21 | ⚪ Low | Security | `RefTest.cs:40` | Token uses `Guid.NewGuid()` rather than an explicit CSPRNG |
| 22 | ⚪ Low | GDPR | `RefTest.cs:570-571` | Anonymization nulls the consent proof on the record |
| 23 | ⚪ Low | Perf | `RefTestConfiguration.cs:121` | No composite index for the retention/expiration scan predicates |
| 24 | ⚪ Low | Perf | `PrivacyRetentionService.cs:46-53` | Re-scans already-anonymized rows daily forever |
| 25 | ⚪ Low | A11y | `datepicker-calendar.html:3` | Modal backdrop is mouse-only; no `Escape`, no focus trap |
| 26 | ⚪ Low | A11y | `ref-test-navigation.html:7` | `<select>` has no label or `aria-label` |
| 27 | ⚪ Low | Privacy | `global-error-handler.ts:17` | `console.error` of arbitrary error objects in production |
| 28 | ⚪ Low | i18n | `fr.json`, `de.json` | Two key typos cause missing translations |
| 29 | ⚪ Low | i18n | `nl/fr/de.json` | 24–38 strings per language are still identical to English |
| 30 | ⚪ Low | Frontend | `app.config.ts:147-155` | No entity `keyFields`; no Apollo `ErrorLink` |
| 31 | ⚪ Low | Frontend | `ref-test.store.ts` | Bare `setTimeout` not tied to destroy |
| 32 | ⚪ Low | Arch | `BackgroundJobService.cs` | 530-line class owning polling, locking, dispatch, retries, cleanup |
| 33 | ⚪ Low | CI/CD | `package.json:32-33` | `sharp` allow-scripts pin (`0.34.5`) is stale vs resolved `0.35.4` |
| 34 | ⚪ Low | CI/CD | `.github/` | No Dependabot config and no CodeQL workflow |

---

## 4. GDPR compliance — ❌ Not currently compliant

> **Status: the design is compliant; the implementation is not.** Four gaps produce demonstrable
> failures of **Art. 5(1)(e)** (storage limitation) and **Art. 17** (right to erasure), and one
> of them means erasure requests fulfilled through the admin UI do not actually erase the data.
> All four are contained, one-clause fixes — this is a remediation task, not a redesign.
> Assessed at the application layer only; controller-level obligations are out of scope (§4.8).

### 4.1 What is correctly implemented

These were verified in code, not taken from the documentation:

| Control | Evidence | Verdict |
| --- | --- | --- |
| Consent required before processing, with version pinning | `RefTest.cs:220-221` — `Start()` throws unless `PrivacyNoticeVersion == requiredVersion && PrivacyNoticeAcceptedAt.HasValue` | ✅ Enforced server-side, not just in the UI |
| Stale-consent rejection | `RefTestLifecycleMutations.cs:89-90` — accepting an outdated notice version is refused | ✅ |
| Withdrawal without identity friction | `RefTestLifecycleMutations.cs:113-134` — token-only, any status | ✅ Matches Art. 7(3) "as easy to withdraw as to give" |
| Erasure is transactional and retry-safe | `RefTestPrivacyErasureService.cs:96-108` — reloads the entity inside the execution strategy so a rolled-back attempt cannot leave a false "already anonymized" state | ✅ Genuinely subtle and correctly handled |
| Anonymized records blocked from further processing | `RefTest.cs:210, 232, 247`; `RefTestEmailMutations.cs:55, 131` | ✅ Consistent across every operation |
| Audit PII redaction handles both payload shapes | `RefTestPrivacyErasureService.cs:161-192` — flat `{"email":"x"}` and diff `{"email":{"old":…,"new":…}}` | ✅ |
| Token collision on anonymization avoided | `RefTest.cs:569` — `erased-{Guid:N}`, not a shared sentinel | ✅ |
| No personal data written to disk | Repo-wide search: the only file write is `workbook.SaveAs(stream)` (`RefTestReportService.cs:306`) to an in-memory stream | ✅ PDFs/Excel are generated in memory and emailed |
| Privacy notice genuinely translated | 17 `privacy.*` keys in all four languages, no empty values, 1755–2167 chars each | ✅ Not placeholder text |
| No participant data cached by the service worker | `ngsw-config.json` has **no `dataGroups`**; `/graphql**` and `/Account/**` excluded from navigation | ✅ No cross-participant cache leak on a shared device |

### 4.2 Gap 1 — Administrative delete never anonymizes (Art. 17, Art. 5(1)(e)) 🟠 High

`docs/PRIVACY.md` states:

> The standard administrative `deleteRefTests` operation and the participant-facing
> `withdrawConsent` operation both use this same erasure path, and both trigger step 1 or step 2
> depending on whether the RefTest was already anonymized.

The code does not do this. `RefTestDeletionMutations.cs:59` calls `DeleteAsync` unconditionally:

```csharp
await privacyErasureService.DeleteAsync(refTest, cancellationToken);
```

There is no `if (refTest.IsAnonymized)` branch. `DeleteAsync` (`RefTestPrivacyErasureService.cs:58-82`)
removes the row but **never touches `AuditEvents`** — by design, because its documentation assumes
step 1 already redacted them:

> Its audit trail is left untouched (it carries no FK to the RefTest row) and expires on its own
> per the normal audit-log retention schedule

That assumption only holds when anonymization ran first. When an administrator deletes a RefTest
that was never anonymized — the normal case — the audit trail retains the participant's
`firstName`, `lastName`, and `email` in the event `Data` payload (written by
`RefTest.cs:227`, `new RefTestStartedEvent(FirstName, LastName, Email)`), plus `ActorName`
and `ActorEmail`.

**Impact:** the participant record disappears from the admin UI, so the deletion *appears*
complete, while their identifying data remains in `AuditEvents` — and, per Gap 2 below,
remains there permanently. A data-subject erasure request fulfilled through the admin UI does
not actually erase the data.

**Fix:** branch on `IsAnonymized` in `DeleteRefTestsAsync`, exactly as the documentation
describes — call `EraseAsync` first when the record is not yet anonymized, then `DeleteAsync`.

### 4.3 Gap 2 — Audit events are never deleted, only flagged (Art. 5(1)(e)) 🟠 High

`AuditLogCleanupService.cs:60-63`:

```csharp
var archived = await context.AuditEvents
    .Where(a => a.Timestamp < cutoff && !a.IsArchived)
    .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsArchived, true), cancellationToken);
```

`AuditLogOptions.RetentionDays` defaults to 90. After 90 days the service sets a boolean. The
row, including any personal data in `Data` / `ActorName` / `ActorEmail`, stays in the database
indefinitely. The README describes this as intentional ("soft-archived (never hard-deleted)"),
which is a legitimate accountability choice — but it means the audit log is a **permanent PII
store** unless the redaction in Gap 1 always runs. It currently does not.

**Impact:** storage limitation is not satisfied for any data that reaches the audit log without
being redacted.

**Fix:** either (a) close Gap 1 so audit PII is always redacted before the row is deleted, or
(b) hard-delete archived audit events after a second, longer retention window, or (c) redact
PII fields at archive time. Option (a) is the smallest change and preserves the accountability
intent.

### 4.4 Gap 3 — Personal data in application logs (Art. 5(1)(c), Art. 17, Art. 32) 🟠 High

`ServiceLoggerMessages.cs` logs participant personal data at `Information` level throughout:

| Line | Logged |
| --- | --- |
| `:31, 34, 37, 40` | Participant email on every send/success/failure |
| `:46-47` | Email **+ full invitation token + full invitation URL** |
| `:49-50` | Email + question score + answer score + percentage |
| `:80-84` | Email on every job enqueue |
| `:134-141` | Email on auto-complete and expiry |

Two distinct problems:

1. **Assessment results and email addresses are duplicated into the log stream.** On Azure App
   Service these flow to App Service logs and/or Application Insights, which have their own
   retention, their own access control, and are entirely outside the erasure path. When a
   participant withdraws consent, `EraseAsync` redacts the database and the audit trail — it
   cannot redact logs. The erasure is therefore incomplete.

2. **The invitation token is a bearer credential and it is logged in plaintext at
   Information level, together with the URL that uses it** (`:46-47`). Anyone with log read
   access can take over a participant's test session, view their results, or withdraw their
   consent on their behalf.

`docs/PRIVACY.md` acknowledges that "third-party logs are outside the application's database
cleanup" but does not disclose that the application writes participant PII and bearer tokens
into its own logs at default verbosity.

**Fix:** remove the token and URL from `LogSendingRefTestInvitation` entirely. Replace email
addresses with the RefTest `Guid` (already logged alongside in most messages) or a hash, and
drop scores from `LogSendingRefTestResults`. The RefTest ID is sufficient for every
operational diagnostic these messages serve.

### 4.5 Gap 4 — `PendingApproval` and `Rejected` tests are retained forever (Art. 5(1)(e)) 🟠 High

Two filters combine into an unbounded retention hole.

`RefTestExpirationService.cs:120-143` — `IsExpired` returns `false` for these statuses:

```csharp
case RefTestStatus.Completed:
case RefTestStatus.Expired:
case RefTestStatus.PendingApproval:
case RefTestStatus.Rejected:
default:
    return false;
```

`PrivacyRetentionService.cs:47-52` — only ever looks at `Completed` and `Expired`:

```csharp
.Where(refTest =>
    (refTest.Status == RefTestStatus.Completed && refTest.CompletedAt < cutoff) ||
    (refTest.Status == RefTestStatus.Expired && refTest.ExpiredAt < cutoff))
```

A RefTest created but never approved (`PendingApproval`), or explicitly rejected by an approver
(`Rejected`), therefore **never expires and is never erased**. Its participant first name, last
name, email, and live invitation token remain in the database indefinitely. `Pending` and
`InProgress` are fine — the expiration service converts them to `Expired`/`Completed`, which
the retention service then picks up.

This directly contradicts the README's "Automatic retention enforcement with a configurable
retention period".

**Impact:** every rejected or never-approved test accumulates participant PII permanently.
For a platform where approval is an optional workflow that some tests will fail, this set grows
without bound.

**Fix:** add `PendingApproval` and `Rejected` to the `PrivacyRetentionService` predicate, using
`CreatedAt` as the cutoff basis since neither status has a completion timestamp.

### 4.6 Gap 5 — Withdrawal does not stop in-flight email, and payloads are not deleted (Art. 7(3), Art. 17) 🟡 Medium

`RefTestPrivacyErasureService.cs:110-120` cancels jobs, but only `Pending` ones:

```csharp
var pendingJobs = await context.Jobs
    .Where(job => job.Status == JobStatus.Pending && job.Payload.Contains(refTestId))
    .ToListAsync(ct);

foreach (var job in pendingJobs)
    job.Cancel("RefTest personal data was erased");
```

Three issues:

1. **Jobs already in `Processing` are not cancelled.** A participant who withdraws consent while
   their invitation or results email is mid-flight will still receive it. The window is small
   but real, and it is precisely the moment a participant is most likely to act.

2. **`Job.Cancel()` does not clear the payload.** `Job.cs:74-80` sets `Status`, `ErrorMessage`,
   `LockedUntil`, and `CompletedAt` — `Payload` is untouched. `InvitationEmailPayload` contains
   full name, email, and **token**; `ResultEmailPayload` contains full name, email, scores, and
   the participant's answers. These survive in the `Jobs` table until the job-cleanup window
   elapses (`BackgroundJobService.cs:508-517` does eventually hard-delete them, so this is
   bounded — but it is not immediate).

3. **`docs/PRIVACY.md` states the opposite:** "Queued or completed background-job payloads
   (invitation/result email content) referencing the RefTest are **deleted outright**." They are
   not deleted; pending ones are marked failed and completed ones are left alone.

Also note the query is `job.Payload.Contains(refTestId)` — a `LIKE '%guid%'` scan over an
unindexed JSON text column, executed on every erasure.

**Fix:** widen the predicate to include `Processing`, null out `Payload` in `Cancel()` (or add
an explicit redaction), and correct the sentence in `docs/PRIVACY.md`.

### 4.7 Gap 6 — Anonymization destroys the consent proof (Art. 7(1)) ⚪ Low

`RefTest.cs:570-571` clears the consent record during anonymization:

```csharp
PrivacyNoticeVersion = null;
PrivacyNoticeAcceptedAt = null;
```

Article 7(1) requires the controller to be able to demonstrate that consent was obtained.
Partially mitigated: the `RefTestPrivacyNoticeAcceptedEvent` remains in the audit trail with
its timestamp (only `ActorName`/`ActorEmail` are redacted). Worth a conscious decision rather
than a silent side effect.

### 4.8 Article mapping

| Article | Requirement | Status |
| --- | --- | --- |
| Art. 5(1)(c) — minimisation | Only necessary data processed | ⚠️ Gap 3 (PII in logs) |
| Art. 5(1)(e) — storage limitation | Retained no longer than necessary | ❌ Gaps 1, 2, 4 |
| Art. 7(1) — demonstrable consent | Controller can prove consent | ⚠️ Gap 6 |
| Art. 7(3) — withdrawal | As easy to withdraw as to give | ✅ Exemplary — token-only, any status |
| Art. 12–14 — transparency | Notice provided before processing | ✅ Versioned, gated, 4 languages |
| Art. 17 — erasure | Data actually erased on request | ❌ Gaps 1, 3; ⚠️ Gap 5 |
| Art. 25 — privacy by design | Built in, not bolted on | ✅ Strong; erasure is a first-class domain concept |
| Art. 30 — ROPA | Records of processing | ➖ Controller obligation, correctly disclaimed |
| Art. 32 — security | Appropriate technical measures | ⚠️ Token in logs; otherwise solid |
| Art. 44–49 — transfers | Safeguards for Auth0/Brevo/IHF | ➖ Controller obligation, correctly disclaimed |

---

## 5. Security

No exploitable vulnerability was found. No committed secrets — `appsettings.json:10-74` has
empty credential fields.

### Authorization matrix

Every administrative GraphQL operation carries an explicit permission. The unauthenticated
surface is small and intentional:

**Public by design (participant flow, token-addressed):** `getPrivacyNotice`,
`getRefTestByToken`, `getEnabledLanguages`, `getScoreConfiguration`,
`getResultsEmailDelayMinutes`, `startRefTest`, `acceptPrivacyNotice`, `withdrawConsent`,
`saveRefTestProgress`, `completeRefTest`, `refTestTimeExtended`, `refTestSessionLock`.

**Permission-gated:** all 16 administrative mutations, all 5 admin queries, and the
`refTestUpdated` / `refTestsUpdated` subscriptions. No administrative operation was found
missing an `[Authorize]` attribute.

### Verified correct

- **Cookies:** `Secure=Always`, `HttpOnly=true`, `SameSite=Strict` (`SecurityStartup.cs:20-25`).
- **CORS:** restricted to `http://localhost:4200` / `https://localhost:4200` with credentials,
  and registered **only** inside `IsDevelopment()` (`Program.cs:35-43, 183`). No wildcard.
- **Policy provider fails closed:** an unrecognized policy name falls through to the default
  provider rather than returning a permissive policy — a typo in an `[Authorize]` attribute
  cannot silently disable protection (`TaskAuthorizationPolicyProvider.cs:14-30`).
- **Claims:** permissions read from the `permissions` claim, not a client-influenced type
  (`TaskPermissionHandler.cs:11-35`).
- **State machine:** `Start()` requires `Pending`, `SaveProgress()` and `Complete()` require
  `InProgress`, and all three reject anonymized records (`RefTest.cs:208-252`). Re-completing a
  completed test, starting an expired test, and acting on an anonymized record are all blocked.
- **Unauthenticated subscriptions carry no PII:** `RefTestTimeExtended` emits only
  `(Guid, int, int, DateTime)`; `RefTestSessionLock` emits only `Acquired`/`Blocked`
  (`RefTestSubscriptionService.cs:11-14, 254-255`). A client who knows a RefTest GUID learns
  timing metadata only.
- **No raw SQL** anywhere; no `IncludeExceptionDetails` exposure to clients.
- **HTTPS redirection** enabled (`Program.cs:183`).

### 🟡 Medium — GraphQL cost limits disabled on a public endpoint

`Program.cs:147-153`:

```csharp
.ModifyCostOptions(o => o.EnforceCostLimits = false)
```

There is no `AddRateLimiter` / `UseRateLimiter` anywhere in the backend. `/graphql` is publicly
reachable and accepts unauthenticated operations. With cost enforcement explicitly disabled, a
single anonymous client can submit deeply nested or highly multiplied queries. `MaxPageSize = 100`
constrains pagination but not query shape.

Token brute-force is *not* the concern here — 122 bits of entropy makes that infeasible. The
concern is query-cost amplification against an unauthenticated endpoint.

**Fix:** set `EnforceCostLimits = true` and add ASP.NET rate limiting on the public participant
operations.

### ⚪ Low — Invitation token generated with `Guid.NewGuid()`

`RefTest.cs:40` (and `:428, 476, 517, 530`): `Token = Guid.NewGuid().ToString("N")`.

Modern .NET generates GUIDs from a CSPRNG and 122 bits is ample entropy, so this is not
currently weak. But `Guid.NewGuid()` carries no *contractual* cryptographic guarantee, and this
value is a bearer credential granting full access to a participant's test and their
withdraw-consent action.

**Fix:** `RandomNumberGenerator.GetHexString(32)`. One-line change, removes the caveat entirely.

---

## 6. Backend architecture & quality

### Verified correct

- `Domain` does not reference `Api` or `Infrastructure`; `Application` references only `Domain`.
- All five background services wrap their loop in `try/catch`, honour `CancellationToken`, and
  create a fresh DI scope per iteration. **No silently-dead hosted service** — this is the most
  common .NET background-service bug and it is absent here.
- Read queries consistently use `AsNoTracking()` (`RefTestQueries.cs:58, 94, 109`;
  `DataLoaders.cs:17, 32`).
- Jobs reach a terminal `Failed` state after `MaxAttempts`; expired leases are reclaimable
  because the poll query admits `LockedUntil <= now`. No infinite-retry poison queue, no
  permanently orphaned job after a mid-job crash.
- **`DateTime.UtcNow` used consistently** — zero occurrences of `DateTime.Now`, `DateTime.Today`,
  or `DateTimeOffset.Now` repo-wide. For a platform with timed assessments and a 3-year retention
  window on a CET/CEST server, this matters and it was done right.
- No `async void`, no empty `catch { }`, no `TODO`/`HACK`/`FIXME` comments.
- Migration parity across all four providers is intact (see §2).

### 🟡 Medium — Domain transitively depends on EF Core

`RefTestManagement.Domain.csproj:11` references `RefTestManagement.AuditLog`, which carries a
`Microsoft.EntityFrameworkCore` package reference and contains `AuditSaveChangesInterceptor`.
The domain centre therefore compiles against EF Core.

**Fix:** split the audit abstractions (`IDomainEvent` and friends) into a dependency-free
contracts project; keep the interceptor in Infrastructure.

### 🟡 Medium — Job claim is not atomic

`BackgroundJobService.cs:108-115` reads eligible jobs, then `:142-143` marks one as processing
in a separate round trip. On a scaled-out App Service plan, two instances can read the same
pending job before either persists the lock, producing duplicate emails.

**Fix:** claim in a single statement (`UPDATE … WHERE Status = 'Pending' … OUTPUT`) or use a
compare-and-swap on a version column.

### 🟡 Medium — No concurrency token on `RefTest` or `Job`

`RefTestConfiguration.cs:38, 134-135` and `JobConfiguration.cs:43-46` define indexes but no
`IsConcurrencyToken()` / rowversion. Concurrent edits are last-write-wins. Two administrators
editing the same test, or a background service racing an admin action, silently lose one change.

### 🟡 Medium — Write and enqueue are not atomic

`RefTestCreationMutations.cs:58-65` saves the entity, then enqueues the job; `:227-243` catches
enqueue failures *after* the entity is already committed. Same pattern in
`RefTestApprovalMutations.cs:64-120` and `RefTestUpdateMutations.cs:41-64`. A failed enqueue
leaves a persisted test whose invitation never sends.

**Fix:** enqueue the job row in the same `SaveChangesAsync` as the entity (a transactional
outbox — the `Jobs` table already is one, it just needs to share the unit of work).

### 🟡 Medium — Auth0 and IHF clients have no timeout or resilience policy

`Auth0ServiceExtensions.cs:14-24` registers `AddHttpClient` with no configuration;
`Program.cs:130-136` configures the IHF StrawberryShake client with a base address only. The
email client correctly sets a 30s timeout (`Program.cs:100-106`) — these two do not. The IHF
client is on the test-creation path, so a transient upstream outage fails test creation with no
retry.

**Fix:** `AddStandardResilienceHandler()` on both.

### 🟡 Medium — Expiration service evaluates client-side

`RefTestExpirationService.cs:76-94` loads **every** RefTest not in `Completed`/`Expired` into
memory every five minutes, then filters with the in-process `IsExpired` method. The projection
keeps the row narrow, but the row count is unbounded and grows with the dataset.

**Fix:** push the predicate into SQL, or add the composite index from finding #23 and filter on
a computed expiry timestamp.

### ⚪ Low — Missing composite indexes / repeated retention scan

`RefTestConfiguration.cs:121, 134-135` indexes `IsAnonymized`, `Email`, `Status`, `CreatedAt`
individually, but the retention and expiration scans filter on `Status` + timestamp
combinations. Separately, `PrivacyRetentionService.cs:46-53` does not exclude `IsAnonymized`,
so it reloads every already-anonymized historical record daily forever — `EraseAsync` returns
early, so it is harmless but increasingly wasteful.

### ⚪ Low — `BackgroundJobService` is a 530-line god class

Owns polling, claiming, dispatch, every job handler, retry logic, and cleanup.

---

## 7. Frontend

### Verified correct

- Admin routes guarded with `authGuard` + `permissionGuard` (`app.routes.ts:17-57`); participant
  routes isolated (`:60-67`).
- **Every route is lazy-loaded** via `loadComponent` — anonymous participants do not download
  the admin bundle.
- **Invitation token is never written to `localStorage`/`sessionStorage`** — the only
  `localStorage` use is the language preference (`app.ts:66, 104`).
- No XSS sinks: no `innerHTML`, `bypassSecurityTrust*`, `document.write`, or `eval` in `src/app`.
- Banner notifications use `role="alert"` and `aria-live` (`banner.html:7-8`).
- `strict: true` in `tsconfig.json`; no production source maps.
- No unmanaged `.subscribe()` — long-lived streams use `takeUntilDestroyed`.

### 🟡 Medium — Deadline has a grace window of up to ~5 minutes

The countdown is client-driven (`take-ref-test.ts:79-92`, `interval(1000)`), which is fine for
UX. The server does **not** validate `StartedAt + MaxTimeInMinutes` on submission —
`SaveRefTestProgressAsync` and `CompleteRefTestAsync` (`RefTestLifecycleMutations.cs:147-196`)
check only `Status == InProgress`. The test only leaves `InProgress` when
`RefTestExpirationService` next runs, which is **every 5 minutes**.

A participant who blocks the auto-submit (closes the tab, disables JS, or manipulates the clock)
can therefore submit up to ~5 minutes past their deadline and be accepted.

**Fix:** add an explicit `StartedAt + MaxTimeInMinutes` check in `Complete()` and `SaveProgress()`.
The data is already on the entity — this is a two-line guard in the domain model.

### 🟡 Medium — Positive `tabindex` throughout the create form

`create-ref-tests.html:47, 84, 136, 224, 250, 280, 333, 373, 398, 421` use values from
`tabindex="1"` up to `tabindex="2106"`. Any positive tabindex pulls those elements ahead of the
entire rest of the document in tab order, including the site navigation. WCAG 2.4.3.

**Fix:** delete them; rely on DOM order.

### 🟡 Medium — Hardcoded English confirmation

`can-deactivate-ref-test.guard.ts:6`:

```typescript
return confirm('Are you sure you want to leave the test?');
```

Shown to Dutch, French, and German participants mid-assessment, in English, in an untranslatable
browser dialog.

### ⚪ Low — Remaining accessibility issues

| File:line | Issue | WCAG |
| --- | --- | --- |
| `datepicker-calendar.html:3` | Backdrop `(click)` only — no `Escape`, no focus trap, no dialog role | 2.1.1 |
| `ref-test-navigation.html:7` | `<select>` with no label or `aria-label` | 1.3.1 / 3.3.2 |
| `app.html:31-37` | Avatar initials conveyed visually with only `title` | 1.1.1 / 4.1.2 |

Not separately verified: whether the countdown timer is announced to assistive technology. For a
timed assessment, a purely visual countdown disadvantages screen-reader users — worth checking
and adding a polite `aria-live` region at a sensible cadence (e.g. at 5 minutes and 1 minute
remaining, not every second).

### ⚪ Low — Apollo and i18n details

- No entity `keyFields`; only `relayStylePagination` on two `Query` fields (`app.config.ts:147-155`).
- No `ErrorLink` — only `RetryLink` (`app.config.ts:108-145`). Unrecoverable errors have no
  global surface.
- `ref-test.store.ts` — bare `setTimeout(… , 10000)` not tied to destroy.
- `global-error-handler.ts:17` — `console.error` of arbitrary error objects in production; if an
  error carries participant data it lands in the browser console.

### i18n coverage

| Language | Leaf keys | Missing vs `en` | Identical to English |
| --- | ---: | ---: | ---: |
| `en` | 633 | — | — |
| `nl` | 633 | 0 | 29 |
| `fr` | 633 | 1 | 38 |
| `de` | 637 | 1 | 24 |

Two key typos cause real missing translations:

- `fr.json` has `ref_tests.list.dialogs.delete.reftests` instead of `…delete.ref_tests`
- `de.json` has `…specific_question_numbers.import_importing` instead of `…importing`

`de.json` also carries 5 orphan keys (`ref_tests.list.filters.score_range`, `min_score`,
`max_score`, `sorting.score`) with no English counterpart — dead entries from a removed feature.

The 24–38 identical-to-English strings per language are mostly legitimate (`status`,
`percentage`, `email`), but items like `ref_test.instructions_title` and
`ref_test.participant_name` look genuinely untranslated.

---

## 8. Testing & CI/CD

### 🟠 High — No automated tests exist

- `RefTestManagement.slnx:11-21` lists 11 projects. None is a test project.
- `Directory.Packages.props` contains no xunit, NUnit, MSTest, FluentAssertions, Moq,
  NSubstitute, or Testcontainers reference.
- Zero `*.spec.ts` files under `RefTestManagement.Ui/src`, against 89 components and services.
- `pr.yml:33-70` runs `npm ci`, the Angular build, `dotnet restore`, and `dotnet build` — and
  no test step at all.

The quality gate is: *does it compile, and is the PR title a conventional commit*.

This matters disproportionately here because the highest-risk logic in the system is exactly the
logic with no coverage: the two-step erasure path, the retention scheduler's status predicates,
the job queue's retry and lease handling, the scoring calculation, and the consent gate. Gaps 1
and 4 in §4 are precisely the kind of defect a single test would have caught —
*"delete a non-anonymized RefTest, assert no PII remains in AuditEvents"* and
*"assert a Rejected RefTest is erased after the retention period"*.

**Fix:** one backend test project covering `RefTestPrivacyErasureService`,
`PrivacyRetentionService`, `RefTest`'s state machine, and the scoring calculation, wired into
`pr.yml` with `dotnet test`. That is a small amount of work for a large reduction in the risk
that any of these findings silently return.

### 🟡 Medium — README overstates the testing setup

`README.md` lists **Vitest 4.1.11** in the frontend tech-stack table as the "Unit testing
framework". `vitest` is in `devDependencies` (`package.json:47-61`) and `"test": "ng test"` is
defined, but there is no `vitest.config.*`, no `vite.config.*`, and no spec files. `angular.json:71-73`
declares only the `@angular/build:unit-test` builder.

A reader of the README would reasonably conclude the project has unit tests. It has none.

### 🟡 Medium — Script injection via PR title

`pr.yml:29-30`:

```yaml
run: echo "${{ github.event.pull_request.title }}" | npx commitlint
```

`${{ … }}` is substituted by the Actions runner *before* the shell parses the line, so a PR
titled with a backtick or `$(…)` sequence executes on the runner.

Mitigating: the trigger is `pull_request` (`:3-6`), not `pull_request_target`, so a fork PR gets
a read-only token and no secrets are in scope for `validate-pr-title`. The blast radius is
runner compromise and workflow tampering, not secret theft — hence Medium rather than High.
The workflow does contain a separate `sync-readme-versions` job with `contents: write` (`:72-76`).

**Fix:**

```yaml
env:
  PR_TITLE: ${{ github.event.pull_request.title }}
run: echo "$PR_TITLE" | npx commitlint
```

### 🟡 Medium — Long-lived `GH_PAT`

`beta-release.yml:24-28` and `stable-release.yml:23-27, 54-59, 175-179` pass
`token: ${{ secrets.GH_PAT }}` to `actions/checkout` so release jobs can push tags and branch
updates past protection rules. A PAT with repo write is a longer-lived, higher-value secret than
the ephemeral `GITHUB_TOKEN`.

**Fix:** a GitHub App installation token, or a fine-grained PAT scoped to this repository only.

### Verified correct

- **Every action is pinned to a full commit SHA**, including third-party ones (`azure/login`,
  `azure/webapps-deploy`, `major0/gh-artifact-cleanup`). This is better than most repositories.
- Release and deploy jobs declare explicit least-privilege `permissions` blocks; Azure
  authentication uses OIDC (`id-token: write`) rather than a stored publish profile.
- **Production deploy is gated by a GitHub Environment** — `stable-release.yml:14`
  (`environment: promote`) and `:198`. Whether required reviewers are configured is a GitHub
  setting outside the repository; worth confirming in the org settings.
- `package-lock.json` committed, `lockfileVersion: 3`; dependencies `~`-pinned rather than `^`.
- `renovate.json` enables `dependencyDashboard`, `vulnerabilityAlerts`, `osvVulnerabilityAlerts`,
  and pins Action digests.
- `.gitignore` correctly excludes `*.user`, `.idea/`, `node_modules/`, `*.pfx`, `*.env`,
  `*.DotSettings.user`.
- `Directory.Build.props:1-4` enables NuGet lockfiles.

### ⚪ Low

- No `.github/dependabot.yml` and no CodeQL workflow — Renovate covers dependency updates, but
  there is no static security analysis in CI.
- `package.json:32-33` allows scripts for `sharp@0.34.5`; the lockfile resolves `~0.35.4`. Stale pin.
- `.husky/pre-commit` runs a README sync and an Angular build, but no lint and no tests. A
  full Angular build on every commit is slow enough to invite `--no-verify`.
- `CODEOWNERS` assigns everything to `@KristofGilis`, the sole maintainer — it cannot enforce
  independent review. This is inherent to a single-maintainer project, not a defect, but it does
  mean the test suite in finding #5 is the only realistic safety net.

---

## 9. Prioritized remediation

> **An implementable breakdown of everything in this section — all 34 findings as 27 PR-sized
> work packages with files, acceptance criteria, and sequencing — is in
> [docs/AUDIT-REMEDIATION.md](AUDIT-REMEDIATION.md).** The list below is the summary view.

**Fix first — correctness of the erasure promise (small, contained changes):**

1. Branch `DeleteRefTestsAsync` on `IsAnonymized` so admin deletes anonymize before deleting
   (§4.2). One `if`. Closes the largest GDPR gap.
2. Add `PendingApproval` and `Rejected` to the retention predicate (§4.5). One clause.
3. Strip the invitation token, URL, email addresses, and scores from
   `ServiceLoggerMessages` (§4.4). Log the RefTest `Guid` instead.
4. Include `Processing` jobs in erasure cancellation and clear `Job.Payload` on cancel; correct
   the "deleted outright" sentence in `docs/PRIVACY.md` (§4.6).

**Then — the safety net:**

5. Add one backend test project covering the erasure path, retention predicates, the `RefTest`
   state machine, and scoring. Wire `dotnet test` into `pr.yml` (§8).

**Then — hardening:**

6. `EnforceCostLimits = true` + rate limiting on the public endpoint (§5).
7. Server-side deadline validation in `Complete()`/`SaveProgress()` (§7).
8. Atomic job claim and a concurrency token on `RefTest`/`Job` (§6).
9. `env:` indirection for the PR title in `pr.yml` (§8).
10. Resilience handlers on the Auth0 and IHF clients (§6).

**Then — polish:**

11. Remove positive `tabindex`; translate the `confirm()` dialog; fix the two i18n key typos.
12. `RandomNumberGenerator.GetHexString` for tokens; composite retention index; Apollo `ErrorLink`.
13. Update the README to stop advertising a test framework that is not configured.

---

## 10. Closing note

The gaps in §4 are not sloppiness — they are the natural consequence of a genuinely sophisticated
privacy design (two-step erasure, redact-but-retain audit trail, status-aware retention) built
without tests to hold its invariants in place. The documentation describes the intended behaviour
accurately and completely; the code drifted from it in four specific places, and nothing existed
to notice.

The fastest durable fix is not any individual patch above. It is finding #5: a handful of tests
asserting the invariants that `docs/PRIVACY.md` already states in prose.
