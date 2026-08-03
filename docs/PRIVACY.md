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

The configured retention period is three years. The application records both completion and expiry timestamps. A daily `PrivacyRetentionService` removes completed or expired RefTests after the retention period.

Permanent erasure removes:

- the RefTest record and its participant data
- queued or completed background-job payloads that reference the RefTest
- audit events in the RefTest's audit stream, including archived events

The standard administrative `deleteRefTests` operation and the participant-facing `withdrawConsent` operation both use this same erasure path. An already delivered email cannot be recalled from a participant's inbox.

Backups, email-provider retention, and third-party logs are outside the application's database cleanup. Their retention and deletion procedures must be agreed with the relevant provider and documented by the controller.

## Handling Requests

Requests are received at `kristof.gilis@outlook.be`. There is no public self-service export endpoint, because the token in an invitation link is not sufficient proof of identity for access, correction, or portability requests.

### Self-service withdrawal (before completion)

A participant who has not yet completed their RefTest can withdraw consent and immediately erase their own data using only their invitation link, via the "withdraw consent" action on the welcome and in-progress pages. This uses the same permanent-erasure path described above (RefTest, jobs, and audit events) and requires no identity verification beyond the token, matching the low bar already used to give consent. It is not available once the RefTest is `Completed`.

### Verified-identity requests (access, correction, completed RefTests)

1. Record the request date, requester contact details, requested right, and RefTest identifier if available.
2. Verify the requester's identity using a proportionate method before disclosing, correcting, or erasing data. Do not ask for more personal data than necessary.
3. Locate the RefTest using the authorized administration interface; access to participant details requires the relevant `ref-tests` permission.
4. For access or portability, prepare a secure copy of the participant's identity, timing, progress, answers, and results. Send it only after identity verification.
5. For corrections, update the minimum necessary fields through the administration interface.
6. For erasure or withdrawal of consent on a completed RefTest, delete the RefTest using the administration interface. This invokes complete application-database erasure.
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
