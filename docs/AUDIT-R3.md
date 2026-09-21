# Deep Audit — Round 3

| | |
|---|---|
| **Repository** | `handballbelgium-referees/ref-test-management` |
| **Date** | 2026-09-18 |
| **Commit audited** | `555c149` (branch `docs/audit-and-remediation-plan`) |
| **Previous reports** | [`AUDIT.md`](./AUDIT.md) (R1, 34 findings) · [`AUDIT-R2.md`](./AUDIT-R2.md) (R2, 12 findings) |
| **Remediation tracker** | [`AUDIT-REMEDIATION.md`](./AUDIT-REMEDIATION.md) (39 work packages) |
| **Scope** | Adversarial verification of all 39 work packages **plus** a fresh full-stack sweep (backend, frontend, security, GDPR, CI/CD, test quality) |
| **Method** | Read-only. No repository code was modified during this audit. |

---

## Executive summary

**Production readiness: NOT READY — one High-severity unauthenticated data-disclosure path must be closed before this service handles real participant data.**

The codebase is in substantially better shape than at R1 or R2. Thirty-nine work packages were delivered, the build is clean at zero warnings, 160 backend and 33 frontend tests pass, and the great majority of previously reported findings genuinely hold closed. The engineering quality of the remediation is high: the code carries unusually good explanatory comments, the domain model is coherent, and several fixes are more thorough than the findings required.

That said, this round found **14 confirmed issues**, including **four rated High**. The most serious is a Relay `node(id:)` resolver that returns a participant's name, email address, score and submitted answers to a caller with **no authentication whatsoever**. This is a textbook broken-object-level-authorization flaw, and it exists because field-level authorization on `RefTestType` was applied by hand, inconsistently, with no test to enforce it.

Three of the four High findings are newly introduced by the remediation work itself, not pre-existing defects. That is the central lesson of this round: the fixes were sound in intent but two of them silently broke guarantees that other fixes depended on.

This audit also **corrects the remediation tracker's claim that all 46 prior findings are closed**. Three work packages did not fully hold — WP-15, WP-20 and WP-35 — and are re-opened below with evidence.

### Verdict by area

| Area | Verdict |
|---|---|
| Domain model & business logic | Good, with one genuine inconsistency (three competing deadline definitions) |
| GraphQL authorization | **Weakest area.** Inconsistent, untested, one unauthenticated disclosure path |
| Background jobs & transactions | Good design undermined by one context-scoping bug |
| GDPR / privacy | Strong documentation; two real gaps (erasure coverage, notice completeness) |
| Frontend correctness | One unbounded retry loop; otherwise sound |
| Accessibility | Partially remediated — 11 of 18 dialogs still unlabelled, no focus management anywhere |
| CI/CD | Solid; one minor hardening item |
| Test coverage | **Material gap** — zero authorization tests, zero audit-retention tests |

---

## Findings

| # | Severity | Area | Finding |
|---|---|---|---|
| R3-01 | 🔴 High | Security | `node(id:)` returns participant name, email, scores and answers unauthenticated |
| R3-02 | 🔴 High | Jobs | Expiration auto-complete stages the result email in a different `DbContext` |
| R3-03 | 🔴 High | Security | Rate-limit partition collapses to a single bucket behind a reverse proxy |
| R3-04 | 🔴 High | Frontend | Auto-submit has no in-flight guard and fires once per second indefinitely |
| R3-05 | 🟡 Medium | GDPR | Report-email job payloads carry no `RefTestId`, so erasure can never cancel them |
| R3-06 | 🟡 Medium | Domain | Three divergent deadline predicates; the sweep ignores the grace period the domain grants |
| R3-07 | 🟡 Medium | Jobs | `MarkAsExpired` is enqueued for in-progress tests, where `Expire()` always throws |
| R3-08 | 🟡 Medium | A11y | 11 of 18 dialogs lack modal semantics; no focus trap, Escape or focus restore anywhere |
| R3-09 | 🟡 Medium | GDPR | In-app privacy notice omits the internal recipient categories `PRIVACY.md` requires |
| R3-10 | 🟡 Medium | Security | Open redirect via unvalidated `returnUrl` in `AccountController` |
| R3-11 | 🟡 Medium | Testing | Zero authorization tests; zero audit-retention/interceptor tests |
| R3-12 | 🟡 Medium | Frontend | Detail subscription silently ignores five union members |
| R3-13 | ⚪ Low | CI/CD | README-sync job runs PR-branch code with `contents: write` |
| R3-14 | ⚪ Low | Security | Error filter logs raw exception objects; safe only by convention |

---

## High severity

### R3-01 — `node(id:)` returns participant PII with no authentication

**Files:** `RefTestManagement.Api/Program.cs:235-236`, `RefTestManagement.Api/Graphql/Types/RefTestType.cs:21-24,30-33`, `RefTestManagement.Api/Graphql/Queries/DataLoaders.cs:9-21`

