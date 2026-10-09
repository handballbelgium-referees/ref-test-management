# Remediation tracker: {audit round / approved outcome}

<!-- A tracker is not a second plan. Link the canonical plan and its WP definitions. Audit/report approval never authorizes remediation. Publishing or updating this tracker must be within an approved documentation scope. -->

## Source and baseline

- Audit report / round: {path or link}
- Finding IDs: {IDs in scope}
- Current HEAD / branch / working-tree changes: {verified baseline}
- Existing remediation rounds / highest WP ID: {discovered values; avoid collisions}
- Approved canonical plan and version: {native artifact or authorized repository path}

## Separate remediation approval

- Status: {pending / approved}
- Approver and role: {trusted user/maintainer}
- Exact quote and native message/comment reference: {verifiable evidence}
- Approved scope and limitations: {WP IDs; reference their exact file scopes}
- Authorized side effects: {none by default; record explicit separate target/action authority}

## Finding-to-work-package map

| Finding ID | WP ID / canonical definition | Dependencies | Owner | Status | Verification evidence | Remaining risk |
|---|---|---|---|---|---|---|
| {R?-NN} | {WP-ID and link} | {none / IDs} | {role/person} | Pending | {not run} | {risk} |

Use **Pending**, **In progress**, **Blocked**, **Verified**, or **Deferred**. Mark Verified only after the WP's acceptance criteria and applicable checks pass. A deferred item needs a reason and decision reference; it is not fixed. Preserve historical rows and link revalidated findings rather than silently closing them.

## Validation and review

| WP | Command / acceptance check | Result (pass/fail/not run) | Evidence / limitation |
|---|---|---|---|
| {WP-ID} | {exact check} | {result} | {details} |

- Review mode: {Separate read-only reviewer / Self-review (not independent)}
- Review reference and outstanding findings: {evidence}

## Blockers, scope changes, and next action

{Record blocked checks, remaining WPs, dependencies, and any required revised approval. Do not expand the approved plan through tracker edits.}
