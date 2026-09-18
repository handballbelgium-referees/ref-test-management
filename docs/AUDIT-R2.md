# RefTest Management — Re-Audit & GDPR Compliance Report (R2)

**Repository:** `handballbelgium-referees/ref-test-management`
**Audit date:** 18 September 2026
**Commit audited:** `6c574ce` (branch `docs/audit-and-remediation-plan`)
**Previous report:** [`AUDIT.md`](./AUDIT.md) at commit `8763854`
**Scope:** full-stack re-audit — verification of Phase 1 remediation, plus a fresh sweep for
security, GDPR, backend, frontend, testing and CI/CD issues
**Type:** read-only audit. No code was changed while producing this report.

> This is a point-in-time assessment. Findings reference `file:line` positions as they existed at
> the commit above. This report **supersedes** `AUDIT.md`, which remains valid as the baseline
> record of the original 34 findings.

---

## 1. Executive summary

> **Production readiness: 🟡 Conditional (unchanged)** — and for a new reason. The six GDPR
> gaps that blocked compliance were genuinely closed in the code, but the remediation itself
> **introduced two High-severity regressions**, one of which silently defeats the very fix it
> shipped with. Do not treat Phase 1 as done.

The remediation delivered real progress: **6 of the 34 original findings are fixed and 1 is
partially fixed**, concentrated precisely where the risk was — the erasure path, the retention
sweep, and personal data in logs. Those fixes were verified independently and hold up.

The problem is what came with them. An adversarial review of the change set found that:

1. **The audit-log fix does not reach existing data.** The new redaction loop only processes rows
   where `IsArchived = false`. The code it replaced set `IsArchived = true` *without redacting
   anything*. Every audit row archived by any previous deployment is therefore permanently
   excluded from redaction and keeps the participant's name and email **forever**. The headline
   fix works only for rows archived from now on. (Finding N1)

2. **Clearing job payloads can break admin operations.** `Job.Cancel()` now wipes `Payload` to
   an empty string, but `MarkAsFailed` can return a cancelled job to `Pending`, and
   `JobEnqueueService` deserializes the payload of every `Pending`/`Processing` job with no
   error handling. `JsonSerializer.Deserialize<T>("")` throws — so a single poisoned row can make
   `resetRefTest` / `reviveRefTest` fail for unrelated tests. (Finding N2)

Neither is exotic. Both were found by reading the change set against the code it touches, which
is exactly the check that a test suite would normally perform automatically — and this repository
still has **no automated tests at all**, which remains the single highest-leverage finding in
both reports.

Set against that, the codebase's original strengths are intact. The domain model is still
disciplined, the GraphQL authorization surface is still fully covered, no secrets are committed,
no XSS sink exists in the frontend, and every third-party GitHub Action is SHA-pinned.

### Production readiness

| Dimension | Status | Note |
|---|---|---|
| Security posture | ✅ | No Critical findings, no exploitable vulnerability, no committed secrets |
| Authorization coverage | ✅ | Every admin operation carries `[Authorize]`; public surface is explicit and narrow |
| Secrets management | ✅ | `appsettings.json` placeholders are empty; secrets come from configuration |
| Supply chain | ✅ | All third-party actions SHA-pinned; Renovate active |
| Data protection (GDPR) | ❌ | Improved, but historical audit data still retains PII indefinitely (N1) |
| Operational correctness | 🟡 | Two new regressions (N1, N2); pre-existing non-atomic job claim unresolved |
| Resilience | 🟡 | Outbound HTTP clients still have no timeout or retry policy |
| Automated testing | ❌ | Still zero tests; this is how both regressions reached `main`-bound code |

**Verdict:** safe to keep running, but Phase 1 must be finished before it can be called done.
The two regressions should be fixed before this branch is merged.

---

## 2. What changed since the last audit

Two commits landed on `docs/audit-and-remediation-plan`:

| Commit | Contents |
|---|---|
| `29dba7c` | Phase 1 GDPR remediation (WP-01 → WP-07) plus a fix for PII persisted into `Job.ErrorMessage` |
| `6c574ce` | Fix for a `.husky/pre-commit` hook that disabled itself after every `[skip ci]` release commit |

No backend architecture, frontend, testing or CI work was attempted, so those findings are
expected to be unchanged — and were re-verified rather than assumed.

---

## 3. Findings summary

**Original 34 findings:** 6 fixed ✅ · 1 partially fixed 🟡 · 27 still open
**New findings in this re-audit:** 12 (2 High, 7 Medium, 3 Low)
**Total open:** 39 — 3 High, 21 Medium, 15 Low · **0 Critical**

### 3.1 New findings

| # | Severity | Area | File | Finding |
|---|---|---|---|---|
| N1 | 🟠 High | GDPR | `AuditLogCleanupService.cs:77` | Redaction loop skips already-archived rows; historical audit PII is never removed |
| N2 | 🟠 High | Correctness | `Job.cs:83`, `JobEnqueueService.cs:141-152,185` | Cleared payload can reach a `Pending` job and break reset/revive mutations |
| N3 | 🟡 Medium | Perf | `AuditLogCleanupService.cs:74-101` | Batch loop never clears the change tracker; O(N²) `DetectChanges` and unbounded memory |
| N4 | 🟡 Medium | Correctness | `BackgroundJobService.cs:189` | Regex timeout inside the `catch` block can strand a job in `Processing` forever |
| N5 | 🟡 Medium | GDPR | `AuditPiiRedactor.cs:25` | PII key matching is case-sensitive; PascalCase diff payloads would not be redacted |
| N6 | 🟡 Medium | Correctness | `RefTestDeletionMutations.cs:70-72` | Erase and delete run in separate transactions; failure leaves a half-erased record |
| N7 | 🟡 Medium | GDPR | `docs/PRIVACY.md:36-42` | Notice does not disclose that participant data is emailed to staff and approvers |
| N8 | 🟡 Medium | Security | `SaveRefTestProgressInput.cs:1-6`, `RefTestLifecycleMutations.cs:147-182` | Public participant mutations accept unbounded input with no validation |
| N9 | 🟡 Medium | Security | `app.routes.ts:60-65`, `ref-test.store.ts:12` | Participant invitation token travels in the URL path and into app state |
| N10 | ⚪ Low | GDPR | `RefTestPrivacyErasureService.cs:55` | Admin actor on `RefTestAnonymized` is redacted, losing "who erased this" |
| N11 | ⚪ Low | Correctness | `PrivacyRetentionService.cs:54-58` | Comment asserts `Rejected` is terminal, but `Approve()` accepts it |
| N12 | ⚪ Low | GDPR | `EmailService.cs:179`, `BackgroundJobService.cs:81,486` | Raw exception objects still logged unmasked, unlike their message strings |

### 3.2 Status of the original 34 findings