The schema enables Relay global object identification:

```csharp
.AddGlobalObjectIdentification(true)
.AddAuthorization()
```

`AddAuthorization()` is registered bare — there is no `FallbackPolicy`, no `DefaultPolicy` override, and no `RequireAuthorization()` on the GraphQL endpoint. The endpoint must stay anonymously reachable because the participant flow is anonymous, so nothing gates entry.

`RefTestType` then opts into the `node` field:

```csharp
descriptor.ImplementsNode()
    .IdField(x => x.Id)
    .ResolveNode((ctx, id) => ctx.DataLoader<RefTestByIdDataLoader>().LoadAsync(id, ctx.RequestAborted))
```

`RefTestByIdDataLoader` applies **no authorization filter** — it loads any id it is given. The normal query path `GetRefTest` is correctly guarded with `[Authorize(Policy = Permissions.RefTests.ViewDetail)]`, but `node` reaches the same data without passing through it.

What is then readable depends entirely on per-field `.Authorize()` calls, and those were applied inconsistently. Fields carrying **no** authorization include:

`Title`, **`name`** (the `FullName` projection), **`Email`**, `MaxTimeInMinutes`, `NumberOfQuestions`, `StartedAt`, **`Percentage`**, **`QuestionScore`**, **`AnswerScore`**, `QuestionTotal`, `AnswerTotal`, **`WrongQuestionIds`**, **`WrongAnswerIds`**, `SendResultsAutomatically`, `CurrentQuestionIndex`, **`SelectedAnswerIds`**, **`questions`**

So this query succeeds for an anonymous caller:

```graphql
query { node(id: "<base64 RefTest id>") { ... on RefTest {
  name email percentage questionScore answerScore selectedAnswerIds wrongAnswerIds
} } }
```

Two details make this worse than a simple oversight:

1. **`FirstName` and `LastName` *are* authorized, but `name` and `Email` are not.** The concatenated projection of two protected fields is unprotected. This is almost certainly accidental, and it is exactly the kind of drift a test would have caught — see R3-11.
2. **`.Authorize()` with no argument means "authenticated", not "permitted".** Every field that *is* guarded here is guarded only by authentication, not by the `Permissions.RefTests.*` policies used on the query roots. Any authenticated Auth0 account — including one with no RefTest permissions at all — can read `Status`, `RejectionReason`, `ScheduledAt`, `IsAnonymized` and the rest through `node`.

**On exploitability.** The id is a `Guid`, so this is not brute-forceable and I am rating it High rather than Critical. But the GUID is treated as a non-secret identifier everywhere else in the system — it appears in staff URLs (`/ref-tests/{id}`, hence in browser history, referrer headers, screenshots and shared links), in the participant's own `refTestByToken` response, and inside approval-notification emails sent to approvers (`ApprovalNotificationRefTestItem.Id`). `node` quietly promotes that identifier into a permanent bearer credential for personal data — one that keeps working after the invitation token is regenerated and after the assessment completes.

**Recommendation.** Either drop `ImplementsNode()` from `RefTestType`, or apply the `ViewDetail` policy at the node resolver and audit every field's authorization against the permission model. Then add the tests in R3-11 so the field set cannot drift again.

---

### R3-02 — Expiration auto-complete stages the result email in the wrong `DbContext`

**Files:** `RefTestManagement.Api/BackgroundServices/JobHandlers/RefTestExpirationJobHandler.cs:23-35,63-74`, `RefTestManagement.Api/Graphql/Mutations/Lifecycle/RefTestLifecycleMutations.cs:263-288`, `RefTestManagement.Infrastructure/Services/JobEnqueueService.cs:57,85-87`, `RefTestManagement.Api/BackgroundServices/BackgroundJobService.cs:235-238`

`CompleteRefTestCoreAsync` promises atomicity in a comment, and delivers it for the participant path:

```csharp
// The completion and the result email it owes are committed together: a completed test
// whose result job was lost never reports back to the participant.
...
await jobEnqueueService.EnqueueResultEmailAsync(payload, scheduledAt, cancellationToken, saveChanges: false);
await context.SaveChangesWithRetryAsync(cancellationToken);
```

The guarantee holds only when `context` and the context inside `jobEnqueueService` are the same instance. In the participant mutation they are — both come from the request scope.

`RefTestExpirationJobHandler` breaks that. It deliberately creates its own context from the factory, and documents why:

```csharp
await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
```

> *This handler uses `IDbContextFactory<TContext>` rather than the scoped context. The auto-complete path runs the same mutation core participants do, which tracks a graph of its own, and it must not share a change tracker with the worker loop that owns the job row.*

The reasoning is sound, but `IJobEnqueueService` is still the scoped one — `JobEnqueueService(RefTestManagementContext context, ...)` holds the **worker scope's** context. So when the handler calls `CompleteRefTestCoreAsync`:

- the `RefTest` completion is written to the **handler's** context and committed;
- the result-email `Job` row is added to the **worker's** context and left uncommitted.

The job row is not lost in the happy path — `ProcessJobAsync` later calls `job.MarkAsCompleted(); await context.SaveChangesWithRetryAsync(...)` on the worker context, which flushes it. But it is now a **separate transaction**, and the failure window is real:

> If the process crashes, is recycled, or is scaled down between the handler's save and the worker's save, the RefTest is `Completed` with no result-email job. The expiration job is reclaimed on restart, but the handler's own guard — `if (refTest.Status == RefTestStatus.Completed ... ) return;` — makes it return immediately. **The participant never receives their result, and nothing retries.**

This is precisely the class of bug WP-15 ("transactional enqueue") was written to eliminate, reintroduced by WP-19's handler split. The `saveChanges: false` flag is now a silent trap: it is correct at one call site and wrong at the other, and nothing in the type system distinguishes them.

**Recommendation.** Have the handler resolve an `IJobEnqueueService` bound to its own context (construct it explicitly, or make the enqueue service accept the context per call). Add a test that auto-completes via the expiration handler and asserts the result-email job is visible in the same transaction.

---

### R3-03 — Rate-limit partition collapses to one bucket behind a proxy

**Files:** `RefTestManagement.Api/Program.cs:108-130`, `RefTestManagement.Api/Program.cs:251-254`

The limiter partitions on the socket address:

```csharp
options.AddPolicy(graphQlRateLimiterPolicy, httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", ...
```

with a comment stating:

> *Behind a proxy this is the proxy's address **unless `UseForwardedHeaders` (configured below) has restored the original** — hence `EnableRateLimiting`, for deployments that already limit at the edge.*

The configuration below does not restore it:

```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
});
```

Only `XForwardedProto` is processed. `XForwardedFor` is not, so `Connection.RemoteIpAddress` remains the proxy's address in every deployment that terminates TLS upstream — Azure App Service, nginx, Cloudflare, any container ingress. Every client shares one fixed window.

The consequence is the inverse of the stated intent: rather than preventing one abusive client from locking out everyone, the limiter **guarantees** it. And the comment actively misleads the next reader into thinking the case is handled.

This compounds with **R3-04**: a handful of participants whose assessment timer expires while their connection is degraded will each emit one request per second forever, and every one of those requests lands in the same shared bucket as legitimate traffic.

**Recommendation.** Add `ForwardedHeaders.XForwardedFor` together with `KnownProxies`/`KnownNetworks` (or `ForwardLimit`) configured for the actual deployment — accepting `X-Forwarded-For` without restricting trusted proxies would let a client spoof its own partition key. Then correct the comment.

---

### R3-04 — Auto-submit has no in-flight guard and repeats once per second

**Files:** `RefTestManagement.Ui/src/app/ref-test/take/take-ref-test.ts:80-94`, `RefTestManagement.Ui/src/app/ref-test/take/state/ref-test.store.ts:170-177`

The countdown effect runs on every one-second tick:

```typescript
this.store.updateRemainingTime();

// Auto-submit when timer reaches 0
if (this.store.timeRemainingSeconds() === 0 && !this.store.completed()) {
  this._facade.submit();
}
```

and the clock clamps at zero:

```typescript
this.timeRemainingSeconds.set(Math.max(0, total - elapsed));
```

The clamp is deliberate — there is even a test asserting it, reasoning that a negative value would make the `=== 0` equality check miss and the assessment would never auto-submit. Correct. But it also means `timeRemainingSeconds() === 0` stays true on **every subsequent tick**.

The only guard is `completed()`, which flips after the mutation round-trip resolves. `submit()` is asynchronous and nothing tracks it as in-flight. So from the moment the timer hits zero, a new submit mutation is dispatched every second until one returns successfully.

- On a healthy connection: a handful of duplicate mutations. The domain guard rejects them (`Complete()` throws `InvalidRefTestStatusException` once the status is no longer `InProgress`), so there is no data corruption — but the participant can see a spurious error banner immediately after a successful submission.
- On a degraded or offline connection, or if the submit fails for any reason: `completed()` never flips and the loop **never terminates**. One request per second, per affected participant, with no backoff, for as long as the tab stays open.

Combined with R3-03 this is a plausible self-inflicted denial of service.

**Recommendation.** Track submission state explicitly (a `submitting` signal set before the call and cleared in a `finally`), and gate on `!submitting() && !completed()`. Surface a retry affordance rather than looping silently.

---

## Medium severity

### R3-05 — Report-email payloads cannot be reached by privacy erasure

**Files:** `RefTestManagement.Application/Models/JobPayloads.cs:42-65`, `RefTestManagement.Infrastructure/Services/RefTestPrivacyErasureService.cs:198-215` (and the parallel block at `:132-134`)

