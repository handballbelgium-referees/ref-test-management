# Deep Audit — Round {n}

| | |
|---|---|
| **Repository** | `handballbelgium-referees/ref-test-management` |
| **Date** | {date} |
| **Commit audited** | `{sha}` (`{branch}`) |
| **Previous reports** | {links to completed prior rounds} |
| **Scope** | {areas covered} |
| **Method** | Fresh, read-only source review with reproducible validation |

---

## Executive summary

**Production readiness: {READY / NOT READY} — {evidence-backed conclusion}.**

{Concise summary of the strongest controls, material gaps, and limits of this audit.}

**GDPR evidence status ({source-verifiable / non-repository / mixed}): {what the code and repository establish, what remains unverified, and why}.**

### Verdict by area

| Area | Verdict |
|---|---|
| Backend domain logic and invariants | {Strong / Needs remediation / Not ready — evidence} |
| GraphQL authentication and authorization | {verdict} |
| Background jobs, transactions, and logging | {verdict} |
| Frontend correctness, i18n, and accessibility | {verdict} |
| GDPR/privacy technical controls | {verdict; separate external evidence limits} |
| CI/CD and supply chain | {verdict} |
| Documentation parity | {verdict} |

| Severity | Count |
|---|---:|
| 🔴 Critical | {n} |
| 🟠 High | {n} |
| 🟡 Medium | {n} |
| ⚪ Low | {n} |

## Findings

| # | Severity | Area | Finding |
|---|---|---|---|
| R{n}-01 | {severity} | {area} | {short, concrete title} |

### {Severity} findings

#### R{n}-01 — {Title}

- **Severity:** {severity}
- **Area:** {area}
- **Evidence:** `{path}:{line}` — {observed fact}

{Explain the impact and the conditions needed to trigger it.}

**Recommendation:** {smallest effective remediation}

{Repeat per finding, grouped by Critical, High, Medium, then Low. Omit empty severity sections.}

## Validation performed

| Check | Result |
|---|---|
| `{command}` | {pass/fail/not run and concise reason} |

## Previous-round status

| Prior finding | Status | Current evidence |
|---|---|---|
| {R{n-1}-NN} | {✅ Resolved / 🔄 Partial / ❌ Open / ⚪ Not rechecked} | `{path}:{line}` or reason not rechecked |

## Source-verifiable vs non-repository evidence

### Source-verifiable

- {Repository fact, with exact file/line or validation output.}

### Non-repository or operational evidence

- {Evidence needed, who or what can provide it, and why it could not be verified from this repository.}

## Remediation status

{Link the existing remediation tracker, or state that findings are ready for `/deliver R{n}`. Do not claim a fix is complete until verified.}
