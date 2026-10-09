# Shared AI workflow

[AGENTS.md](../AGENTS.md) owns the contract: investigate, plan, obtain native approval, implement bounded WPs, validate, review, and hand off. This directory supplies portable scope instructions, role guidance, and supporting templates. It is not a scheduler, sandbox, or permission service.

## Shared sources and integration

| Source | Purpose |
|---|---|
| `AGENTS.md` | Repository-wide lifecycle, approval, invariants, and side-effect boundaries |
| `.ai/instructions/*.instructions.md` | Backend, UI, and workflow conventions |
| `.ai/agents/*.md` | Portable role guidance, usable inline or through a supported host agent |
| `.ai/models.json` | Optional model preferences per execution provider and delegated role |
| `.ai/templates/*.md` | Intake, remediation tracking, reviews, PRs, and handoffs |
| `.agents/skills/deliver/` | Canonical delivery procedure and the only plan/WP template |
| `.agents/skills/audit/` | Canonical audit procedure and the only audit report template |

The shared contract, canonical skills/templates, and 16 generated native adapters are present. The Node regression suite and independent PR consistency job validate the assembled workflow without AI credentials or package installation.

### Fixed native mappings

| Canonical source | Generated adapter |
|---|---|
| `AGENTS.md` | Root `CLAUDE.md` containing native `@AGENTS.md`; `.github/copilot-instructions.md` pointing to the root contract |
| `.ai/instructions/{backend,ui,workflows}.instructions.md` | `.github/instructions/*.instructions.md` retaining `applyTo`; `.claude/rules/{backend,ui,workflows}.md` using `paths` |
| `.agents/skills/{audit,deliver}/**` | `.claude/skills/{audit,deliver}/**`, including all templates and resources |
| `.ai/templates/issue.md` | `.github/ISSUE_TEMPLATE/work-item.md` with GitHub issue-template frontmatter |
| `.ai/templates/pull-request.md` | `.github/pull_request_template.md` |

Copilot and Codex discover `.agents/skills/` directly; there is no duplicate `.github/skills/` tree or `.codex` configuration. Portable role guidance remains under `.ai/agents/`, not provider-specific custom agents. Agent model preferences use the shared config below; context limits, tools, and permissions inherit host/user choices. The generator validates configuration but does not switch models, add hooks, merge host settings, or establish a permission sandbox.

### Agent model configuration

Edit [models.json](models.json) to choose models independently for `claude`, `codex`, and
`github-copilot`. These keys identify the **execution host**, not the model vendor.
Copilot using a Claude model still reads `github-copilot`.

Each provider has a `default` plus `agents.implementer`, `agents.auditor`, and
`agents.reviewer`. Claude defaults to `haiku`; Codex and GitHub Copilot default to
`gpt-6-luna`. All agent overrides use JSON `null`, inheriting their provider default.
Set a provider default to null to inherit the host model, or use an available model ID
or alias accepted by that specific host.
For example, edit the existing `github-copilot` entry as follows, replacing these
illustrative IDs with real IDs from your host's model selector:

```json
{
  "default": "your-default-model-id",
  "agents": {
    "implementer": null,
    "auditor": "your-audit-model-id",
    "reviewer": "your-review-model-id"
  }
}
```

Resolution is **explicit current user choice → agent override → provider default →
host inheritance**. Null for an agent falls through to its provider default, not directly
to the host. If both are null, omit the host's model argument. An explicit user instruction
to inherit the current host model takes precedence over configured strings as well.

Inspect the configured selection without launching an agent:

```text
node .ai/scripts/resolve-model.mjs claude implementer
node .ai/scripts/resolve-model.mjs codex auditor
node .ai/scripts/resolve-model.mjs github-copilot reviewer
```

With the configured defaults, the last command prints exactly:

```json
{"provider":"github-copilot","agent":"reviewer","model":"gpt-6-luna"}
```

The CLI resolves its repository root from its own location. It exits 0 with JSON on stdout
or 2 with a diagnostic on stderr for invalid usage or configuration, never a success-shaped
fallback. The importable `resolveModel(root, provider, agent, { override })` uses the same
validation: omit `override` for configured selection, supply a model ID for explicit user
choice, or null for explicitly requested host inheritance. Config validation always runs.

Delivery and audit resolve each delegated role before dispatch and pass a non-null model
through the host's supported per-agent option. Explicit user choices and higher-priority
host policies take precedence. If the host rejects a selection, cannot select it, or
policy conflicts, disclose the limitation and pause that delegation; never silently
substitute a model. Record requested/effective model or unknown/inherited in the handoff.
If the execution provider is unknown, determine it before selecting a provider entry.