Erasure cancels in-flight jobs by substring-matching the RefTest id against the serialised payload:

```csharp
var refTestId = refTest.Id.ToString();
...
var cancellableJobs = await context.Jobs
    .Where(job => (job.Status == JobStatus.Pending || job.Status == JobStatus.Processing)
                  && job.Payload.Contains(refTestId))
    .ToListAsync(ct);
```

The surrounding comment is explicit about why this matters:

> *Cancelling also clears the job payload, which holds the participant's name, email, token, scores and answers — erasing the RefTest row alone would leave all of that sitting in the Jobs table.*

Every payload type carries a matchable id **except one**:

| Payload | Carries RefTest id? |
|---|---|
| `InvitationEmailPayload` | ✅ `RefTestId` |
| `ResultEmailPayload` | ✅ `RefTestId` |
| `RefTestExpirationPayload` | ✅ `RefTestId` |
| `ApprovalNotificationEmailPayload` | ✅ via `ApprovalNotificationRefTestItem.Id` |
| `ApprovalDecisionEmailPayload` | ✅ via `ApprovalNotificationRefTestItem.Id` |
| **`ReportEmailPayload`** | ❌ **none** |

`RefTestReportPayloadData` is `(TitleName, FirstName, LastName, StartedAt, CompletedAt, QuestionScore, QuestionTotal, AnswerScore, AnswerTotal, Percentage, Passed, Language, Duration)` — participant names and full results, with no identifier the filter can match.

Consequences, both GDPR Art. 17 failures:

1. A queued or in-flight report email **still sends** after the participant's data has been erased, disclosing their name and score.
2. The payload **persists in the `Jobs` table** carrying that data until normal job retention sweeps it, rather than being cleared at erasure time as the comment promises.

The recipients are internal staff who were already authorised to see this data before erasure, which is why I am rating this Medium rather than High. It remains processing after an erasure request, and the storage-limitation breach is unambiguous.

**Recommendation.** Add `Guid RefTestId` to `RefTestReportPayloadData`. Prefer this over broadening the filter — matching on names would be both unreliable and self-defeating. Consider replacing the substring match with a proper `JobRefTest` association column so this class of bug cannot recur.

---

### R3-06 — Three divergent deadline predicates

**Files:** `RefTestManagement.Domain/RefTests/RefTest.cs:45,664-666,346-361`, `RefTestManagement.Infrastructure/Queries/RefTestExpirationQueries.cs:39-52`

Three separate definitions of "past the deadline" coexist, and no two agree:

| Predicate | Rule |
|---|---|
| `RefTest.HasPassedDeadline()` (submit guard) | `now > StartedAt + MaxTimeInMinutes + 60s` — **includes** grace |
| `RefTest.IsExpired(...)` (token-query path) | `elapsed > MaxTimeInMinutes` — **no** grace |
| `RefTestExpirationQueries.IsDueForExpiration(...)` (sweep) | `StartedAt + MaxTimeInMinutes <= now` — **no** grace |

The 60-second `DeadlineGrace` exists for a documented and good reason:

> *…the participant's clock is not the server's; without this, an answer sent a fraction of a second before the deadline would be rejected.*

But the sweep does not honour it. In the 60-second window after the nominal deadline the participant is still permitted to submit, while the sweep already considers the test due and enqueues `AutoComplete`. Whichever lands first wins:

- Sweep first → the participant's final submission fails with `InvalidRefTestStatusException`, and answers they entered during the window they were explicitly granted are silently discarded in favour of the last autosave.
- Participant first → benign; the handler's status guard returns early.

If the sweep interval is a minute or less, this window is entered routinely rather than exceptionally.

**Recommendation.** Express the deadline once. `IsDueForExpiration` should add the same grace, and `IsExpired` should either delegate to `HasPassedDeadline` or be deleted in favour of it. A single shared constant with one translation to SQL removes the whole class of drift.

---

### R3-07 — `MarkAsExpired` is enqueued for in-progress tests, where it always throws

**Files:** `RefTestManagement.Api/Graphql/Queries/RefTestQueries.cs:71-79`, `RefTestManagement.Domain/RefTests/RefTest.cs:333-344,346-361`, `RefTestManagement.Api/BackgroundServices/JobHandlers/RefTestExpirationJobHandler.cs:63-80`

`GetRefTestByTokenAsync` reaches this branch only when the test *is* expired, and unconditionally picks the `MarkAsExpired` action:

```csharp
if (!refTest.IsExpired(configuration.ExpirationIfNotStarted))
    return refTest.ToDto();

await jobEnqueueService.EnqueueRefTestExpirationAsync(
    new RefTestExpirationPayload(refTest.Id, RefTestExpirationAction.MarkAsExpired), ...);
```

