# AI implementation approval contract

Implementation agents must have an explicit, machine-readable approval artifact for the
work package they are executing.

The artifact should identify:

- `planId`
- `workPackageId`
- `approvedFiles` (or an equivalent constrained scope)
- `approvalHash`
- `approvedBy`
- `approvedAt`

The implementation guard must deny the operation when the artifact is missing, malformed,
expired, or does not match the requested work package/scope.

Security-sensitive hooks are fail-closed. Unknown write-capable tool schemas are denied
until their write targets can be resolved safely.

This contract supplements repository instructions; it does not replace normal code review.
