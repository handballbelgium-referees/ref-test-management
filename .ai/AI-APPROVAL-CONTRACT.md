# AI implementation approval contract

Implementation agents must have an explicit, machine-readable approval artifact for the
work package they are executing.

The ephemeral artifact is `.ai/approvals/active.json` and must identify:

- `planId`
- `workPackageId`
- `approvedFiles` (an exact list of repository-relative file paths)
- `approvalHash` (SHA-256 of the canonical artifact with `approvalHash` and `approvalSignature` omitted)
- `approvalSignature` (Ed25519 signature over the canonical artifact with `approvalSignature` omitted)
- `approvedBy`
- `approvedAt` (ISO-8601 timestamp)

The implementation guard validates the artifact before every direct write and every
recognized shell file mutation. It denies the operation when the artifact is missing,
malformed, expired, tampered with, or when the requested files are outside the approved
scope. The default validity window is 2 hours and can be shortened with
`AI_APPROVAL_MAX_AGE_MS`.

The artifact is deliberately ephemeral and ignored by Git. Create it only after the
trusted host/user approval gate has completed:

```text
node .ai/scripts/create-approval.mjs \
  --plan-id=PLAN-123 \
  --work-package-id=WP-01 \
  --approved-by=KristofGilis \
  --files=src/a.cs,src/b.cs
```

The command is a host-side signing adapter, not a source of approval. It requires
`AI_APPROVAL_PRIVATE_KEY_FILE`, pointing to an Ed25519 private key that is kept outside the
agent workspace. The guard verifies the corresponding public key from
`.ai/approval-public-key.pem` (or `AI_APPROVAL_PUBLIC_KEY_FILE`) before allowing any write.
Do not expose the private key to an agent, derive approval from an issue/PR body or tool
permission, or let an agent create or replace the verification key. For cloud-agent mode,
the orchestrator must invoke the signer only after the required maintainer approval
comment has been observed through the host's trusted approval mechanism.

Security-sensitive hooks are fail-closed. Unknown write-capable tool schemas are denied
until their write targets can be resolved safely.

This contract supplements repository instructions; it does not replace normal code review.