| # | Original finding | Status | Evidence |
|---|---|---|---|
| 1 | Admin delete skips anonymization | ✅ Fixed | `RefTestDeletionMutations.cs:59-76` |
| 2 | Audit events soft-archived, never cleared | 🟡 Partial | New rows redacted; historical rows never are → **N1** |
| 3 | Email, scores and token logged | ✅ Fixed | `ServiceLoggerMessages.cs:1-260` |
| 4 | `PendingApproval`/`Rejected` never erased | ✅ Fixed | `PrivacyRetentionService.cs:34-59` |
| 5 | No automated tests exist | ❌ Open | Still no test project in `RefTestManagement.slnx` |
| 6 | In-flight jobs not cancelled; payload retained | ✅ Fixed | `RefTestPrivacyErasureService.cs:80-85,133-138` |
| 7 | Cost limits disabled, no rate limiting | ❌ Open | `Program.cs:138-155` |
| 8 | ~5 min grace window after deadline | ❌ Open | `RefTestExpirationService.cs:137-142` |
| 9 | Script injection via PR title | ❌ Open | `.github/workflows/pr.yml:24-30` |
| 10 | PR workflow runs no tests | ❌ Open | `.github/workflows/pr.yml` |
| 11 | README advertises Vitest that does not exist | ❌ Open | `RefTestManagement.Ui/package.json` |
| 12 | Long-lived `GH_PAT` | ❌ Open | `beta-release.yml:21-24`, `stable-release.yml:22-24` |
| 13 | Domain transitively depends on EF Core | ❌ Open | `RefTestManagement.Domain` → `AuditLog` |
| 14 | Job claim is not atomic | ❌ Open | `BackgroundJobService.cs:110-143` |
| 15 | No concurrency token | ❌ Open | `RefTest.cs:18-22`, `Job.cs:8-20` |
| 16 | Write and enqueue not atomic | ❌ Open | `RefTestCreationMutations.cs:57-73` |
| 17 | No timeout/resilience on HTTP clients | ❌ Open | `Auth0ServiceExtensions.cs:24` |
| 18 | Expiration loads all active tests into memory | ❌ Open | `RefTestExpirationService.cs:75-79` — see §6 correction |
| 19 | Positive `tabindex` in create form | ❌ Open | `create-ref-tests.html:47,84,136,398,421` |
| 20 | Hardcoded English `confirm()` | ❌ Open | `can-deactivate-ref-test.guard.ts:9` |
| 21 | Token uses `Guid.NewGuid()` | ❌ Open | `RefTest.cs:40` — see §6 correction |
| 22 | Anonymization nulls consent proof | ✅ Fixed | `RefTest.cs:559-577` |
| 23 | No composite index for retention scans | ❌ Open | `RefTestConfiguration.cs` |
| 24 | Re-scans anonymized rows forever | ✅ Fixed | `PrivacyRetentionService.cs:34-59` |
| 25 | Datepicker modal is mouse-only | ❌ Open | `datepicker-calendar.html:1-27` |
| 26 | `<select>` has no label | ❌ Open | `ref-test-navigation.html:1-14` |
| 27 | `console.error` in production | ❌ Open | `global-error-handler.ts:17` |
| 28 | Two i18n key typos | ❌ Open | `fr.json`, `de.json` |
| 29 | 24–38 untranslated strings per language | ❌ Open | `nl/fr/de.json` |
| 30 | No Apollo `ErrorLink` or `keyFields` | ❌ Open | `app.config.ts:19-20,164-192` |
| 31 | Bare `setTimeout` not tied to destroy | ❌ Open | `ref-test.store.ts` |
| 32 | `BackgroundJobService` god class | ❌ Open | `BackgroundJobService.cs:1-420` |
| 33 | Stale `sharp` allow-scripts pin | ❌ Open | `package.json` |
| 34 | No Dependabot config, no CodeQL | ❌ Open | `.github/` |

---

## 4. GDPR compliance — ❌ Still not compliant (materially improved)

> **Status: the gap narrowed from six issues to one, but that one is unresolved.** Erasure,
> retention, consent proof and logging are now implemented correctly for data created from this
> commit onward. Compliance is still blocked because **personal data already written to the
> audit log before this deployment will never be removed** (N1), which is a continuing failure
> of **Art. 5(1)(e)**. A second, smaller transparency gap (N7) also needs closing.
> Assessed at the application layer only; controller-level obligations remain out of scope.

### 4.1 Previously reported gaps — verification

