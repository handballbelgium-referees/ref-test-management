# GDPR Operations Evidence (Backups, Logging, DSAR)

This file captures operational evidence that cannot be proven from application source code alone.

## 1) Backup and restore controls (Azure SQL default backups)

### Current setup summary

- Backup mechanism: Azure SQL default automated backups
- Restore mechanism: Azure SQL point-in-time restore (platform-managed)
- Application policy requirement: if restored data reintroduces previously erased personal data, perform documented follow-up deletion.

### Evidence checklist

- [ ] Backup retention window documented (platform plan + effective retention)
- [ ] Restore procedure documented (who can run, when, approvals)
- [ ] Post-restore GDPR cleanup runbook documented
- [ ] Test/record of restore drill and cleanup validation
- [ ] Access controls for backup/restore operations reviewed

## 2) Logging controls (App Service logs, no App Insights)

### Current setup summary

- Central telemetry store: none (no App Insights)
- Operational logs: App Service/platform logs + app logs
- Policy requirement: avoid logging participant PII; keep retention and access boundaries documented.

### Evidence checklist

- [ ] App Service log retention configured and recorded
- [ ] Log access roles/groups documented
- [ ] PII redaction checks performed and dated
- [ ] Export destinations (if any) documented
- [ ] Log deletion/rotation behavior documented

## 3) Data subject rights (DSAR) operations

### DSAR register

| Request ID | Right invoked | Received | Identity verified | Systems checked | Completed | Outcome | Notes |
|---|---|---|---|---|---|---|---|
| ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ |

### DSAR evidence checklist

- [ ] Identity verification procedure documented
- [ ] One-month SLA tracking visible
- [ ] Processor escalation path documented (Azure/Auth0/Brevo/etc.)
- [ ] Refusal/extension rationale template available

## 4) Periodic compliance review

| Date | Reviewer | Scope | Findings | Actions |
|---|---|---|---|---|
| ☐ | ☐ | ☐ | ☐ | ☐ |

