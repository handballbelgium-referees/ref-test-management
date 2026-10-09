# Review: {plan / WP / change}

## Context and evidence

- Approved plan/version and WP: {reference}
- Approval reference: {trusted native record}
- Baseline and reviewed HEAD / branch / working-tree state: {values}
- Files / diff reviewed: {exact scope, including relevant untracked files}
- Review mode: {Separate read-only reviewer / Self-review (not independent)}
- Reviewer and limitations: {identity/role; self-review lacks independent context}

## Acceptance and invariant checks

| Criterion | Evidence | Result (pass/fail/not run) |
|---|---|---|
| {WP acceptance criterion} | {current path:line / check} | {result} |
| {Applicable repository invariant} | {evidence or N/A reason} | {result} |

## Findings

{Use one block per actionable finding, or state "No findings in the reviewed scope."}

- ID / title: {identifier and summary}
- Classification / severity / confidence: {defect, risk, documentation gap, or unverified external control; Critical/High/Medium/Low; 1-10 with rationale}
- Evidence: {current path:line and observed fact or safe reproduction}
- Impact: {concrete consequence}
- Minimal recommendation: {bounded correction}
- Resolution / recheck: {open, corrected with evidence, or deferred by explicit decision}

## Verification

| Exact command / check | Run by reviewer or reported by implementer | Result (pass/fail/not run) | Limitations |
|---|---|---|---|
| {check} | {source} | {result} | {reason/evidence} |

## Verdict and next action

{No blocking findings / Changes requested / Inconclusive}

{Residual risks, scope not assessed, and next action. No findings does not guarantee exhaustive correctness. This review does not authorize commits, pushes, PR publication, or merge.}