| Gap | Article | Verdict | Evidence |
|---|---|---|---|
| 1. Admin delete never anonymizes | Art. 17 | ✅ Closed | `RefTestDeletionMutations.cs:59-76` calls `EraseAsync` before `DeleteAsync` |
| 2. Audit events only flagged, never cleared | Art. 5(1)(e) | 🟡 Partially closed | `AuditLogCleanupService.cs:46-83` redacts `Data`/`ActorName`/`ActorEmail` — but only for rows not yet archived (**N1**) |
| 3. Personal data in application logs | Art. 5(1)(c), 32 | ✅ Closed | `ServiceLoggerMessages.cs:1-260`; call sites mask or pass ids |
| 4. `PendingApproval`/`Rejected` retained forever | Art. 5(1)(e) | ✅ Closed | `PrivacyRetentionService.cs:34-59` |
| 5. Withdrawal does not stop in-flight email | Art. 7(3), 17 | ✅ Closed | `RefTestPrivacyErasureService.cs:80-85,133-138`; residual delivery window is disclosed |
| 6. Anonymization destroys consent proof | Art. 7(1) | ✅ Closed | `RefTest.cs:559-577` retains version and timestamp |

### 4.2 N1 — Historical audit rows keep personal data forever 🟠 High

**Art. 5(1)(e) — storage limitation.** `AuditLogCleanupService.cs:77` selects rows with
`a.Timestamp < cutoff && !a.IsArchived`. The implementation it replaced was:

```csharp
.Where(a => a.Timestamp < cutoff && !a.IsArchived)
.ExecuteUpdateAsync(s => s.SetProperty(e => e.IsArchived, true), cancellationToken);
```

That set `IsArchived = true` and redacted nothing. Cleanup is enabled by default
(`EnableCleanup = true`, `RetentionDays = 90`), so any deployment that has been running for more
than 90 days already has a population of archived rows carrying `firstName`, `lastName`, `email`
and the actor's name and address. The new loop's predicate excludes exactly those rows.

The result is that the fix works perfectly for future data and not at all for existing data —
and because `IsArchived` is doing double duty as both "retention applied" and "redaction
applied", nothing distinguishes a redacted row from an un-redacted one.

**Fix.** Separate the two concepts. Add a `RedactedAt` (or schema version) column and use it as
the redaction cursor instead of `IsArchived`, then let the loop sweep everything past the cutoff.
A one-off backfill that resets `IsArchived = false` for old rows would also work but conflates
the two meanings again. Note this requires a migration in **all four** provider projects.

### 4.3 N7 — Undisclosed recipients of participant data 🟡 Medium

**Art. 13(1)(e) — recipients or categories of recipients.** `docs/PRIVACY.md:36-42` lists the
three processors (Auth0, Brevo, IHF) but nowhere states that participant names, email addresses,
scheduled times and scores are sent by email to internal staff: approval-request and
approval-decision notifications (`BackgroundJobService.cs:435-470`) and batch staff reports
(`EmailService.cs:186-216`). Participants are not told their results circulate this way.

**Fix.** Add a "Recipients" subsection to `docs/PRIVACY.md` and the published notice, naming the
category (assessment administrators and approvers within Handball Belgium) and the purpose.

### 4.4 N5 — Redaction key matching is case-sensitive 🟡 Medium

`AuditPiiRedactor.cs:25` matches `"firstName"`, `"lastName"`, `"email"`, and
`JsonObject.TryGetPropertyValue` is case-sensitive (`:52`). The audit interceptor's property-diff
path writes raw EF property names — `"FirstName"`, `"Email"` — and `JsonOptions` sets
`PropertyNamingPolicy` but not `DictionaryKeyPolicy`, so those keys serialize in PascalCase.

This is **latent, not active**: `RefTest` and `RefTestTitle` implement `IHasDomainEvents` so the
diff path is unreachable for them today, and `Job` is excluded from auditing entirely
(`Program.cs:53`). But the redactor's own documentation claims to cover entity-change payloads,
so the first audited entity that carries a name or email will silently store unredactable PII.

**Fix.** Match keys case-insensitively, or add the PascalCase variants to `PiiKeys`.

### 4.5 Remaining limits, correctly disclosed