`Completed` already returned above, so the status here is `Pending` **or `InProgress`**. And `IsExpired` returns `true` for an overdue in-progress test — its own comment says so:

```csharp
// In-progress tests that exceed the time limit are auto-completed, not expired
var elapsedTime = DateTime.UtcNow - StartedAt.Value;
return elapsedTime.TotalMinutes > MaxTimeInMinutes;
```

But `Expire()` rejects anything that is not `Pending`:

```csharp
if (Status != RefTestStatus.Pending)
    throw new InvalidRefTestStatusException("Only pending RefTests can be marked as expired");
```

So for an in-progress overdue test the handler throws, the job is retried, and it fails again on every attempt until it is marked permanently failed. The action should have been `AutoComplete`.

The test still closes eventually because the sweep enqueues its own correctly-actioned job. The damage is a stream of guaranteed-failing jobs and error-level log noise that masks real failures — and if the sweep is disabled or lagging, this path never closes the test at all.

**Recommendation.** Choose the action from the status at the call site, mirroring the sweep's logic — `InProgress` → `AutoComplete`, `Pending` → `MarkAsExpired`. Better still, let the handler derive the action from the row it loads and remove the choice from the payload entirely, so the two paths cannot disagree.

---

### R3-08 — Most dialogs are not exposed as modals, and none manage focus

**Files:** `RefTestManagement.Ui/src/app/ref-test/take/components/submit-ref-test-dialog/submit-ref-test-dialog.html:1-16`, `.../leave-ref-test-dialog/`, `.../ref-test/components/withdraw-consent-dialog/`, `RefTestManagement.Ui/src/app/ref-tests/list/components/dialogs/*` (8 dialogs), versus `RefTestManagement.Ui/src/app/ref-tests/detail/components/ref-test-detail-tab/components/dialogs/*` (7 dialogs)

WP-20 added modal semantics to the seven detail-tab dialogs:

```html
role="dialog"
aria-modal="true"
aria-labelledby="dialog-title"
```

The other **eleven** dialogs did not get them. Every list-view dialog (approve, reject, delete, reset, revive, send-invitations, send-results, generate-report) and all three participant-facing dialogs (submit, leave, withdraw-consent) are plain `div`s:

```html
@if (show()) {
<div class="fixed inset-0 bg-black/60 ..." (click)="cancel.emit()">
  <div class="bg-white rounded-xl ..." (click)="$event.stopPropagation()">
    <div class="px-6 py-4 border-b ...">
      <h2 class="text-xl font-bold ...">{{ 'ref_test.dialog.submit.title' | translate }}</h2>
```

A screen reader announces nothing when these open; the heading is not associated with any dialog role; and nothing tells assistive technology the content behind the overlay is inert.

Separately — and affecting **all eighteen** dialogs, including the seven that were fixed — there is **no focus management anywhere in the codebase**. A search for `cdkTrapFocus`, any focus-trap implementation, `Escape` key handling, or a programmatic `.focus()` call across the dialog components returns nothing. So:

- opening a dialog leaves focus on the trigger behind the overlay;
- <kbd>Tab</kbd> walks straight out of the dialog into the page underneath;
- <kbd>Esc</kbd> does not close it — the only dismissal is a mouse click on the backdrop or a button;
- closing does not restore focus.

For a keyboard-only or screen-reader participant, the submit-confirmation dialog in the assessment flow is effectively unusable. This is WCAG 2.1 AA territory (2.1.2 No Keyboard Trap, 2.4.3 Focus Order, 4.1.2 Name/Role/Value), which matters for a Belgian federation service.

**Recommendation.** Extract one shared dialog wrapper component that owns `role`/`aria-modal`/`aria-labelledby`, focus trapping, initial focus, <kbd>Esc</kbd>, and focus restore, then route all eighteen through it. Angular CDK's `A11yModule` (`cdkTrapFocus`) or `FocusTrapFactory` does most of this. Doing it once is also the only way to stop the set drifting apart again.

---

### R3-09 — In-app privacy notice omits internal recipient categories

**Files:** `RefTestManagement.Ui/public/i18n/en.json` (`privacy.recipients.*`) and its translated counterparts, versus `docs/PRIVACY.md:33-40`

`docs/PRIVACY.md` is unambiguous that internal recipients must be named:

> **Recipients** — Beyond the service providers above, participant data is disclosed to people inside Handball Belgium as part of running an assessment. **Art. 13(1)(e) requires these recipient categories to be named:**
> - **Assessment administrators.** Staff who create, schedule, and manage assessments see the participant's name, email address, scheduled time, status, and score in the application, and receive batch report emails summarising assessment results.
> - **Approvers.** Where an assessment requires approval, the designated approvers receive an approval-request email and an approval-decision email. Both carry the participant's name, email address, and scheduled time.

The notice the participant actually reads and must affirmatively accept before starting says only:

