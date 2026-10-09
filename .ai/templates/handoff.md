# Handoff: {plan / WP / outcome}

## Request and destination

- Source / destination host or role: {values; inline is valid}
- Requested next action: {one bounded task}
- Applicable contract / scope instructions: {AGENTS.md and matching paths}
- Host capabilities and limits: {native approval, skills, separate reviewer, shell, cloud pause; do not assume support}
- Execution provider / agent role: {claude | codex | github-copilot; implementer | auditor | reviewer, or inline}
- Requested model / selection source: {explicit user choice | agent override | provider default | host inheritance; model ID or null}
- Effective model / evidence: {confirmed model ID and host evidence, or unknown/inherited; disclose unsupported or blocked selections}

## Verified baseline

- Repository / branch / HEAD: {values}
- Working-tree changes before work: {paths, ownership, and preserved user edits}
- Current changed/untracked files: {exact list}
- Relevant dependency state: {completed prerequisite WPs and checks}

## Approved plan and work package

- Plan identity, version, and location: {native artifact or authorized path}
- Approver / authority: {trusted user or repository maintainer role}
- Exact approval quote: {verbatim}
- Native message/comment reference: {where the receiver can verify it}
- Approval limitations: {what is and is not covered}

### Verbatim approved WP

{Paste the complete approved WP here, including source, size, priority, dependencies, exact file scope, change, acceptance, verification, and risks. Do not replace it with a summary.}

## Side-effect authority

{None by default. Record explicit authorization separately for staging, commits, pushes, branch mutations, issue/PR publication or updates, merge, deployment, and any automatic hosted side effects, with exact target/branch and limits. Assignment alone is insufficient.}

## Completed and remaining work

- Completed: {verified acceptance criteria / WPs}
- Changed files: {paths and concise purpose}
- Remaining: {bounded next steps and dependencies}
- Out of scope / preserved edits: {paths and constraints}

## Validation and review

| Exact command / acceptance check | Result (pass/fail/not run) | Evidence / limitation |
|---|---|---|
| {check} | {result} | {details} |

- Review mode: {Separate read-only reviewer / Self-review (not independent)}
- Review verdict, evidence, and unresolved findings: {reference/details}

## Blockers and safe next action

{Include unavailable tools/checks, changed assumptions, or required revised approval. Do not carry secrets, tokens, or participant data into the handoff.}

The receiver must verify the original approval record and current baseline before writing. A copied handoff is not a trusted approval token. Resume the same verified approved WP without asking for duplicate approval; stop for renewed approval if authority is unverifiable, scope has grown, or user changes invalidate it. Unsupported cloud pause/side-effect requirements must be resolved before dispatch, not worked around.