`docs/PRIVACY.md` gained a "Known limits of erasure" section in the same commit. Its four claims
were checked against the code and are accurate: the in-flight delivery window, the 7-day
retention of completed job payloads, audit redaction happening on schedule rather than on
request, and the note that pre-existing logs may still contain personal data. No statement in
the document is now false — the omission in §4.3 is the only documentation gap.

---

## 5. New findings in detail

### 5.1 N2 — Cleared payload can break admin mutations 🟠 High

`Job.Cancel()` (`Job.cs:77-84`) now sets `Payload = string.Empty`. The sequence that breaks:

1. A worker claims a job → `Status = Processing`, payload intact in the DB
   (`BackgroundJobService.cs:137`).
2. An erasure or reset concurrently calls `Cancel()` → `Status = Failed`, `Payload = ""`,
   committed from a different `DbContext`.
3. The worker's own send then fails, so it calls `MarkAsFailed` (`Job.cs:61-67`), which sets
   `Status = Attempts >= maxAttempts ? Failed : Pending`. With the default `MaxAttempts = 3`, the
   job goes back to **`Pending`**. The worker's in-memory entity still holds the old payload, but
   EF only writes modified properties, so the database keeps `Payload = ""`.
4. The result is a `Pending` job with an empty payload.

`JobEnqueueService.CancelPendingJobsForRefTestAsync` (`:124-152`) and
`CancelPendingResultEmailsAsync` (`:178-190`) then select **every** `Pending`/`Processing` job of
the relevant types and call `JsonSerializer.Deserialize<T>(job.Payload, …)` on each, with no
try/catch. Deserializing `""` throws `JsonException`. Because the loop covers all jobs of that
type and not just the target's, one poisoned row makes `resetRefTest` and `reviveRefTest` fail
for **unrelated** RefTests. `BackgroundJobService.DeserializePayload` (`:481`) hits the same
throw and burns all three retry attempts.

**Fix.** Skip empty payloads defensively at the top of both loops
(`if (string.IsNullOrEmpty(job.Payload)) continue;`) and stop `MarkAsFailed` resurrecting a
cancelled job — cleanest is a distinct `JobStatus.Cancelled` that `MarkAsFailed` cannot leave.

### 5.2 N3 — Batch loop never clears the change tracker 🟡 Medium

`AuditLogCleanupService.cs:74-101` loads 500 rows per batch into the same `DbContext` and never
calls `ChangeTracker.Clear()`. Tracked entities accumulate across every batch, so
`SaveChangesAsync`'s `DetectChanges` becomes O(total tracked) per batch — O(N²/500) overall —
with memory growing for the whole run. The first run after deployment processes the entire
backlog in one pass, which is precisely the worst case. The code it replaced was a single
`ExecuteUpdateAsync`, so this is a measurable regression.

**Fix.** `context.ChangeTracker.Clear();` after each `SaveChangesAsync` inside the loop.

### 5.3 N4 — Regex timeout can strand a job in `Processing` 🟡 Medium

`BackgroundJobService.cs:189` calls `LogRedaction.MaskEmailsInText(ex.Message)` as the first
statement inside the `catch` block. That call can throw `RegexMatchTimeoutException` (the pattern
carries a 250 ms timeout, `ServiceLoggerMessages.cs:49`). If it does, `MarkAsFailed` and the
subsequent save never execute, leaving the row at `Status = Processing`.

Nothing recovers from that state: `ProcessJobsAsync` only ever selects `Status == Pending`
(`:110-113`), and `CleanupOldJobsAsync` only deletes `Completed`/`Failed` (`:515-517`). There is
no stale-lock reclaim, so the job is never sent, never retried and never cleaned up. Probability
is low — it needs a pathological exception message — but the failure is permanent and silent.

**Fix.** Wrap the masking call in its own `try/catch` falling back to a constant string, and
separately add a reclaim for `Processing` jobs whose `LockedUntil` has expired.

### 5.4 N6 — Erase and delete are not atomic 🟡 Medium