> **Service providers** — We use Auth0 for authentication, Brevo to deliver emails, and the IHF Rules Questions service to retrieve assessment content and calculate results. These providers process data only as needed to provide their services.

Processors are named; recipients are not. The section is titled "Service providers", so there is not even a heading under which a participant would expect to find them.

The rest of the in-app notice is good — controller and contact, data categories, purposes, legal basis, retention, and rights are all present and clearly written. This is a single gap, but it is a gap the project's own documentation identifies as a legal requirement, which makes it hard to characterise as an acceptable omission.

WP-35 delivered the documentation but not parity with the participant-facing notice.

**Recommendation.** Add a recipients paragraph to the `privacy.*` i18n block covering administrators and approvers, in all supported languages, and bump the notice version so existing acceptances are re-collected against the corrected text.

---

### R3-10 — Open redirect via unvalidated `returnUrl`

**File:** `RefTestManagement.Api/Controllers/AccountController.cs:13-17,31-37`

```csharp
[HttpGet("Login")]
public Task Login(string returnUrl = "/")
{
    return HttpContext.ChallengeAsync("Auth0", new AuthenticationProperties { RedirectUri = returnUrl });
}
```

`returnUrl` is taken from the query string and used verbatim as the post-authentication `RedirectUri`. There is no `Url.IsLocalUrl(returnUrl)` check. `Logout` has the same shape.

`/Account/Login?returnUrl=https://evil.example/` therefore sends the user through a genuine Auth0 login — real domain, real certificate, real credentials — and lands them on an attacker-controlled page afterwards. The authenticity of the first hop is what gives the second hop its credibility, which is why open redirects on login endpoints are worth more to a phisher than a bare redirect.

**Recommendation.** Reject non-local values:

```csharp
if (!Url.IsLocalUrl(returnUrl)) returnUrl = "/";
```

Apply to both actions.

---

### R3-11 — No authorization tests, no audit-retention tests

**Evidence:** across `RefTestManagement.UnitTests`, a search for `Authorize`, `Permission` or `policy` matches only `BackgroundJobProcessingTests.cs` (incidentally) and `packages.lock.json`. A search for `AuditLogCleanup`, `RedactedAt` or `AuditSaveChangesInterceptor` matches **nothing**.

The 160-test suite is genuinely good where it exists — the expiration-predicate tests even assert that every branch survives EF translation, which is exactly the right instinct. But two areas have no coverage at all:

**Authorization.** Nothing asserts that `Permissions.RefTests.ViewDetail` actually guards what it is supposed to, that `IsCorrect` and `Number` stay hidden from participants, that role-to-permission mapping is correct, or that a given field is reachable only by a permitted caller. R3-01 is the direct cost of this: the `name`/`Email` gap would have been caught instantly by a test that enumerated the anonymously-readable field set and compared it against an expected list.

**Audit retention and writing.** `AuditLogCleanupService` is untested, `RedactedAt` behaviour is untested, and the existing audit tests seed `AuditEvent` rows by hand — so `AuditSaveChangesInterceptor`, the component that decides what actually gets recorded, is never exercised. Given that the audit trail is load-bearing for the GDPR accountability story in `PRIVACY.md`, that is a meaningful gap.

**Recommendation.** Add (a) a schema-level test that snapshots which `RefTestType` fields resolve for an anonymous caller and fails on any addition, (b) policy tests per permission, and (c) interceptor and cleanup tests covering redaction and retention.

---

### R3-12 — Detail subscription silently ignores five union members

**File:** `RefTestManagement.Ui/src/app/ref-tests/detail/data/ref-test-detail-data.ts:292-328`

The detail view's subscription handler switches on the payload union and returns `{}` for anything it does not recognise. `RefTestApproved`, `RefTestRejected`, `RefTestReset`, `RefTestRevived` and `RefTestCreated` all fall through.

The subscription itself is correctly authorized (`ViewDetail`) and the list view's cache patching works, so this is narrow: a staff member with the detail view open while another staff member approves, rejects, resets or revives that assessment sees no update, and the page silently diverges from the server until it is reloaded.

Worth noting for the record: the earlier concern that Apollo union payloads were failing to normalise and that subscriptions never updated the UI was **re-confirmed as a false positive** this round. `ref-test-data.ts:406-610` patches the `RefTest` cache manually and does not rely on automatic normalisation. This finding is the genuine residue of that investigation.

**Recommendation.** Handle the remaining members, and add a default branch that logs rather than returning `{}` so the next added event type is noticed.

---

## Low severity

### R3-13 — README-sync job runs PR-branch code with `contents: write`

**File:** `.github/workflows/pr.yml:100-126`

```yaml
sync-readme-versions:
  permissions:
    contents: write
  steps:
    - uses: actions/checkout@... 
      with:
        ref: ${{ github.head_ref }}
    - run: node scripts/sync-readme-versions.mjs
```

