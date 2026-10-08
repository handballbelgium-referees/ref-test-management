# Audit Remediation Plan — R7

> **Status at a glance —** This file tracks only Round 7 follow-up work packages.
> It is intentionally scoped to findings from [`AUDIT-R7.md`](./AUDIT-R7.md), starting at
> **WP-65** (continuing after WP-64 in the main tracker).

Companion to [`AUDIT-R7.md`](./AUDIT-R7.md). That report explains *what* was found and *why*;
this file breaks the R7 wave into reviewable implementation packages.

---

## R7 remediation wave (2026-09-22)

All work packages in this wave are now implemented in code and tracked as complete.

### WP-65 — Add release-pipeline test gates before deploy

**Findings:** R7-01 (🟠 High)  
**Status:** ✅ Implemented  
**Size:** S  
**Priority:** P1  
**Dependencies:** None

**Files**
- `.github/workflows/beta-release.yml`
- `.github/workflows/stable-release.yml`

**Change**
- Add explicit backend/frontend test steps to release flows before build/deploy.
- Make build/deploy jobs depend on a successful test gate.

**Acceptance**
- A release run cannot reach deploy unless backend tests and frontend tests pass.

**Watch out for**
- Keep runtime reasonable by reusing existing restore/install outputs where possible.

---

### WP-66 — Build beta artifacts from the immutable release tag

**Findings:** R7-02 (🟠 High)  
**Status:** ✅ Implemented  
**Size:** S  
**Priority:** P1  
**Dependencies:** WP-65

**Files**
- `.github/workflows/beta-release.yml`

**Change**
- Replace `ref: main` in the beta build checkout with the release output tag.
- Keep version derivation consistent with that immutable ref.

**Acceptance**
- Beta artifact commit SHA is exactly the tag commit SHA for the same run.

**Watch out for**
- Normalize `v`-prefix handling to avoid mismatches in downstream variables.

---

### WP-67 — Reduce token exposure by defaulting checkout credentials to off

**Findings:** R7-03 (🟡 Medium)  
**Status:** ✅ Implemented  
**Size:** S  
**Priority:** P2  
**Dependencies:** None

**Files**
- `.github/workflows/pr.yml`
- `.github/workflows/beta-release.yml`
- `.github/workflows/stable-release.yml`

**Change**
- Set `persist-credentials: false` for checkout steps that do not push.
- Restrict write-token usage to the minimal push/promotion steps.

**Acceptance**
- Workflows still pass; non-push jobs no longer keep git credentials in local checkout config.

**Watch out for**
- Do not break steps that intentionally run `git push`.

---

### WP-68 — Emit CSP/HSTS from server and align security-header documentation

**Findings:** R7-04 (🟡 Medium)  
**Status:** ✅ Implemented  
**Size:** M  
**Priority:** P2  
**Dependencies:** None

**Files**
- `RefTestManagement.Api/Program.cs`
- `RefTestManagement.Ui/src/index.html`

**Change**
- Add HTTP CSP and HSTS response headers in API middleware/pipeline.
- Keep client meta tags as fallback only if desired, and align comments with actual enforcement path.

**Acceptance**
- Response headers include CSP/HSTS in non-development environments.
- Documentation/comments no longer overstate protections.

**Watch out for**
- Validate CSP compatibility with existing inline/script/style needs before enforcing stricter policy.

---

### WP-69 — Fix Auth0 management token caching lifetime design

**Findings:** R7-05 (🟡 Medium)  
**Status:** ✅ Implemented  
**Size:** M  
**Priority:** P2  
**Dependencies:** None

**Files**
- `RefTestManagement.Auth0/Services/Auth0ManagementService.cs`
- `RefTestManagement.Auth0/Auth0ServiceExtensions.cs`

**Change**
- Move token cache to shared scope (`IMemoryCache` or equivalent singleton collaborator).
- Ensure service lifetime and cache semantics match intended behavior.

**Acceptance**
- Repeated management API calls reuse valid token instead of acquiring a new token each call.

**Watch out for**
- Handle token expiry races safely (refresh margin, thread-safety).

---

### WP-70 — Remove startup-path blocking from permission sync (or document it clearly)

**Findings:** R7-06 (🟡 Medium)  
**Status:** ✅ Implemented  
**Size:** S  
**Priority:** P3  
**Dependencies:** None

**Files**
- `RefTestManagement.Api/BackgroundServices/PermissionSyncService.cs`
- `RefTestManagement.Api/Program.cs`
- `README.md`

**Change**
- Prefer delayed/background sync that does not block host startup.
- If behavior stays blocking, update README to reflect reality.

**Acceptance**
- Startup is not delayed by outbound Auth0 sync, or docs explicitly describe the blocking behavior.

**Watch out for**
- Preserve failure-tolerant semantics (sync errors must not take service down).

---

### WP-71 — Enforce safe forwarded-header/rate-limiter defaults

**Findings:** R7-07 (⚪ Low)  
**Status:** ✅ Implemented  
**Size:** S  
**Priority:** P3  
**Dependencies:** None

**Files**
- `RefTestManagement.Api/appsettings.json`
- `RefTestManagement.Api/Program.cs`

**Change**
- Require explicit trusted proxy/network configuration when app-level rate limiting is enabled.
- Optionally fail fast instead of warning-only on unsafe default combos.

**Acceptance**
- Production-like configurations cannot silently run with shared limiter buckets by default.

**Watch out for**
- Keep local developer experience reasonable (explicit dev-safe override).

---

### WP-72 — Restore README parity (OnPush/lint/docs links)

**Findings:** R7-08 (⚪ Low)  
**Status:** ✅ Implemented  
**Size:** S  
**Priority:** P3  
**Dependencies:** None

**Files**
- `README.md`

**Change**
- Correct or implement claims about OnPush usage, lint in PR checks, and audit-document index coverage.

**Acceptance**
- README claims are verifiably true against current repository state.

**Watch out for**
- If implementing lint instead of changing docs, keep ruleset noise manageable for existing codebase.

---

## Phase view

| Phase | Focus | Packages |
|---|---|---|
| 1 | Release integrity | WP-65 → WP-67 |
| 2 | Backend hardening | WP-68 → WP-70 |
| 3 | Config & documentation parity | WP-71 → WP-72 |

## Suggested execution order

1. WP-65, WP-66 (highest risk, release integrity)
2. WP-67 (credential exposure reduction)
3. WP-68, WP-69, WP-70 (backend hardening/resilience)
4. WP-71, WP-72 (default safety and docs parity)
