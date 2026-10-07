# GDPR Processor & DPA Register

This document tracks processor relationships and the contract/evidence needed to support GDPR Article 28 and international-transfer accountability.

## Controller vs processor (this project)

- **Controller:** Handball Belgium / service owner (you, acting as controller)
- **Processors:** Third-party providers processing personal data on your behalf (e.g., Microsoft Azure, Auth0, Brevo). IHF Rules Questions is an external service under assessment; its processor and personal-data classification are not yet established.

> Note: you are generally **not** both controller and processor for the same processing activity unless you process data on behalf of another controller as a separate legal role.

---

## Processor register

| Processor | Service used | Data categories | Purpose | DPA in place | Transfer mechanism | Retention/deletion terms | Evidence location | Last reviewed |
|---|---|---|---|---|---|---|---|---|
| Microsoft (Azure) | Azure SQL, App Service, platform backups/logging | Participant identity, test data, operational logs | Hosting and infrastructure operations | Assumed yes (Microsoft standard DPA/Online Services Terms) — acceptance evidence pending | EEA hosting in use; non-EEA subprocessors/SCC details pending controller evidence | Azure SQL/App Service retention and deletion behavior documented in `GDPR-OPERATIONS-EVIDENCE.md` (some items pending) | Azure portal + Microsoft legal terms evidence (to be linked) | 2026-09-22 |
| Auth0 | Identity/authz and management API (free tenant) | Admin/staff identity + permissions metadata | Authentication and authorization | Pending confirmation | Tenant indicated as Belgium/Europe; transfer safeguard details pending | Pending confirmation | Auth0 tenant settings + legal terms (to be linked) | 2026-09-22 |
| Brevo | Email delivery (free account) | Recipient email, message metadata/content | Invitation/result/report email sending | Pending confirmation | Pending confirmation | Pending confirmation | Brevo account/legal terms (to be linked) | 2026-09-22 |
| IHF Rules Questions (classification under assessment) | External question-retrieval and score-calculation API | Question and answer IDs are globally assigned content identifiers generated from GUIDs; score requests include question IDs, submitted selected-answer IDs, and score configuration. The app request contains no participant, RefTest, or attempt ID; whether submitted responses or request metadata are participant data or linkable is unconfirmed. | Question retrieval and score calculation | Applicability pending data-classification review; contract evidence not provided | Pending confirmation | Service logging and retention terms pending confirmation | Owner attestation, app source paths, and a read-only schema check are recorded in the 2026-10-07 review-log entry; no independent IHF statement provided | 2026-10-07 |

### Region/location and integration data-flow notes (known)

- Azure App Services region: **West Europe**.
- Azure SQL Server region: **Belgium Central**.
- Auth0 tenant location: **Belgium/Europe** (controller-provided).
- Brevo account tier: **Free** (controller-provided).
- IHF Rules Questions data flow (2026-10-07): the repository data owner, who also owns the question/answer repository, stated that no participant data goes to the service and clarified that question and selected-answer IDs are globally assigned content identifiers generated from GUIDs, not participant, RefTest, or attempt IDs. A read-only schema introspection confirmed that `calculateScore` accepts `questionIds`, `selectedAnswerIds`, and `scoreConfigurationInput`. Source review found that score requests include question IDs and the participant's selected-answer IDs (`RefTestManagement.Api/Graphql/Mutations/Lifecycle/RefTestLifecycleMutations.cs` and `RefTestManagement.Application/Services/IHFRulesQuestionsService.cs`); no participant, RefTest, or attempt ID is present in the app request. Whether the submitted response or service request metadata can be linked to an individual remains unconfirmed. Do not treat the flow as verified non-linkable until this is resolved. The owner statement is not independent IHF evidence; processor/personal-data classification, contract/DPA applicability, transfer, logging, retention, and deletion terms remain pending.

---

## Required evidence checklist per processor

For each processor, store or link:

1. Signed/accepted **DPA** (or equivalent contractual terms)
2. Subprocessor list and review date
3. Data location/region statement
4. International transfer safeguard (if applicable)
5. Security and incident-notification commitments
6. Data return/deletion terms at contract end
7. Operational contact/channel for DSAR support

---

## Review log

| Date | Reviewer | Summary | Follow-up |
|---|---|---|---|
| 2026-09-22 | Kristof Gilis | Bootstrapped processor register with known Azure hosting regions and default-baseline assumptions. | Attach concrete DPA/transfer/retention evidence links for all processors. |
| 2026-09-22 | Kristof Gilis | Added controller-provided facts: Auth0 Belgium/Europe tenant, Brevo free account, IHF service receives no personal data. | Attach supporting links/screenshots for Auth0/Brevo terms and IHF data-flow proof. |
| 2026-10-07 | Repository data owner (attestation) | The owner, who also owns the question/answer repository, stated that no participant data goes to IHF and clarified that question and selected-answer IDs are globally assigned content identifiers generated from GUIDs, not participant, RefTest, or attempt IDs. Read-only schema introspection confirmed that `calculateScore` accepts question IDs, selected-answer IDs, and score configuration. Separately, source review found that score requests include question IDs and the participant's submitted selected-answer IDs, with no participant, RefTest, or attempt ID in the app request. Whether the submitted response or service-side request/log metadata can be linked to an individual remains unresolved. | Confirm the service's request/log metadata and retention, and determine whether submitted response data can be attributed to a participant through available records; attach owner evidence before classifying the flow or IHF's processor status. |
