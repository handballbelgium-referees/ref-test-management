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

The application requires an affirmative acceptance of the current privacy-notice version before a participant can start an assessment. The server persists the accepted version and timestamp, and rejects attempts to start without it.

### Service Providers

The notice identifies the following service providers:

- Auth0 for authentication
- Brevo for email delivery
- IHF Rules Questions for assessment content and result calculation

Before production use, the controller must confirm each provider's data-processing agreement, hosting location, and any required safeguards for international transfers.

## Retention and Erasure

The configured retention period is three years. The application records both completion and expiry timestamps. A daily `PrivacyRetentionService` erases completed or expired RefTests after the retention period.

Erasure is a two-step, irreversible process:

1. **Anonymize (first request).** The RefTest record is kept, but its name, email, and access token are redacted in place (`***`/equivalent placeholders), and the same redaction is applied to personal-data fields recorded in its historical audit trail (e.g. the name/email captured when the RefTest was created or last updated). Queued or completed background-job payloads (invitation/result email content) referencing the RefTest are deleted outright, since they hold transient personal data with no accountability value once processed. The record and its (now redacted) audit trail remain visible to staff, so it stays clear *when* and *why* a RefTest was anonymized. Once anonymized, only the delete/erase action remains available for that RefTest — all other operations are disabled.
2. **Permanent delete (a subsequent request against an already-anonymized RefTest).** The RefTest row itself is removed entirely. Its audit trail no longer contains personal data (it was redacted in step 1), so it is left in place and simply follows the normal audit-log retention/cleanup schedule (see `AuditLogConfiguration.RetentionDays`) rather than being force-deleted.

The standard administrative `deleteRefTests` operation and the participant-facing `withdrawConsent` operation both use this same erasure path, and both trigger step 1 or step 2 depending on whether the RefTest was already anonymized. An already delivered email cannot be recalled from a participant's inbox.

Backups, email-provider retention, and third-party logs are outside the application's database cleanup. Their retention and deletion procedures must be agreed with the relevant provider and documented by the controller.

## Handling Requests

Requests are received at `kristof.gilis@outlook.be`. There is no public self-service export endpoint, because the token in an invitation link is not sufficient proof of identity for access, correction, or portability requests.

### Self-service withdrawal (any status, including completed)

A participant can withdraw consent and immediately anonymize their own data using only their invitation link, via the "withdraw consent" action on the welcome, in-progress, and results pages. This uses the same erasure path described above (starting with the anonymize step) and requires no identity verification beyond the token, matching the low bar already used to give consent. This is available regardless of RefTest status, including `Completed`, because anonymizing no longer destroys the record or its audit trail outright — the completion timestamp, score, and the fact that the RefTest happened remain visible to staff (with the name/email redacted); only a subsequent, separate request permanently deletes the row. A participant or controller may request permanent deletion of an already-anonymized RefTest at any time via the standard erasure process; since it no longer holds personal data, no further identity verification is needed for that step.

### Verified-identity requests (access, correction, and requests without a valid token)

1. Record the request date, requester contact details, requested right, and RefTest identifier if available.
2. Verify the requester's identity using a proportionate method before disclosing, correcting, or erasing data. Do not ask for more personal data than necessary.
3. Locate the RefTest using the authorized administration interface; access to participant details requires the relevant `ref-tests` permission.
4. For access or portability, prepare a secure copy of the participant's identity, timing, progress, answers, and results. Send it only after identity verification.
5. For corrections, update the minimum necessary fields through the administration interface.
6. For erasure or withdrawal of consent (any status, if the participant no longer has their token or prefers not to use self-service), delete the RefTest using the administration interface. This anonymizes the record; request a second delete to permanently remove the anonymized row once no further need to retain it is identified.
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
