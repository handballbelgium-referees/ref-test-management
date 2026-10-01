# Privacy & Data-Subject Requests

This document describes the privacy controls implemented by RefTest Management and the operational procedure for handling requests from test participants. It supports, but does not replace, legal advice or the controller's GDPR compliance obligations.

## Controller and Contact

| Item                   | Value                                      |
| ---------------------- | ------------------------------------------ |
| Data controller        | Kristof Gilis                              |
| Correspondence address | Pipelstraat 26, 3800 Sint-Truiden, Belgium |
| Privacy contact        | kristof.gilis@outlook.be                   |
| Current notice version | `1.0`, effective 3 August 2026             |

The public privacy notice is available at `/privacy` in English, Dutch, French, and German.

## Data Processing

### Data Categories

The service processes participant name, email address, invitation token, assessment progress, answers, score, preferred language, and timestamps. It also holds limited administrator identity and audit data needed to operate and secure the service.

### Purposes and Legal Bases

| Activity               | Purpose                                            | Legal basis          |
| ---------------------- | -------------------------------------------------- | -------------------- |
| Invitation and contact | Send and manage an assessment invitation           | Legitimate interests |
| Optional assessment    | Run, score, and communicate an assessment result   | Explicit consent     |
| Service security       | Prevent misuse and investigate operational changes | Legitimate interests |

The application requires an affirmative acceptance of the current privacy-notice version before a participant can start an assessment. The server persists the accepted version and timestamp, and rejects attempts to start without it. Because the controller must be able to demonstrate that consent was given (Art. 7(1)), the accepted version and timestamp are retained when a record is anonymized — on their own, detached from the redacted name and email, they no longer identify anyone.

### Recipients

Beyond the service providers above, participant data is disclosed to people inside Handball Belgium as part of running an assessment. Art. 13(1)(e) requires these recipient categories to be named:

- **Assessment administrators.** Staff who create, schedule, and manage assessments see the participant's name, email address, scheduled time, status, and score in the application, and receive batch report emails summarising assessment results.
- **Approvers.** Where an assessment requires approval, the designated approvers receive an approval-request email and an approval-decision email. Both carry the participant's name, email address, and scheduled time.

No participant data is disclosed to any other recipient, and none is sold or used for marketing.

### Service Providers

The notice identifies the following service providers:

- Auth0 for authentication
- Brevo for email delivery
- IHF Rules Questions for assessment content and result calculation

Before production use, the controller must confirm each provider's data-processing agreement, hosting location, and any required safeguards for international transfers.

## Retention and Erasure

The configured retention period is three years. The application records both completion and expiry timestamps. A daily `PrivacyRetentionService` erases RefTests that have reached a terminal state after the retention period: `Completed` and `Expired` (measured from their completion/expiry timestamp), and `PendingApproval` and `Rejected` (measured from creation, since neither records a completion timestamp).

Erasure is a two-step, irreversible process:

1. **Anonymize (first request).** The RefTest record is kept, but its name, email, and access token are redacted in place (`***`/equivalent placeholders), and the same redaction is applied to personal-data fields recorded in its historical audit trail (e.g. the name/email captured when the RefTest was created or last updated). Background jobs referencing the RefTest that are still queued or in flight (invitation/result/report emails) are cancelled and their payloads cleared. The record and its (now redacted) audit trail remain visible to staff, so it stays clear *when* and *why* a RefTest was anonymized. Once anonymized, only the delete/erase action remains available for that RefTest — all other operations are disabled.
2. **Permanent delete.** The RefTest row itself is removed entirely. Personal data is redacted from its audit trail first, so nothing identifying is left behind; the (now redacted) trail then follows the normal audit-log retention schedule (see `AuditLogConfiguration.RetentionDays`) rather than being force-deleted.

The standard administrative `deleteRefTests` operation and the participant-facing `withdrawConsent` operation both use this same erasure path. `withdrawConsent` performs step 1; `deleteRefTests` performs step 1 followed by step 2, so a staff delete leaves no personal data whether or not the RefTest was already anonymized. An already delivered email cannot be recalled from a participant's inbox.

### Known limits of erasure

These are bounded, deliberate limits rather than gaps, and are disclosed here so the picture is complete:

- **A job already being processed may still deliver.** Cancellation marks an in-flight job terminal, but a worker that has already handed the message to the email provider cannot be stopped. The window is seconds.
- **Jobs that already ran keep their payload briefly.** A completed job is not rewritten by erasure; its payload (which held name, email, and score) is removed when the job row is deleted on the normal schedule — 7 days for completed jobs, 30 for failed ones.
- **Audit events lose personal data on the audit retention schedule, not on request.** Erasing or deleting a specific RefTest redacts *that* RefTest's audit trail immediately. For every other record, the daily audit cleanup redacts the payload and actor identity once the event passes `AuditLogConfiguration.RetentionDays` (90 days by default), keeping only what happened and when.
- **Application logs.** Logs no longer record participant names, email addresses, invitation tokens, test URLs, or scores; records are identified by their RefTest id, and recipient addresses are masked. Logs written before this change may still contain personal data and are subject to the hosting provider's own retention, not the application's.

Backups, email-provider retention, and third-party logs are outside the application's database cleanup. This also applies to export PDFs after email delivery: application cleanup cannot recall a message or remove copies held by Brevo or the recipient's mailbox. The controller must agree retention and deletion procedures with the relevant providers and document them.

## Handling Requests

Access and portability requests for current RefTest records can be made through the public self-service form at `/privacy`. Other requests, including corrections and requests that cannot be completed through the self-service flow, can be sent to `kristof.gilis@outlook.be`.