`RefTestDeletionMutations.cs:70-72` calls `EraseAsync` then `DeleteAsync`. Each opens its **own**
execution-strategy transaction (`RefTestPrivacyErasureService.cs:63,110`). If the delete fails on
a constraint, a transient fault or cancellation, the erasure has already committed: the record
survives as anonymized, showing `***` in the admin UI, while the mutation reports only a generic
failure string for that id. Staff are not told the participant's data was already destroyed.

**Fix.** Combine into a single `EraseAndDeleteAsync` sharing one transaction, or surface the
half-done state explicitly in `DeleteRefTestError`.

### 5.5 N8 — Unbounded input on public participant mutations 🟡 Medium

`SaveRefTestProgressInput.cs:1-6` and `CompleteRefTestInput.cs:1-5` accept `SelectedAnswerIds`,
`CurrentQuestionIndex` and `Token` with no length, size or range validation
(`RefTestLifecycleMutations.cs:147-182`). These are reachable anonymously with only an invitation
token. Combined with finding #7 (GraphQL cost limits disabled, no rate limiting), this is an
abuse surface: large lists amplify database and scoring work.

**Fix.** Bound the collection size and validate the index range and token length at the input
boundary.

### 5.6 N9 — Invitation token travels in the URL 🟡 Medium

The participant route is `/ref-test/:token` (`app.routes.ts:60-65`) and the token is copied into
component state and the store (`ref-test-welcome.ts:106`, `take-ref-test.ts:49`,
`ref-test.store.ts:12`). The token is a bearer credential granting full access to the
participant's assessment, so placing it in the path exposes it to browser history, shoulder
surfing, screenshots and `Referer` headers on any outbound navigation.

This is inherent to emailing a clickable link and is a common, accepted design — but it should be
a deliberate decision. **Fix.** At minimum set a strict `Referrer-Policy` and avoid outbound
links on token-bearing pages; ideally exchange the token for a short-lived session cookie on
first load and drop it from the URL.

### 5.7 Low-severity findings

- **N10** (`RefTestPrivacyErasureService.cs:55`) — `ParticipantActorEventTypes` includes
  `RefTestAnonymizedEvent`. Now that admin deletes call `EraseAsync`, the admin's identity on
  that event is overwritten with `***`, losing the "who erased this" record. Mitigated because
  the later `RefTestDeleted` event still carries the admin actor. Redact the actor only when the
  request was anonymous.
- **N11** (`PrivacyRetentionService.cs:54-58`) — the comment justifying the new clause asserts
  that `PendingApproval` and `Rejected` are terminal. `RefTest.Approve()` explicitly accepts a
  `Rejected` test and returns it to `Pending` (`RefTest.cs:163`), so `Rejected` is *not*
  terminal. The behaviour is still defensible after the full retention period — but once erased,
  `Approve()` throws on `IsAnonymized` and the test can never be revived. Correct the comment and
  confirm the intent.
- **N12** (`EmailService.cs:179`, `BackgroundJobService.cs:81,486`) — WP-03 scrubbed exception
  *message strings* but the full exception object is still passed to `ILogger`, and its
  `ToString()` is not masked. Whether it can carry an address is provider-dependent, so this is
  a completeness gap rather than a confirmed leak.

---

## 6. Corrections to the previous report

Two entries in `AUDIT.md` were re-examined and need qualifying:

- **#18 — "Expiration service evaluates client-side."** Still open, and the description is
  accurate. `RefTestExpirationService.cs:75-79` does prefilter on status and `IsAnonymized` in
  SQL, but it then materialises every remaining active test and evaluates `IsExpired` in memory
  (`:92-97`). An initial re-check of this finding wrongly concluded it had been fixed; direct
  reading of the current code confirms it has not. No commit has touched this file.

