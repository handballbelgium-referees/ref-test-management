# GDPR Legal Basis Record

This document records the legal-basis rationale for personal-data processing in RefTest Management.

## Scope

- Applies when real personal data is processed (including real test users' names/emails).
- Complements `docs/PRIVACY.md` and `docs/GDPR-ROPA.md`.

## Legal-basis mapping

| Processing purpose | Data used | Proposed legal basis | Why this basis applies | Notes |
|---|---|---|---|---|
| Invite and manage referee tests | Name, email, status, scheduling metadata | Legitimate interests (Art. 6(1)(f)) | Necessary to organize, run, and monitor tests | Keep balancing assessment summary below updated |
| Participant test execution and scoring | Token/session data, answers, score | Consent and/or service operation as presented in participant flow | Participant explicitly accepts current notice before starting | Ensure notice version acceptance remains stored |
| Security, anti-abuse, and auditability | Identity/permission metadata, audit events | Legitimate interests (Art. 6(1)(f)) | Required to secure admin area and investigate misuse | Keep retention and redaction controls enforced |
| Email dispatch for service notifications | Email + test context needed for message | Legitimate interests / performance of service expectation | Required to deliver invitations/results/approvals | Limit content to what is necessary |

## Legitimate-interests balancing summary

- **Interest:** operate referee-testing workflow securely and reliably.
- **Necessity:** participant/admin contact and status data are required to deliver core workflow.
- **Safeguards:** minimization, role-based access, anonymization/erasure path, retention limits, audit redaction.
- **Residual risk:** processor/legal transfer terms and operations evidence must stay current.

## Data-minimization statement

- Use only the minimum personal data needed for invitation, test execution, and result/approval communication.
- Do not send personal data to the IHF question source service.

## Reassessment triggers

Reassess legal basis when:

1. new processor is added,
2. new data category is collected,
3. purpose changes materially,
4. automation/profiling is introduced,
5. participant communications change meaningfully.

## Review log

| Date | Reviewer | Change summary |
|---|---|---|
| 2026-09-22 | Kristof Gilis | Initial legal-basis record created for current architecture and processor landscape. |
