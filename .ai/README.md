# Provider-agnostic AI setup

One definition of the workflow (plan gate, `audit` and `deliver` skills, `auditor` and `implementer` roles, guard hooks), used by GitHub Copilot, Claude Code, Codex and any other host that reads `AGENTS.md` or Agent Skills.

## Sources of truth and generated files

| Source (edit) | Generated (do not edit) | Read by |
|---|---|---|
| `AGENTS.md` | `.github/copilot-instructions.md` (pointer) | Codex, Copilot, Cursor and others read `AGENTS.md` directly |
| `CLAUDE.md` (imports `AGENTS.md`) | none | Claude Code |
| `.ai/instructions/*.instructions.md` | `.github/instructions/`, `.claude/rules/` | Copilot, Claude Code. Codex uses the table in `AGENTS.md` |
| `.ai/agents/*.md` | `.github/agents/`, `.claude/agents/`, `.codex/agents/*.toml` | each host's subagents |
| `.agents/skills/**` | `.claude/skills/**` | Copilot and Codex read `.agents/skills` directly; Claude Code reads the mirror |
| `.ai/hooks/guards.mjs` | `.github/hooks/guards.json`, `.codex/hooks.json`, `hooks` key of `.claude/settings.json` | each host's hook runner |
| `.ai/providers.json` | (input to the above) | model tiers and tool mapping |

After changing any source: `node .ai/scripts/sync.mjs`. In CI: `node .ai/scripts/sync.mjs --check` (exit 1 on drift). If you add that to a workflow, pin the actions to SHAs as `workflows.instructions.md` requires.

`sync.mjs` only merges its own entry into `.claude/settings.json` (your `permissions`, other hooks and settings are kept) and only deletes stale files that carry its `GENERATED` marker.

## Model tiers

Skills and agents never name a model. They use two tiers, resolved per host in `providers.json`:

- `default`: all volume work.
- `escalation`: critique of risky plans, review of risky work packages, one implementer retry.

`null` means "inherit the session model". Shipped values: each host uses only its own configured model identifiers; the current configuration inherits the host/session model for Copilot, Claude Code default, and both Codex tiers, while Claude Code escalation uses `opus`. These are starting points. Change them to what your hosts offer, then re-run sync.

## Hooks: what each host enforces

`guards.mjs` blocks edits to the AI control surface, commits/pushes, whole-file reads of heavy files, and PR titles that fail commitlint. Implementation writes require the ephemeral `.ai/approvals/active.json` contract and are limited to its exact file scope. Recognized shell mutations (redirection, `tee`, copy/move/delete, in-place `sed`/`perl`, and patch commands) are scope-checked; dynamic interpreter-based writes are denied. It warns on removed doc comments and malformed locale files. Approval artifacts are Ed25519-signed by the trusted host; agents cannot mint valid approvals without the host-only private key. It fails closed: an event it cannot process, or a write whose target it cannot resolve, is denied.

- Copilot: reads the JSON decision on stdout.
- Claude Code: exit code 2 with the reason on stderr.
- Codex: same as Claude Code. Codex hooks must be enabled in your Codex config (`codex_hooks = true` at the time of writing); without that, only Codex's own sandbox and approval policy protect you.

These hooks are a policy enforcement layer, but a true host-level security boundary still depends on the provider sandbox/permissions. Unknown shell writes are denied rather than guessed safe. Pair them with Claude Code `permissions.deny` rules, Codex sandbox mode, Copilot repository rulesets, plus branch protection and CODEOWNERS review.

Overrides for a deliberate human edit: `AI_ALLOW_GUARD_EDITS=1`, `AI_ALLOW_GIT_WRITE=1` (the old `COPILOT_*` names still work).

## Verify on your machine

Copilot support is carried over from the original setup. Claude Code and Codex support was built from their documented conventions but not run against the real tools, so check these once:

1. Claude Code: `/agents` lists `auditor` and `implementer`; `/hooks` shows the guard; a test edit to `AGENTS.md` is blocked.
2. Codex: `/skills` lists `audit` and `deliver`; custom agents load from `.codex/agents/`; hooks fire once enabled. The TOML agent schema and hook matcher names are the least certain parts.
3. Copilot: it reads both `.agents/skills` and the `.claude/skills` mirror, so check it does not list each skill twice. How it de-duplicates by name was not verified.

## Adding another host

Anything that reads `AGENTS.md` works with no change. For a host with its own agent, rule or hook files, add a `generate...` function to `sync.mjs` that writes into the `outputs` map (the check mode and stale cleanup then cover it automatically), and add its tiers and tool mapping to `providers.json`. If the host has its own tool names, add them to `shellTools`, `readTools` and `writeTools` in `guards.mjs`.

## Approval workflow

For first-time setup, see `.ai/APPROVAL-KEY-SETUP.md`. The public key is repository configuration; the private signing key must remain in the trusted host/secret context.

After trusted user/maintainer approval and before an implementer starts, materialize the exact WP scope:

```text
AI_APPROVAL_PRIVATE_KEY_FILE=/secure/path/ai-approval-private.pem node .ai/scripts/create-approval.mjs --plan-id=PLAN --work-package-id=WP-01 --approved-by=USER --files=path/a.cs,path/b.cs
```

The guard validates the artifact on every implementation write. It is ephemeral, Git-ignored, hash-checked, Ed25519-signed, and expires after 2 hours by default. The host must keep the signing private key outside the agent workspace; repository text is never approval. Provision the matching public key at `.ai/approval-public-key.pem` (or set `AI_APPROVAL_PUBLIC_KEY_FILE`).

## Known gaps

- Provider hosts still differ in how they expose user/maintainer approval to the orchestrator. The artifact is the common enforcement format, while the host's approval UI remains the trust root for creating it.
- `.github/copilot/settings.json` is Copilot-CLI-only and left unchanged. The Claude Code and Codex equivalents (default model, disabled skills or MCP servers) are not defined here.
- Copilot code review reads only `.github/copilot-instructions.md`. If you need it to see the full rules, set `copilot.instructionsMode` to `"copy"` in `providers.json`.