This is not a provider launcher: editing JSON does not switch the running session, create
native custom agents, or configure global settings. Inline work continues on the current
session model. A host without per-agent model control cannot honor a non-null preference
automatically. Claude/Codex/Copilot CLI, IDE, and cloud support varies; the capability table
below records discovery evidence, not proof that a selected model ran.

The resolver validates required provider/role keys, rejects unknown keys, and accepts only
null or nonempty model ID strings containing letters, digits, `.`, `_`, `:`, `/`, and `-`
(starting with a letter or digit). It does not query subscriptions or validate live model
availability. No credentials belong in this file. `sync.mjs`, including `--check`, validates
the whole config before writing any adapter. Config edits alone do not change the 16
generated outputs; workflow instruction edits still require regeneration.

### Generation and ownership

Run from the repository root with Node.js 18 or newer:

```text
node .ai/scripts/sync.mjs
node .ai/scripts/sync.mjs --check
```

The CLI resolves the root from the script location, not the current directory. Tests can import `sync(root, { check: true })` from `.ai/scripts/sync.mjs` using an isolated fixture root. The function returns `{ issues, written, total }`; invalid/missing sources or unsafe paths throw. The complete source set is mandatory: root instructions, model configuration, all three scoped files, both skills with native `name`/`description` frontmatter, `audit/template.md`, `deliver/plan-template.md`, and both GitHub template sources. There is no partial-success mode.

Generated Markdown carries `GENERATED by .ai/scripts/sync.mjs` below native YAML frontmatter (or at the start without frontmatter). Skill metadata is preserved, not reinterpreted as provider settings. Non-Markdown resources are copied byte-for-byte; each copied skill has a generated `.managed-by-sync.json` recording their SHA-256 ownership. Newly generated Markdown and ownership manifests use LF; comparisons tolerate CRLF checkout conversion without rewriting files. Non-Markdown resource ownership and content remain byte-exact, including nested resources named `.managed-by-sync.json`. Skill trees have the same depth and internal layout, preserving relative resource and repository links. Shared issue/PR templates currently have no relative links; review link bases when adding any, particularly because the PR adapter is one directory shallower.

All sources and destinations are preflighted before any writes. Existing unmarked Markdown, unowned or edited non-Markdown resources, invalid manifests, ancestor-file collisions, symlinks/junctions, hard-linked files, and path traversal are rejected. Unrelated regular provider files, settings, and `.github/CODEOWNERS` are never changed. Ordinary source drift updates owned outputs; the generator never deletes files. Obsolete owned files are reported as blockers and require separately authorized manual cleanup. No marker is proof of human approval. Run on a quiescent checkout; preflight is not a transaction against concurrent filesystem changes.

`--check` never creates directories or writes files. It reports missing outputs, stale content, unowned collisions, and obsolete owned files separately in deterministic path order. CLI exit codes: **0** synchronized/successful generation, **1** check-mode output drift/collisions, **2** missing/invalid sources, unsafe paths, generation collisions, or invalid usage. Generated adapters are intended for version control alongside their sources so fresh complete checkouts can pass `--check`; generation itself does not stage or commit anything.

### Provider capability and evidence

The following distinguishes documented native formats from actual observation. First-party references were supplied/confirmed during planning; they are not evidence of a live run of this generated configuration.

| Provider/surface | Native entry and procedure | Scoped rules and approval fallback | Evidence / verification limit |
|---|---|---|---|
| Copilot CLI | Root `AGENTS.md`, `.github/copilot-instructions.md`, canonical `.agents/skills/`; invoke `/deliver` or `/audit` when listed, or request the skill by name | Generated `applyTo` instructions where supported; otherwise read shared scope files. Use native plan approval or wait for a reply | Copilot executable and canonical skill listing observed in this environment; no separate end-to-end discovery/approval smoke run |
| Copilot IDE | Repository instructions and canonical `.agents/skills/` in supported IDE versions; select the skill in chat or ask for it by name | IDE support for scoped instructions/plan tools varies; use explicit reads and full-plan conversational approval when absent | [GitHub skills][github-skills] and [instructions][github-instructions]; no live IDE validation |
| Copilot cloud agent | Same documented `.agents/skills/` discovery and repository instructions | Trusted maintainer approval addressed to the executing agent and separately authorized platform Git/PR effects; see cloud limits below | GitHub documentation covers cloud skills; cloud dispatch and pause/resume not tested |
| Claude Code CLI / IDE extension | Root `CLAUDE.md` imports `@AGENTS.md`; copied `.claude/skills/` support `/deliver`, `/audit`, or natural-language selection | `.claude/rules/` uses native `paths` YAML list or comma-separated string. Native plan mode if available, otherwise full-plan conversational approval | [Claude memory][claude-memory] and [skills][claude-skills]; Claude CLI absent, IDE not tested |
| Claude hosted/cloud execution | Use supported repository memory/skill discovery only after confirming the specific hosted surface | Same approved-handoff requirements; unsupported if a safe approval handoff cannot be established | No live hosted verification; local formats do not prove cloud parity |
| Codex CLI / IDE | Native `AGENTS.md` and `.agents/skills/`; invoke `$deliver`, `$audit`, or select/request a listed skill | No generated scoped-rule configuration: explicitly read the shared scope table. Use host-native approval when available, otherwise wait for a reply | [Codex instructions][codex-instructions] and [skills][codex-skills]; Codex CLI absent, IDE not tested |
| Codex cloud | Confirm current hosted skill support; explicitly read canonical skill files when automatic discovery is unavailable | Approved plan/handoff and separately authorized platform side effects; do not assume an interactive pause | No live cloud verification or promised CLI/cloud equivalence |

