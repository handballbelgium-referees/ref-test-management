# Audit Remediation Plan — R11

> **Status at a glance —** R11-03 through R11-05 are assigned to WP-111 through WP-113.

Companion to [`AUDIT-R11.md`](Audits/AUDIT-R11.md). The audit records evidence and impact; this
tracker records the approved repository remediation packages and their verification status.

---

## R11 remediation wave

**Approval:** Approved through the repository plan gate.  
**Scope:** Repository-actionable findings R11-03 through R11-05 only. R11-01 and R11-02 remain
excluded pending live operational evidence and controller/legal determinations.  
**Execution:** Execute one WP at a time and mark it implemented only after its focused checks pass.

### WP-111 — Fail CI on vulnerable NuGet dependencies

**Finding:** R11-03 (🟡 Medium)  
**Status:** ✅ Implemented

**Size:** M  
**Priority:** P1  
**Dependencies:** None

**Files**
- `.github/workflows/pr.yml`
- `.github/workflows/beta-release.yml`
- `.github/workflows/stable-release.yml`

**Change**
- Run locked restore with `NuGetAuditMode=all` and `NuGetAuditLevel=low`, promoting NU1901–NU1904 vulnerability warnings to errors in every in-scope PR/release validation or build restore.

**Acceptance**
- PR validation and beta/stable release validation and build jobs audit direct and transitive dependencies at every severity; existing downstream release gates and locked restores remain intact.

**Tests**
- Run the locked solution restore with the same NuGet audit properties; inspect every in-scope workflow restore and its job dependencies.
- `node --test scripts/release-safety.test.mjs` — passed (9 tests; includes YAML parsing and release-gate invariants). Actionlint was unavailable locally.

**Watch out for**
- Keep vulnerability warning promotion limited to NU1901–NU1904; preserve existing workflow permissions, action pins, and release provenance gates. CodeQL and Copilot setup restores remain outside this finding's scope.

### WP-112 — Expose export email required state

**Finding:** R11-04 (⚪ Low)  
**Status:** ✅ Implemented

**Size:** S  
**Priority:** P2  
**Dependencies:** None

**Files**
- `RefTestManagement.Ui/src/app/privacy/components/personal-data-export/request/personal-data-export-request.html`
- `RefTestManagement.Ui/src/app/privacy/components/personal-data-export/request/personal-data-export-request.spec.ts`

**Change**
- Expose the required email state with `aria-required="true"` and assert it on the rendered input.

**Acceptance**
- Assistive technology can identify the email field as required, with existing signal-form validation and messaging unchanged.

**Tests**
- `npm test -- --include src/app/privacy/components/personal-data-export/request/personal-data-export-request.spec.ts --watch=false` — passed (5 tests).

**Watch out for**
- Do not add native browser-required validation alongside the custom form validation.

### WP-113 — Correct privacy export form location

**Finding:** R11-05 (⚪ Low)  
**Status:** ✅ Implemented

**Size:** S  
**Priority:** P2  
**Dependencies:** None

**Files**
- `docs/PRIVACY.md`

**Change**
- State that the `/privacy` notice links to the export request form at `/privacy/export-request`.

**Acceptance**
- The documented export form path matches the Angular route and the privacy notice link; the withdrawal route remains correctly described.

**Tests**
- Searched directly related docs for stale form-location wording and verified the export and withdrawal routes against source.

**Watch out for**
- Preserve `/privacy` references that identify the notice itself.

## Execution order

1. WP-111, WP-112, and WP-113 are independently reviewable; execute one package at a time.
2. Mark a WP implemented only after its acceptance criteria and focused checks pass.

## Explicitly out of scope

- R11-01 operational privacy evidence and production Azure inspection.
- R11-02 provider/controller legal-basis decisions or edits to provisional privacy records.
- CodeQL and Copilot setup workflow restores, unrelated dependency changes, commits, pushes, and pull requests.
