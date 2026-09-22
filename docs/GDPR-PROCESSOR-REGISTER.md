# GDPR Processor & DPA Register

This document tracks processor relationships and the contract/evidence needed to support GDPR Article 28 and international-transfer accountability.

## Controller vs processor (this project)

- **Controller:** Handball Belgium / service owner (you, acting as controller)
- **Processors:** Third-party providers processing personal data on your behalf (e.g., Microsoft Azure, Auth0, Brevo, IHF Rules Questions)

> Note: you are generally **not** both controller and processor for the same processing activity unless you process data on behalf of another controller as a separate legal role.

---

## Processor register

| Processor | Service used | Data categories | Purpose | DPA in place | Transfer mechanism | Retention/deletion terms | Evidence location | Last reviewed |
|---|---|---|---|---|---|---|---|---|
| Microsoft (Azure) | Azure SQL, App Service, platform backups/logging | Participant identity, test data, operational logs | Hosting and infrastructure operations | ☐ | ☐ | ☐ | ☐ | ☐ |
| Auth0 | Identity/authz and management API | Admin/staff identity + permissions metadata | Authentication and authorization | ☐ | ☐ | ☐ | ☐ | ☐ |
| Brevo | Email delivery | Recipient email, message metadata/content | Invitation/result/report email sending | ☐ | ☐ | ☐ | ☐ | ☐ |
| IHF Rules Questions | External question/result source | Test/question interaction metadata as applicable | Question retrieval and score/report integration | ☐ | ☐ | ☐ | ☐ | ☐ |

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
| ☐ | ☐ | ☐ | ☐ |