The job checks out the pull request's branch and executes a script from it with a write-scoped token.

The trigger is `pull_request`, not `pull_request_target`, so **fork** pull requests receive a read-only `GITHUB_TOKEN` regardless of the `permissions:` block — the workflow's own comment acknowledges this. The exposure is therefore limited to same-repository branches, which require push access the actor already has. The step also runs `node` directly with no dependency installation, so a malicious manifest in a Renovate PR cannot execute.

That leaves this as hardening rather than a live vulnerability, and the workflow is otherwise well constructed — pinned action SHAs, least-privilege default `permissions: contents: read` at the top with an explanatory comment.

**Recommendation.** Run the sync script from the base ref rather than the PR head, or drop the write permission and fail the check instead of auto-committing.

---

### R3-14 — Error filter logs raw exception objects

**File:** `RefTestManagement.Api/Graphql/UnhandledExceptionLoggingErrorFilter.cs`

The filter passes `error.Exception` straight to the logger with no masking, while the job pipeline deliberately runs `LogRedaction.MaskEmailsInText(...)` before persisting or logging an exception message.

I investigated whether this leaks participant invitation tokens and **it does not** — see the retracted candidates below. Every site that throws a token-bearing exception declares it via `[Error<RefTestNotFoundException>]` / `[Error<RefTestExpiredException>]`, so HotChocolate's query and mutation conventions map them into typed payload errors and they never reach this filter as unhandled exceptions.

The residual issue is that this safety property is held together by convention alone. Any future exception type that carries personal data in its message and is *not* declared on its resolver will be logged verbatim, and nothing tests for it.

**Recommendation.** Apply the same `LogRedaction` masking here that the job pipeline uses. It costs nothing on the current exception set and removes the standing trap.

---

## GDPR assessment

**Overall: substantially compliant, with two gaps to close before production.**

The privacy work in this repository is better than most projects of this size. `docs/PRIVACY.md` is a genuine Art. 13/14 record rather than boilerplate — it reasons about legal bases per purpose, explains why the consent version and timestamp survive anonymization (Art. 7(1) demonstrability), documents the two-step erasure path, and is candid about what the system cannot do (recall a delivered email).

| Requirement | Status |
|---|---|
| Art. 6 legal basis | ✅ Documented per purpose; legitimate interests for invitation, consent for participation |
| Art. 7 consent + demonstrability | ✅ Affirmative acceptance enforced server-side; version and timestamp retained through anonymization |
| Art. 13 transparency | ⚠️ **R3-09** — in-app notice omits internal recipient categories |
| Art. 15 access | ⚠️ No self-service DSAR export; handled manually via the contact address. Acceptable at this scale, worth noting |
| Art. 17 erasure | ⚠️ **R3-05** — report-email jobs escape the erasure filter |
| Art. 25 data protection by design | ✅ Two-step erasure, audit redaction, token from a CSPRNG |
| Art. 30 records of processing | ✅ `PRIVACY.md` serves this purpose |
| Art. 32 security of processing | ⚠️ **R3-01** — unauthenticated read path to participant PII |
| Art. 28 processor agreements | ⏳ Open action for the controller: `PRIVACY.md` correctly flags that DPAs, hosting locations and transfer safeguards must be confirmed for Auth0, Brevo and IHF before production |

**R3-01 is the material GDPR item**, not just a security one: an unauthenticated path returning a named individual's email address and assessment results is an Art. 32 failure and a reportable personal-data breach if exploited.

---

## Verification of the 39 work packages

I attempted to break each work package rather than confirm it. **36 of 39 hold.** The three below did not fully hold and are re-opened.

| WP | Claim | Verdict |
|---|---|---|
| **WP-15** | Transactional job enqueue — completion and its result email commit together | ❌ **Re-opened.** Holds for the participant path; broken for the expiration handler by WP-19's context split. See **R3-02** |
| **WP-20** | Dialog accessibility | ❌ **Re-opened.** Only 7 of 18 dialogs received modal semantics, and no dialog received focus management. See **R3-08** |
| **WP-35** | Privacy notice completeness | ❌ **Partially held.** `PRIVACY.md` is thorough; the participant-facing notice was not brought to parity. See **R3-09** |

Work packages that held under adversarial testing, and were worth the effort to confirm:

- **WP-02 / privacy retention** — the `!IsAnonymized` guard is present on every relevant path, including the two erasure entry points.
- **WP-03 / log redaction** — `LogRedaction.MaskEmailsInText` is applied in the job pipeline, with a timeout on the regex and a documented fallback if masking itself throws. The reasoning in that catch block is correct and non-obvious.
- **WP-14 / concurrency** — `ClaimJobAsync` takes ownership in a single statement; the handler split preserved it.
- **WP-19 / job handler split** — the keyed-service refactor is clean and the unknown-job-type path reports by name rather than as a DI failure. Its only defect is the context-scoping consequence in R3-02.
- **Participant answer confidentiality** — `AnswerType.IsCorrect` is gated behind `Permissions.RefTests.ViewDetailQuestions` and `QuestionType.Number` likewise. I specifically tried to find a path by which a participant could read correct answers mid-assessment and there is none. This was reported to me as a finding and is false.

