---
applyTo: ".github/workflows/**"
---

# Workflow conventions

- Pin every third-party action to a full commit SHA and retain a `# vX` version comment.
- Use the least-privilege `permissions` block and set `persist-credentials: false` on checkout unless a reviewed step specifically needs credentials.
- Never interpolate untrusted event values directly into shell source. Pass them through `env:` and quote them when used.
- Release and deploy only artifacts built from the intended tagged commit, after required build and test gates pass. Preserve the provenance and gating guarantees in the R7 audit remediations.
- Keep workflow changes scoped, and validate their YAML and trigger paths before completion.
