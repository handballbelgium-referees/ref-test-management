# GDPR Record of Processing Activities (RoPA)

This document records the main personal-data processing activities for RefTest Management.

## Controller

- **Controller:** Kristof Gilis (individual owner/operator)
- **Project status:** hobby project, potentially future operational use
- **Primary contact:** kristof.gilis@outlook.be

## Processing activities

| Activity | Data subjects | Personal data categories | Purpose | Legal basis | Recipients/processors | Retention | Security controls |
|---|---|---|---|---|---|---|---|
| RefTest invitation and lifecycle management | RefTest participants | Name, email, invitation token, status timestamps, answers, score, language | Run and manage referee tests | Legitimate interests (administration) + consent-dependent participant flow where implemented | Azure (hosting), Brevo (email), Auth0 (admin auth) | See `docs/PRIVACY.md` and app retention configuration | Authenticated admin access, redaction/anonymization flow, audit logging |
| Participant self-service test flow | RefTest participants | Token-bound session data, progress, answers, completion metadata | Deliver participant test experience | Consent + service operation | Azure hosting, optional external question source | See `docs/PRIVACY.md` | Tokenized access, backend validation, retention jobs |
| Administrative access and authorization | Administrators/approvers | Staff identity claims, permissions, account metadata | Authorize access to admin functionality | Legitimate interests (security and role control) | Auth0, Azure hosting | Per identity-provider and app log retention terms | RBAC/permission policies, secured cookies/JWT |
| Email notifications | RefTest participants, admins/approvers | Email address, message metadata/content needed for notification | Invitation/result/report and approval messages | Legitimate interests / consent-aligned service operation | Brevo | Processor-side + app-side retention policies | Job queue, redaction controls, operational logs |

## Processor/data-flow notes

- Azure App Services region: **West Europe**.
- Azure SQL Server region: **Belgium Central**.
- Auth0 tenant: **Belgium/Europe** (controller-provided).
- Brevo account tier: **Free** (controller-provided).
- IHF Rules Questions service: **no personal data sent** (controller-provided).

## International transfer note

Transfer safeguards and processor legal terms are tracked in:

- `docs/GDPR-PROCESSOR-REGISTER.md`

## Review cadence

- Review this RoPA at least every 6 months and whenever processor setup or data categories change.

## Review log

| Date | Reviewer | Change summary |
|---|---|---|
| 2026-09-22 | Kristof Gilis | Initial RoPA created for single-owner hobby project with real-user-data readiness. |