- **#21 — "Token uses `Guid.NewGuid()`."** Remains open but should stay ⚪ Low and not be
  escalated. On .NET Core and later, `Guid.NewGuid()` is backed by a cryptographic RNG, so the
  practical predictability risk is low. The finding stands on the narrower ground that Microsoft
  does not *guarantee* GUIDs are suitable for security tokens and that 122 bits of entropy with
  no explicit CSPRNG contract is a fragile thing to rely on.

---

## 7. Verified correct

Checked in this re-audit and found genuinely sound — worth protecting during future changes:

**Phase 1 implementation**
- The batched redaction loop cannot infinite-loop, skip or reprocess rows: `IsArchived` always
  flips `false → true`, so EF always marks the row modified and the cursor always advances.
- `AuditPiiRedactor.RedactData`'s reference-equality optimisation is handled correctly at both
  call sites; `RefTestPrivacyErasureService.cs:150` compares by string *value*.
- `JsonNode.Parse` on `AuditEvent.Data` cannot throw — `Data` is only ever written by
  `JsonSerializer.Serialize` or left null.
- WP-02's boolean grouping is correctly parenthesised; `InProgress` and `Pending` cannot match.
  `CreatedAt` is a sound cutoff basis because `Approve()` resets it.
- WP-06 cannot bypass a guard: every reader of the consent fields is behind an `IsAnonymized`
  check that throws first.
- `refTest` remains validly tracked across `EraseAsync`'s internal `ReloadAsync`, so the
  subsequent `DeleteAsync` is correct. Capturing the DTO before erasure is necessary.
- `MaskEmailsInText` has no nested quantifier and therefore no exponential ReDoS blowup; the
  250 ms timeout bounds the polynomial worst case.
- The `.husky/pre-commit` diagnosis and fix are both correct.

**Pre-existing**
- No admin GraphQL operation is missing authorization; the public participant surface is explicit
  and narrow.
- Read paths use `AsNoTracking()` consistently, including DataLoaders.
- No XSS sinks in the frontend — no `innerHTML`, `bypassSecurityTrust*`, `document.write` or
  `eval` anywhere in `src/app`.
- Frontend teardown is disciplined: `takeUntilDestroyed` is used consistently alongside zoneless
  change detection.
- All third-party GitHub Actions are SHA-pinned across every workflow.
- No real secrets are committed; `appsettings.json` placeholders are empty.

---

## 8. Prioritized remediation

**Before merging this branch**

1. **N1** — split "archived" from "redacted" and sweep the historical backlog. This is the
   difference between the GDPR fix working and only appearing to work. Requires a migration in
   all four provider projects.
2. **N2** — guard against empty payloads and stop `MarkAsFailed` resurrecting cancelled jobs.
3. **N3** — one line: clear the change tracker per batch.
4. **N4** — make the masking call in the `catch` block non-throwing.

**Next**

5. **N6**, **N5**, **N11**, **N10** — finish hardening the Phase 1 surface while it is still
   fresh.
6. **N7** — add the recipients section to `docs/PRIVACY.md`; small, and it closes the last
   documentation gap.
7. **Finding #5 — automated tests.** Both High-severity regressions in this report are the kind
   a test suite catches mechanically. Phase 5 of `AUDIT-REMEDIATION.md` currently schedules the
   test harness late; this re-audit is evidence for moving it earlier.

**Then** continue with Phase 2 onward of [`AUDIT-REMEDIATION.md`](./AUDIT-REMEDIATION.md), whose
work packages still cover findings #7 through #34 unchanged.

---

## 9. Closing note

The Phase 1 work was directionally right and the hard parts — the erasure ordering, the consent
retention, the log scrub — were done correctly and verified. What it lacked was a second pair of
eyes and an automated safety net.

The most useful conclusion in this report is not any individual finding. It is that a change set
written specifically to fix a compliance problem shipped a regression that silently prevented
that same fix from applying to existing data, and nothing in the repository would have caught it.
That is a process finding, and the remedy is finding #5.