After generating or changing skills/instructions, start a fresh provider session (and reload the IDE window if discovery remains cached). Confirm the skill is actually listed and inspect loaded memory/rules where the host exposes that view before relying on automatic selection. Refresh behavior is version-specific, not enforced by this repository.

For every host without automatic skills, ask it to read `.agents/skills/deliver/SKILL.md` or `.agents/skills/audit/SKILL.md` and the referenced resources directly. Without automatic scoped rules, read the applicable `.ai/instructions/*.instructions.md` from the `AGENTS.md` table. Without subagents, execute role guidance inline and label self-review honestly. Neither a native skill nor a provider's tool-permission prompt replaces explicit approval of the complete plan.

[github-skills]: https://docs.github.com/en/copilot/concepts/agents/about-agent-skills
[github-instructions]: https://docs.github.com/en/copilot/how-tos/configure-custom-instructions/add-repository-instructions
[claude-memory]: https://code.claude.com/docs/en/memory
[claude-skills]: https://code.claude.com/docs/en/skills
[codex-instructions]: https://developers.openai.com/codex/guides/agents-md
[codex-skills]: https://developers.openai.com/codex/skills

## Resource index

| Step | Resource | Usage |
|---|---|---|
| Intake | [Issue/intake template](templates/issue.md) | Capture a feature or issue without making publication or implementation implicit |
| Plan and WPs | [Delivery skill](../.agents/skills/deliver/SKILL.md) and [canonical plan template](../.agents/skills/deliver/plan-template.md) | Complete plan, bounded WPs, dependencies, checks, and approval record |
| Implement | [Implementer](agents/implementer.md) | One approved WP, direct or delegated |
| Audit | [Audit skill](../.agents/skills/audit/SKILL.md), [auditor](agents/auditor.md), and [canonical audit template](../.agents/skills/audit/template.md) | Fresh evidence and separately approved report publication |
| Remediate | [Remediation tracker](templates/remediation.md) | Map findings to the separately approved canonical plan, without duplicating WP definitions |
| Review | [Reviewer](agents/reviewer.md) and [review template](templates/review.md) | Independent read-only review, or clearly labeled self-review |
| PR preparation | [PR template](templates/pull-request.md) | Local draft first; publication is separately authorized |
| Resume or change provider | [Handoff template](templates/handoff.md) | Carry exact scope, approval, baseline, evidence, and next action |

Do not create a second canonical plan or audit report template here. Use the two skill-owned resources above. Fill placeholders honestly, use N/A where needed, and omit closing keywords when no issue is being closed. The generator adapts issue and PR content for GitHub; shared templates themselves are not provider configurations.

## Practical use

1. For a feature or issue, read the intake template and follow the canonical delivery skill. Resolve material unknowns before presenting the full plan.
2. Use native plan approval; otherwise present the full plan in the conversation and wait for explicit approval. Record the exact quote, approver, plan version, and native reference. Tool consent is not this gate.
3. Inspect the current branch, HEAD, and user edits. Execute one approved WP at a time. If handing off, include its verbatim definition and applicable scope instructions.
4. Validate the WP, then use a separate read-only reviewer if supported and permitted. Otherwise record **Self-review (not independent)**, explicitly noting the lack of independent context, with the same evidence requirements.
5. Deliver a local summary or PR draft without staging, committing, pushing, publishing, or merging unless those specific actions were explicitly authorized.

