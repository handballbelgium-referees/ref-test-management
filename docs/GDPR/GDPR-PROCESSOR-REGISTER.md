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
| IHF Rules Questions (transient, unlinked score input at service boundary) | External question-retrieval and score-calculation API | Globally assigned question-content IDs for retrieval; question IDs, selected-answer IDs, and score configuration for scoring. Requests do not include a participant, RefTest, or attempt ID; the application retains the local association with the active RefTest. Whether this payload is personal data remains pending controller determination. | Question retrieval and score calculation | DPA applicability and contract evidence not independently confirmed | Pending confirmation | API source calculates in memory without persisting submitted selections or logging request variables. At inspection, App Service application/HTTP logs, detailed errors, failed-request tracing, and Azure Monitor diagnostic settings were disabled; owner reports no backups and no Application Insights. | Owner attestation, source review, live Azure configuration, and privacy notice; no independent IHF statement or processor terms provided | 2026-10-07 |

### Region/location and integration data-flow notes (known)

- Azure App Services region: **West Europe**.
- Azure SQL Server region: **Belgium Central**.
- Auth0 tenant location: **Belgium/Europe** (controller-provided).
- Brevo account tier: **Free** (controller-provided).
- IHF Rules Questions owner attestation (2026-10-07): the repository owner states that no participant data goes to the service, clarifies that question and answer IDs are globally assigned content identifiers generated from GUIDs, and reports that no backups exist. The source findings below separately record that selected-answer IDs are sent for scoring, without participant, RefTest, or attempt IDs.
- IHF Rules Questions source review (2026-10-07; API repository revision `a6d68be`): `Question.Id` and `Answer.Id` are GUID-backed content IDs (`Question.cs`, `Answer.cs`); Hot Chocolate exposes typed global IDs. `ScoreCalculationQueries.CalculateScore` accepts question IDs, the selected answer IDs "selected by the user", and optional scoring configuration. It reads the question/answer data, calculates the score in memory, and returns score results; the resolver does not persist the submitted selections. The referee app passes `RefTest.QuestionIds` and the participant's selected-answer IDs to this call (`RefTestLifecycleMutations.cs`, `IHFRulesQuestionsService.cs`); its configured client explicitly adds `Accept-Language` but no participant, RefTest, or attempt ID. The API repository contains no application-level request-variable logging or Application Insights/HTTP-body logging configuration. The referee app retains selected IDs in its RefTest record after anonymization, but the record's participant name, email, email lookup hash, and real tokens are redacted; its RefTest GUID remains as the record key alongside response and test metadata. The owner reports no Application Insights and no backups; a read-only Azure query confirmed the App Service plan SKU is B1. A read-only Azure configuration check on 2026-10-07 found application logs set to `Off` for Blob, Table, and filesystem; HTTP logs disabled for Blob and filesystem; detailed error messages and failed-request tracing disabled; and no Azure Monitor diagnostic settings. The disabled HTTP filesystem sink reports a 35 MB retention cap; Blob retention is unset, and these values are inactive while logging is disabled. The privacy notice already states that delivered email cannot be recalled. At the IHF service boundary, the request has no identity/link key and the inspected service configuration provides no configured persistent request log or diagnostic export; selected-answer IDs are transient score inputs, not participant identifiers by themselves. The referee app can associate the call with its active RefTest at request time, but that local association is not sent to IHF. Azure-internal operational telemetry outside the inspected tenant-configured log settings was not independently examined. This is a technical classification of the IHF-side payload and retention, not a blanket claim that participant responses are never processed by the referee app or a legal conclusion about all controller-side processing.

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
| 2026-10-07 | Repository data owner (attestation) | The owner, who also owns the question/answer repository, stated that no participant data goes to IHF, clarified that question and answer IDs are globally assigned content identifiers generated from GUIDs, and reported that the API runs on an Azure App Service B1 plan without Application Insights. | Kept separate from source findings; see the source-review entry below. Application Insights absence alone does not establish the state of separate App Service logging; see the Azure configuration check. |
| 2026-10-07 | Repository data owner (attestation) | The owner reports that no backups exist and notes that the existing privacy notice explains that email already delivered cannot be recalled. | Recorded as owner-provided evidence. The notice covers copies outside the service's control; the IHF API has no configured request persistence or log export at inspection. |
| 2026-10-07 | Repository source review | Inspected the referee app request construction and the IHF API source at revision `a6d68be`. The API uses GUID-backed global question/answer content IDs. The score resolver accepts user-selected answer IDs and configuration, calculates a score in memory, and does not persist the submitted IDs or explicitly log GraphQL request variables in application code. The referee app sends no direct participant/RefTest/attempt ID but retains answer selections in the RefTest record after anonymization. API-repository configuration does not establish Azure App Service diagnostic logs, tracing, or retention. | Azure configuration and owner attestations are recorded below. The classification is scoped to the IHF service boundary; contract/DPA applicability and any broader legal classification remain separate items. |
| 2026-10-07 | Azure configuration check | A read-only query confirmed the API App Service plan SKU is B1. `az webapp log show` reported application logs `Off` for Blob, Table, and filesystem; HTTP logs disabled for Blob and filesystem; detailed errors and failed-request tracing disabled. The disabled HTTP filesystem sink has a 35 MB retention cap; Blob retention is unset. `az monitor diagnostic-settings list` returned no settings. This establishes the inspected configuration at that time, not the absence of Azure service-level operational telemetry outside these settings. | The inspected settings show no configured App Service request-log sink or diagnostic export; interpret alongside the source and owner evidence when assessing the IHF-side payload. |