### Self-service access and portability export

The participant enters the email address used for their RefTest. The application trims the address and matches it case-insensitively against RefTest records that have not been anonymized. The form returns the same acknowledgement whether or not a record matches; only a matching address is queued for a verification email with a section for every enabled language. The public GraphQL operations require no login or Auth0 permission. Control of the matching mailbox, demonstrated with a one-time key, is the confirmation method; it is not an independent legal-identity check.

Each language section links to `/privacy/export-confirmation?lang=<language>#<key>`, so the confirmation page opens in that section's language. Opening a link only displays the confirmation page: the client removes the key from the address bar/history and sends it to the server only after the participant explicitly selects Confirm. The key is single-use and expires after 24 hours by default; its lifetime can be configured from 1 through 168 hours (see [PersonalDataExportConfiguration](CONFIGURATION.md#personaldataexportconfiguration)).

After confirmation, the application builds the export from the data available at delivery time, not from a snapshot taken when the request was submitted. It includes every then-current, non-anonymized RefTest matched to that email, regardless of RefTest status, and all retained audit events in those RefTest streams, including staff actions. Archived or previously redacted events are included in their retained form. Before export, staff/third-party actor identities and known third-party fields are omitted, other email addresses are redacted, and sensitive values such as invitation tokens are removed. Records anonymized before delivery are not included.

The export is sent to the verified address as PDF email attachment(s); each PDF contains a section in every enabled language, following the layout used by RefTest report PDFs. Larger exports may be split into numbered PDF parts, with every part containing all enabled languages. If a part exceeds the email provider's size limit, delivery fails rather than sending a truncated or partial export. Acceptance by the email provider does not prove that the recipient received or opened the message.

Challenge and delivery events are recorded on a separate audit stream with non-sensitive lifecycle details such as attempts, verification, attachment count, and safe failure codes. These events do not contain the recipient address, raw key, or provider error text and follow the normal audit-log retention and cleanup policy. Once the provider accepts the challenge email, its protected retry copy of the key is cleared. Confirmation clears the challenge state; successful delivery or terminal failure also clears the request's recipient details. An expired, unverified request is cleaned up by a background service every 15 minutes by default: it clears the email, key hash, protected key, and challenge state. The request row remains as non-identifying lifecycle metadata; this cleanup does not change global audit retention.

If any RefTest for the matching email is erased before an export is complete, the application clears all uncleared export requests for that address and cancels their pending or in-flight challenge and delivery jobs, even if other RefTests for the same address remain. This prevents a queued delivery from sending a stale pre-erasure export. Once an email has been accepted by the provider, the application cannot recall it or its PDF attachments.

### Self-service withdrawal (any status, including completed)

A participant can withdraw consent and immediately anonymize their own data using only their invitation link, via the "withdraw consent" action on the welcome, in-progress, and results pages. This uses the same erasure path described above (starting with the anonymize step) and requires no identity verification beyond the token, matching the low bar already used to give consent. This is available regardless of RefTest status, including `Completed`, because anonymizing no longer destroys the record or its audit trail outright — the completion timestamp, score, and the fact that the RefTest happened remain visible to staff (with the name/email redacted); only a subsequent, separate request permanently deletes the row. A participant or controller may request permanent deletion of an already-anonymized RefTest at any time via the standard erasure process; since it no longer holds personal data, no further identity verification is needed for that step.

### Staff-assisted requests (correction, erasure, and export support)

1. Record the request date, requester contact details, requested right, and RefTest identifier if available.
2. For requests handled by staff, verify the requester's identity using a proportionate method before disclosing, correcting, or erasing data. Do not ask for more personal data than necessary.
3. Locate the RefTest using the authorized administration interface; access to participant details requires the relevant `ref-tests` permission.
4. If the participant cannot use self-service or asks for information outside its scope, prepare a secure copy of the relevant data and send it only after identity verification.
5. For corrections, update the minimum necessary fields through the administration interface.
6. For erasure or withdrawal of consent (any status, if the participant no longer has their token or prefers not to use self-service), delete the RefTest using the administration interface. A staff delete anonymizes the record and removes the row in a single action, leaving a redacted audit trail behind as proof that the erasure happened.
7. Reply without undue delay and normally within one month. Record the action taken and any lawful reason for a refusal or extension.
8. Where a processor received relevant data, follow the processor's documented deletion or request-handling process.

## Release Checklist

Before releasing a privacy-related change, confirm:

- the published controller contact details and notice version are correct in `PrivacyConfiguration`
- the database migrations are applied
- the privacy notice is complete in all four supported languages
- the invitation email includes a link to the public privacy notice
- processor agreements and international-transfer safeguards are current
- the controller's records of processing activities and legitimate-interests assessment are current
- backup and restore procedures do not reintroduce erased participant data without a documented follow-up deletion process

## Limitations

The application does not itself determine the controller's legal basis, execute processor agreements, assess international transfers, or submit supervisory-authority notifications. Those are controller responsibilities and should be reviewed with qualified privacy counsel.

## Operational compliance records

For controller-owned operational evidence that sits outside application source code, see:

- [GDPR Processor & DPA Register](./GDPR-PROCESSOR-REGISTER.md)
- [GDPR Operations Evidence (Backups, Logging, DSAR)](./GDPR-OPERATIONS-EVIDENCE.md)
- [GDPR Record of Processing Activities (RoPA)](./GDPR-ROPA.md)
- [GDPR Legal Basis Record](./GDPR-LEGAL-BASIS-RECORD.md)
- [GDPR DSAR Operating Procedure](./GDPR-DSAR-PROCEDURE.md)