---

## Candidates investigated and rejected

Listed because a report is only as trustworthy as the claims it declines to make.

| Claim | Why it is wrong |
|---|---|
| Participant can read correct answers via the `questions` resolver | `AnswerType.IsCorrect` and `QuestionType.Number` are both permission-gated. Closed by design |
| Invitation tokens leak into application logs | Every throw site declares `[Error<T>]`, so token-bearing exceptions become typed payload errors and never reach the logging filter. See R3-14 for the residual convention risk |
| Admin subscriptions are unauthenticated | `onRefTestChanged` requires `ViewDetail`; `onRefTestStatusChanged` requires `ViewList`. Only the time-extension and session-lock subscriptions are anonymous, and both are legitimately part of the anonymous participant flow; neither carries PII |
| README-sync workflow is a High-severity token-exfiltration risk | Trigger is `pull_request`, so forks get a read-only token. Downgraded to Low (R3-13) |
| Apollo entities are not normalised / subscriptions never update the UI | Re-confirmed false. The cache is patched manually in `ref-test-data.ts:406-610`. The genuine residue is R3-12 |
| Anonymized rows collide on a shared `***` placeholder | Writes `erased-{Guid:N}`; no collision |
| Migration parity gap across providers | Re-checked, not reproducible |

Carried forward from R1/R2 and still rejected: stale `allowScripts` pin (inert, since removed), slow pre-commit build (6.1–6.4s, compilation not bundling), missing Dependabot config (Renovate is used deliberately), datepicker closing only on mouse (Escape works).

---

## Correction to the remediation tracker

`docs/AUDIT-REMEDIATION.md` currently states **"46 closed · 0 open"** and **"39 of 39 work packages shipped"**.

The second statement is accurate — all 39 were implemented. The first is not: WP-15, WP-20 and WP-35 did not fully deliver the outcomes their findings required, and the accurate position at `555c149` is:

> **43 closed · 3 re-opened (WP-15, WP-20, WP-35) · 14 new findings from R3**

I would recommend correcting the tracker rather than leaving it overstated — it is the document a future maintainer will trust, and a remediation tracker that reports its own success is worth less than one that reports its own misses.

---

## Recommended sequencing

**Before any production exposure**

1. **R3-01** — close the `node` disclosure path. Nothing else on this list matters if participant email addresses are anonymously readable.
2. **R3-11 (authorization half)** — add the field-authorization snapshot test, so the fix for R3-01 cannot silently regress.
3. **R3-03** — fix forwarded-header handling and correct the misleading comment.
4. **R3-10** — one-line `Url.IsLocalUrl` guard.

**Before real participant data**

5. **R3-02** — restore transactional enqueue in the expiration handler.
6. **R3-05** — add `RefTestId` to the report payload.
7. **R3-09** — bring the in-app notice to parity with `PRIVACY.md`.
8. **R3-04** — guard the auto-submit loop.

**Next iteration**

9. **R3-06**, **R3-07** — unify the deadline definitions and fix the action selection.
10. **R3-08** — shared accessible dialog wrapper.
11. **R3-11 (audit half)**, **R3-12**, **R3-13**, **R3-14**.

**Controller action, not engineering** — confirm data-processing agreements, hosting locations and transfer safeguards for Auth0, Brevo and IHF, as `PRIVACY.md` already flags.

---

## Outstanding handover item

Unrelated to the findings above, and unchanged since R2: the release workflows still require a maintainer to create the GitHub App and populate `RELEASE_APP_CLIENT_ID` and `RELEASE_APP_PRIVATE_KEY`. **`GH_PAT` must not be revoked until that is done.** This is a handover task, not a defect.

---

## Method and limitations

Six parallel investigation streams covered work-package verification, security, GDPR, backend, frontend and test quality. Every candidate they produced was then re-verified by hand against the source at `555c149` before being admitted here; roughly a quarter did not survive that step and appear in the rejected table above.

**Limitations worth stating plainly:**

- This is a static review. No code was executed against a running instance, so R3-01's exploitability is argued from the schema configuration and resolver wiring rather than demonstrated against a live endpoint. A single anonymous `node` query against a deployed environment would settle it definitively, and I would recommend running one.
- There is no integration-test layer in this repository, so end-to-end claims about request-pipeline behaviour (authorization middleware ordering, forwarded headers under a real proxy) rest on configuration reading.
- Third-party behaviour — Auth0 token issuance, Brevo delivery, the IHF Rules Questions API — was not exercised.
- The three-provider EF migration parity was spot-checked, not exhaustively diffed.
