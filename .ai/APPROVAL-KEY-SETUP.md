# Host approval signing key setup

The implementation approval artifact is signed with Ed25519. The repository guard contains
only the public verification key; the private signing key must remain in a trusted host,
secret store, or other execution context that the agent cannot read.

## One-time setup

Generate the key pair on the trusted host:

```text
openssl genpkey -algorithm Ed25519 -out ~/.config/ai-workflow/approval-private.pem
openssl pkey -in ~/.config/ai-workflow/approval-private.pem -pubout -out .ai/approval-public-key.pem
chmod 600 ~/.config/ai-workflow/approval-private.pem
```

Commit `.ai/approval-public-key.pem` if this repository should pin one trusted approval
issuer. Never commit the private key.

If your host provides a signing service instead of a local file, adapt the host-side
approval adapter to produce the same `approvalSignature` using the corresponding private
key. The implementation agent must never receive the private key or an equivalent signing
capability.

## Creating an approval

Only the trusted orchestrator should run:

```text
AI_APPROVAL_PRIVATE_KEY_FILE=~/.config/ai-workflow/approval-private.pem \
  node .ai/scripts/create-approval.mjs \
  --plan-id=PLAN-123 \
  --work-package-id=WP-01 \
  --approved-by=USER \
  --files=src/a.cs,src/b.cs
```

The guard verifies the signature against `.ai/approval-public-key.pem` before every write.
Changing the approval JSON, recomputing its SHA-256 hash, or replacing the public key from
inside the agent workspace does not constitute approval.

For a non-default public-key location, set `AI_APPROVAL_PUBLIC_KEY_FILE` for the guard and
host adapter. The default approval lifetime is two hours and can be shortened with
`AI_APPROVAL_MAX_AGE_MS`.