For a saved plan or cross-provider handoff, verify the approval record against the original trusted conversation or maintainer comment and compare the baseline with the actual checkout. A copied quote is context, not a portable authorization token. If authority cannot be verified, or changes invalidate the approved scope, pause for renewed approval; do not infer permission from the handoff.

For an audit, inspect current code and prior rounds without modifying tracked files. Cover backend, GraphQL authorization, privacy/logging, frontend/i18n/accessibility, CI/releases/supply chain, and documentation, or explicitly mark excluded areas. Present the full report and proposed documentation paths for publication approval. Use a separate delivery plan and remediation tracker for fixes; report approval never authorizes implementation.

### Worked lifecycle paths

| Intake | Safe progression | Stop condition |
|---|---|---|
| Feature request | Capture behavior and non-goals → complete plan → native approval → one WP → focused checks → review → handoff | Material ambiguity, scope growth, or conflicting user changes |
| Issue | Read and verify the reported behavior → same delivery gates; prepare only a local PR draft by default | Assignment or issue instructions are mistaken for approval or external authority |
| Audit to remediation | Discover unused round → fresh evidence and coverage ledger → complete report → separate publication approval → revalidated finding IDs → delivery plan and separate remediation approval | Missing evidence makes readiness inconclusive; report approval cannot authorize fixes |
| Saved plan / new provider | Read full plan and WP → verify original approval and actual baseline → confirm dependencies → resume only that approved WP | Unverifiable authority, material baseline drift, or unsupported host capabilities |

Keep every artifact in native session storage/conversation unless its repository publication
is explicitly in scope. Use the skill-owned plan and audit templates as the single canonical
definitions; the remediation tracker links WPs rather than copying them. Their relative links
work at the same depth in `.agents/skills/` and generated `.claude/skills/`. Adjust links when
copying a filled artifact to another destination.

Checks use pass/fail/not-run with exact commands and limitations. Audit verification is
selected for relevant areas, not a demand to install every tool or contact live systems.
Do not change tracked files during investigation; unsafe or unavailable checks remain
disclosed evidence gaps. Missing runtime or external evidence may require **Inconclusive /
not fully assessed**, never an unsupported readiness or GDPR compliance claim.

## Cloud and other capability limits

Cloud handoffs require trusted maintainer approval addressed to the executing agent, an exact approved plan, and separately authorized platform side effects. Assignment is not approval. If a cloud service automatically commits, pushes, creates a branch, or publishes a PR, obtain explicit authorization for those actions on the named task branch **before dispatch**. Updating an existing PR with the plan also requires authorization.

A host that cannot pause/resume at the gate needs an interactively approved plan and a supported handoff; otherwise that execution path is unsupported. Do not claim live CLI, IDE, cloud, or independent-review support solely because files exist. Repository model preferences remain subject to explicit user choice and host capability/policy; tool permissions stay with the host/user.

Static checks can establish reference integrity, generated parity, and workflow contract consistency. They cannot prove human approval, branch protection, runtime sandboxing, legal compliance, or deployed configuration. Follow active host controls and report blocks; never work around them.

## Maintenance and verification

Edit shared sources only within an explicitly approved scope; regenerate only owned adapters after source validation. Preserve unrelated provider configuration and `.github/CODEOWNERS`. From the repository root:

```text
node .ai/scripts/sync.mjs --check
node --test tests/ai-workflow.test.mjs
node --test scripts/check-npm-audit.test.mjs scripts/check-renovate-automerge.test.mjs scripts/release-safety.test.mjs
git diff --check
```

The independent `ai-workflow` job in `.github/workflows/pr.yml` runs the first two commands with pinned checkout/setup-node actions, read-only repository permissions, and no dependency install. Existing application and repository-policy jobs remain separate.

Tests invoke the actual root-parameterized generator and CLI in disposable fixtures under `tests/`, cleaning up after themselves. They check the fixed output set, frontmatter/scope mappings, resource bytes and ownership, source changes, idempotence, collision preflight, read-only check mode, obsolete outputs, unsafe paths, local links, and lifecycle/reviewer contracts in canonical and generated copies. Symlink checks are explicitly skipped when the host cannot create links. Static lifecycle examples are artifact checks, **not LLM behavioral proof**, human-consent verification, or sandbox enforcement.

Local validation found Node and Copilot on PATH; Claude and Codex binaries were absent. No provider installation, authentication, paid invocation, IDE session, cloud dispatch, or end-to-end provider execution was performed. To validate discovery later, use an authorized fresh session in each installed host, confirm both skills and applicable rules are loaded, and record the host/version and observations separately from these static checks. Application builds are unnecessary for these configuration/documentation-only changes.
