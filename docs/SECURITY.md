# Security & Permissions

This document describes the task-based permission system used by the RefTest Management Platform.

## Overview

The application uses **Auth0** for authentication and a **task-based permission system** for authorization. Every protected GraphQL operation maps 1:1 to a named permission. Effective permissions are refreshed from the Auth0 Management API and supplied to authorization as `permissions` claims.

The backend enforcement lives in the `RefTestManagement.Security` class library. The Angular frontend uses a `PermissionsService` and a `HasPermission` structural directive for reactive, signal-based UI control.

---

## Auth0 Setup

### 1. Enable RBAC on your API

1. Go to **Auth0 Dashboard → Applications → APIs**
2. Select the API that your application authenticates against
3. Open the **Settings** tab → scroll to **RBAC Settings**
4. Enable **"Enable RBAC"**
5. **"Add Permissions in the Access Token"** is optional for this application: authorization uses current Management API grants, not a possibly stale JWT permission claim. Enable it only if another consumer needs it.
6. Save

### 2. Register permissions on the API

On the same API → **Permissions** tab, add each permission string listed in the [Permission Reference](#permission-reference) below.

### 3. Create roles and assign permissions

1. Go to **User Management → Roles** → create a role (e.g. `Admin`, `Viewer`)
2. On the role → **Permissions** tab → add permissions from your API
3. Go to **User Management → Users** → select a user → **Roles** tab → assign the role

### 4. Verify the token

Ensure the access token is a signed JWT for the API **audience** configured by the application. The application does not use a JWT `permissions` array as its authorization source; it reads the user's current effective grants from the Management API.

---

## Permission Reference

### Special Permissions

| Permission   | Description                                                                    |
| ------------ | ------------------------------------------------------------------------------ |
| `superadmin` | Bypasses all permission checks — grants unrestricted access to every operation |

### RefTests

| Permission                        | Protects                                          | Type     |
| --------------------------------- | ------------------------------------------------- | -------- |
| `ref-tests:create`                | `createRefTests` mutation                         | Mutation |
| `ref-tests:delete`                | `deleteRefTests` mutation                         | Mutation |
| `ref-tests:update-details`        | `updateRefTestDetails` mutation                   | Mutation |
| `ref-tests:update-configuration`  | `updateRefTestConfiguration` mutation             | Mutation |
| `ref-tests:update-notifications`  | `updateRefTestNotificationSettings` mutation      | Mutation |
| `ref-tests:extend-time`           | `extendRefTestTime` mutation                      | Mutation |
| `ref-tests:regenerate-token`      | `regenerateRefTestToken` mutation                 | Mutation |
| `ref-tests:reset`                 | `resetRefTests` mutation                          | Mutation |
| `ref-tests:revive`                | `reviveRefTests` mutation                         | Mutation |
| `ref-tests:approve`               | `approveRefTests` / `rejectRefTests` mutations    | Mutation |
| `ref-tests:send-invitations`      | `sendRefTestInvitations` mutation                 | Mutation |
| `ref-tests:send-results`          | `sendRefTestResults` mutation                     | Mutation |
| `ref-tests:send-report`           | `sendRefTestReport` mutation                      | Mutation |
| `ref-tests:view-list`             | `refTests` query + `refTestsUpdated` subscription | Query    |
| `ref-tests:view-detail`           | `refTest` query + `refTestUpdated` subscription   | Query    |
| `ref-tests:view-detail-questions` | `questions` field on `RefTest` (detail page)      | Query    |
| `ref-tests:view-titles`           | `refTestTitles` query                             | Query    |
| `ref-tests:*`                     | Wildcard — grants all `ref-tests:*` permissions   | Wildcard |

The `RefTest.questions` field requires `ref-tests:view-detail-questions`; `ref-tests:view-detail`
alone does not expose question content or its nested identifiers. The shared `Question.id` field
also accepts the existing `questions:search` and `questions:view` permissions for their direct
question queries. `wrongQuestionIds`, `wrongAnswerIds`, and `selectedAnswerIds` remain available
to `ref-tests:view-detail` users as limited result metadata.

### Questions

| Permission         | Protects                                        | Type     |
| ------------------ | ----------------------------------------------- | -------- |
| `questions:search` | `searchQuestionsByNumber` query                 | Query    |
| `questions:view`   | `getQuestionsByNumber` query                    | Query    |
| `questions:*`      | Wildcard — grants all `questions:*` permissions | Wildcard |

### Audit Logs

| Permission        | Protects                                    | Type     |
| ----------------- | ------------------------------------------- | -------- |
| `audit-logs:view` | `auditLogs` query (paginated, with filters) | Query    |
| `audit-logs:*`    | Wildcard — grants all `audit-logs:*` perms  | Wildcard |

## Public Personal-Data Export Verification

The `requestPersonalDataExport` and `confirmPersonalDataExport` mutations are intentionally public,
like the existing participant lifecycle mutations; they have no `[Authorize]` policy and require no
account or Auth0 permission. The confirmation key proves control of the mailbox before an export is
queued.

The request mutation returns the same acknowledgement for matching and nonmatching addresses.
Only an address already associated with a non-anonymized participant record receives a localized
verification email. The request and confirmation operations have separate per-client-address rate
limits; their permit counts, window, and cleanup interval are configured under
`PrivacyChallengeConfiguration`. Export and withdrawal verification keys expire after 24 hours by
default; deployments may set `PrivacyChallengeKeyLifetimeHours` from 1 through 168.

The email link is `/privacy/export-confirmation?lang=<locale>#<key>`. The initial page load must
only display the confirmation page: the client reads the key from the fragment, removes it from
the address bar/history, and sends it to `confirmPersonalDataExport` only after the participant
explicitly clicks Confirm. The server stores a hash for one-time atomic consumption and keeps only
a data-protected transient copy while retrying email delivery. The job carries the request ID only;
the raw key is not persisted in job or audit data.

## Public Consent-Withdrawal Verification

The `requestPrivacyWithdrawal` and `confirmPrivacyWithdrawal` fields are public GraphQL mutations.
They intentionally have no `[Authorize]` policy or permission constant: participants do not need
an account or Auth0 permission, and control of the matching mailbox is the verification method.

`requestPrivacyWithdrawal` returns a payload containing only
`privacyWithdrawalRequestAcknowledgement.acknowledged: true`, including for unmatched, duplicate,
invalid, or rate-limited email requests. Its generated input wrapper contains a nested `input`
object with the email. Only a matching, non-anonymized participant address is queued for a
one-time challenge email; the response does not disclose a match or any RefTest data. The
confirmation input wrapper carries the one-time key.
`confirmPrivacyWithdrawal` returns a payload containing only
`privacyWithdrawalConfirmationResult.accepted`. It is true only after the withdrawal service
validates and consumes the unexpired one-time key and commits the confirmation and any required
durable processing work; invalid, expired, replayed, or rate-limited attempts return false.
Acceptance means queued, not that anonymization has completed. The public acknowledgements contain
only their generic `acknowledged` and `accepted` values. Withdrawal service logs omit participant
email addresses, raw keys, and RefTest identifiers; they use fixed failure messages and aggregate
retry counts. Request/challenge and batch audit events record lifecycle details and aggregate
counts, not participant addresses, raw keys, or individual RefTest identifiers. Challenge-email
jobs carry only a challenge ID, and batch jobs only a batch ID; RefTest IDs remain in durable target
rows, not job payloads. Separately, each erasure records a `RefTestAnonymizedEvent` on that
RefTest's own audit stream, keyed by its RefTest ID, as per-record erasure evidence.

Request and confirmation attempts use separate per-client-address fixed-window rate limits, shared
with the corresponding public export operations. Defaults are five requests and ten confirmation
attempts per 60-second window. Configure the window and permit counts under
`PrivacyChallengeConfiguration` (`RateLimitWindowSeconds`, `RequestRateLimitPermitLimit`, and
`ConfirmationRateLimitPermitLimit`).

Confirmation is available only as a GraphQL mutation, not a query. A GET request cannot consume a
key. The key is consumed once by the server and is never returned by either mutation.

---

## Wildcard & Superadmin Resolution

The `TaskPermissionHandler` resolves permissions in the following order:

1. **Superadmin**: if the user holds the `superadmin` permission, all checks succeed immediately
2. **Exact match**: `ref-tests:create` matches only `ref-tests:create`
3. **Namespace wildcard**: `ref-tests:*` matches any `ref-tests:…` permission

For OR-semantics (a field accessible with any one of several permissions), the `AnyTaskPermissionHandler` applies the same resolution logic against a set of permissions and succeeds on the first match.

---

## Suggested Roles

| Role       | Permissions                                                                                                                                                        |
| ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Admin`    | `ref-tests:*`, `questions:*`, `audit-logs:*`                                                                                                                       |
| `Auditor`  | `audit-logs:view`                                                                                                                                                  |
| `Approver` | `ref-tests:view-list`, `ref-tests:view-detail`, `ref-tests:approve`                                                                                                |
| `Creator`  | `ref-tests:create`, `ref-tests:view-list`, `ref-tests:view-detail`, `ref-tests:view-detail-questions`, `ref-tests:view-titles`                                     |
| `Manager`  | `ref-tests:view-list`, `ref-tests:view-detail`, `ref-tests:view-detail-questions`, `ref-tests:send-invitations`, `ref-tests:send-results`, `ref-tests:send-report` |
| `Viewer`   | `ref-tests:view-list`, `ref-tests:view-detail`                                                                                                                     |

---

## Backend Implementation

The `RefTestManagement.Security` project (no dependencies on other projects) contains:

| Type                              | Purpose                                                          |
| --------------------------------- | ---------------------------------------------------------------- |
| `Permissions`                     | Single source of truth for all permission string constants       |
| `TaskPermissionRequirement`       | `IAuthorizationRequirement` for a single required permission     |
| `TaskPermissionHandler`           | Handles exact match, wildcard, and superadmin bypass             |
| `AnyTaskPermissionRequirement`    | Requirement satisfied by any one of N permissions (OR semantics) |
| `AnyTaskPermissionHandler`        | Handler for OR-permission requirements                           |
| `TaskAuthorizationPolicyProvider` | Resolves `anyof:perm1\|perm2` policy names at runtime            |
| `SecurityServiceExtensions`       | `AddTaskBasedAuthorization()` — registers all of the above       |

Registered in `Program.cs`:

```csharp
services.AddSecurityConfiguration(configuration); // Auth0 OIDC + JWT Bearer
services.AddTaskBasedAuthorization();             // task-based permission policies
```

`SecurityStartup.cs` refreshes the principal's `permissions` claims from the current effective Management API grants during OIDC/JWT validation and cookie validation. Cookie claims are renewed when the grant set changes.

## Permission freshness and failure behavior

The API resolves direct user grants and permissions inherited through assigned roles, filtered to
the configured API audience. Each API process caches a user's successful snapshot for four minutes
and shares one in-flight refresh per user; an internal limit of four concurrent refreshes per
process bounds work without asserting an Auth0 tenant quota.

When a snapshot expires, a Management API 429, outage, or other refresh failure never falls back to
the old grants. Cookie and bearer authentication fail closed if a fresh snapshot is unavailable,
and `/Account/Permissions` returns no grants (or an authentication error) rather than stale claims.
The UI polls this endpoint every minute and clears its permissions on errors. No refresh token is
used or assumed.

Admin subscription resolvers check the current snapshot before mapping each event. Revoked or
unverifiable permissions produce an authorization error without an event payload; the WebSocket
may remain connected, but admin PII is not delivered. The effective-permission freshness bound is
four minutes per API process, below the five-minute requirement.

---

## Frontend Implementation

### `PermissionsService`

Fetches permissions from `/Account/Permissions` when the user is authenticated and refreshes them every minute. Exposes a reactive `permissions` signal (`undefined` while loading) and a `hasPermission(permission)` helper.

```typescript
// Force a re-fetch after role changes (optional)
permissionsService.refresh();
```

### `HasPermission` Directive

Structural directive that conditionally renders elements based on a permission:

```html
<button *hasPermission="Permissions.RefTests.Create">Create</button>
```

### Route Guards

Protected routes use `permissionGuard` after `authGuard`. The guard waits for permissions to load before evaluating:

```typescript
{
  path: 'ref-tests',
  canActivate: [authGuard, permissionGuard(Permissions.RefTests.ViewList)],
}
```

| Route               | Required Permission     |
| ------------------- | ----------------------- |
| `/ref-tests`        | `ref-tests:view-list`   |
| `/ref-tests/create` | `ref-tests:create`      |
| `/ref-tests/:id`    | `ref-tests:view-detail` |

## The Participant Invitation Token

A participant opens their test through a link containing an invitation token. The token is the
initial credential: there is no sign-in on that flow, because participants are not Auth0 users.

Participant token queries and lifecycle mutations use the dedicated `ParticipantRefTest` GraphQL
contract rather than the administrator `RefTest` type. This keeps administrator authorization rules
and fields isolated from the anonymous participant flow. Answer correctness is omitted while a test
is in progress and requested only after the RefTest is completed; participant answers are randomized
deterministically per RefTest so the order remains stable across refreshes and result review.

The public `createRefTestSession` mutation exchanges an invitation or existing session credential
for a fresh, data-protected session credential after the participant has accepted the privacy
notice. It intentionally has no `[Authorize]` policy or permission constant; possession of the
participant credential is the authorization method. Test operations then use the session credential.
Pending session credentials have a 12-hour window to start the test. When a test starts within that
window, the credential remains valid until one hour after the current test deadline; this deadline
is rechecked against the RefTest on participant requests, including after an administrative time
extension. Credentials issued while a test is in progress are valid for at least 12 hours or until
one hour after the deadline, whichever is later. All session credentials are bound to the RefTest ID
and its current invitation-token digest, so token rotation and anonymization invalidate them.
Session-lock subscriptions revalidate credentials every 30 seconds and release their lock after
expiry or invalidation.

Putting a credential in a URL is a deliberate trade-off, made because requiring an account for a
one-off test would keep most participants from ever taking it. The risks that choice carries are
mitigated rather than ignored:

After acceptance or resuming, the UI exchanges the invitation token and replaces the URL with
`/ref-test/take`. Only the expiring session credential is kept in browser history state for reload
and resume. New invitation emails use `/ref-test?lang=<locale>#<invitation-token>`. The invitation
fragment is moved into history state while the UI redirects to the tokenless welcome route; the
former `/ref-test/:token` path format is no longer routed.

| Risk                                       | Mitigation                                                                                                     |
| ------------------------------------------ | -------------------------------------------------------------------------------------------------------------- |
| Token leaks through the `Referer` header   | New invitation tokens are in the URL fragment, which browsers do not send in HTTP requests or `Referer` headers; the API also sends `Referrer-Policy: no-referrer`. |
| Token guessed                              | Tokens are 32 lowercase hex characters from `RandomNumberGenerator`, not `Guid.NewGuid()` — 128 bits of cryptographic randomness. |
| Token exposed from database rows           | `RefTests.Token` stores a SHA-256 digest; a data-protected retry copy is cleared after delivery, token rotation, or anonymization, and queued invitation jobs contain only the digest. |
| Session token replayed after rotation or expiry | The protected session token is bound to the current invitation digest and validated against the active test deadline; participant lookups and live session locks revalidate it. |
| Token replayed after the test is over      | Every participant mutation re-checks status, and the server enforces the deadline independently of status.       |
| Token reused after a problem               | Operators can regenerate a token, which invalidates the previous link.                                           |
| Oversized or malformed token in a lookup   | Only 32 lowercase hex characters are accepted before hashing; the stored digest cannot be submitted as a credential. |

Residual risk that is accepted: historical server access logs may retain tokens from any previously
issued path-based invitation links, although that route is no longer supported. New invitation
links use a fragment, but after the UI reads it the invitation token is temporarily kept in browser
history state until the session exchange; same-origin scripts can read it. The session credential
remains in browser history state on tokenless routes, and anyone who can read a still-valid
credential can resume that one participant's test. Credential expiration and binding limit this
exposure, while the original invitation credential remains usable until an operator rotates it or
the record is anonymized.

## Response Security Headers

The API emits these headers on responses, including the SPA; HSTS is sent only for HTTPS outside
Development:

| Header                       | Value                                                  | Why                                                                     |
| ---------------------------- | ------------------------------------------------------ | ----------------------------------------------------------------------- |
| `Content-Security-Policy`    | See directives below                                   | Allows same-origin external scripts and blocks inline scripts.          |
| `Referrer-Policy`            | `no-referrer`                                           | Prevents this page's URL from being sent as a referrer.                 |
| `Strict-Transport-Security` | `max-age=2592000` (30 days; non-Development HTTPS only) | Requires HTTPS on later visits after a successful HTTPS response.       |
| `X-Content-Type-Options`    | `nosniff`                                               | Stops content-type sniffing.                                             |
| `X-Frame-Options`           | `DENY`                                                  | Blocks framing, so the test cannot be clickjacked.                       |

The API's Content-Security-Policy response header has these directives (shown one per line):

```text
default-src 'self' https:;
script-src 'self';
worker-src 'self' blob:;
style-src 'self' 'unsafe-inline';
connect-src 'self' wss:;
img-src 'self' data: https:;
font-src 'self' data:;
base-uri 'self';
form-action 'self';
```

Production builds disable Angular's `inlineCritical` optimization because it emits inline CSS and
a media-switch script; the stylesheet remains a same-origin external asset. `style-src` continues
to allow inline styles, independently of the stricter script policy. HSTS is added by ASP.NET
Core's HSTS middleware for HTTPS responses outside Development.

`index.html` has best-effort meta fallbacks for `Referrer-Policy` and `X-Content-Type-Options`,
but CSP is intentionally response-header-only to avoid duplicate policies. Browsers ignore
`X-Frame-Options` and `X-Content-Type-Options` in markup, and a meta `Referrer-Policy` applies
only from the point the parser reaches it. The response headers are authoritative.
