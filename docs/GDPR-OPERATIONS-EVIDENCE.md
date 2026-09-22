# GDPR Operations Evidence (Backups, Logging, DSAR)

This file captures operational evidence that cannot be proven from application source code alone.

## 1) Backup and restore controls (Azure SQL default backups)

### Current setup summary

- Backup mechanism: Azure SQL default automated backups
- Restore mechanism: Azure SQL point-in-time restore (platform-managed)
- Application policy requirement: if restored data reintroduces previously erased personal data, perform documented follow-up deletion.
- SQL region: **Belgium Central**.

### Evidence checklist

- [x] Backup retention window baseline documented: Azure SQL default PITR policy applies (exact configured retention period still needs portal screenshot/evidence).
- [ ] Restore procedure documented (who can run, when, approvals) — pending explicit owner list.
- [ ] Post-restore GDPR cleanup runbook documented — pending.
- [ ] Test/record of restore drill and cleanup validation — pending.
- [ ] Access controls for backup/restore operations reviewed — pending.

## 2) Logging controls (App Service logs, no App Insights)

### Current setup summary

- Central telemetry store: none (no App Insights)
- Operational logs: App Service/platform logs + app logs
- Policy requirement: avoid logging participant PII; keep retention and access boundaries documented.
- App Service region: **West Europe**.

### Evidence checklist

- [ ] App Service log retention configured and recorded — pending explicit retention values from portal.
- [ ] Log access roles/groups documented — pending explicit RBAC mapping.
- [ ] PII redaction checks performed and dated — pending dated operational check record.
- [ ] Export destinations (if any) documented — currently unknown.
- [ ] Log deletion/rotation behavior documented — pending.

## 3) Data subject rights (DSAR) operations

### DSAR register

| Request ID | Right invoked | Received | Identity verified | Systems checked | Completed | Outcome | Notes |
|---|---|---|---|---|---|---|---|
| No DSAR records logged yet | — | — | — | — | — | — | Add rows when first DSAR is received. |

### DSAR evidence checklist

- [x] Identity verification procedure documented — see `docs/GDPR-DSAR-PROCEDURE.md`.
- [x] One-month SLA tracking visible — defined in `docs/GDPR-DSAR-PROCEDURE.md`.
- [ ] Processor escalation path documented (Azure/Auth0/Brevo/etc.) — pending.
- [x] Refusal/extension rationale template available — see `docs/GDPR-DSAR-PROCEDURE.md`.

## 4) Periodic compliance review

| Date | Reviewer | Scope | Findings | Actions |
|---|---|---|---|---|
| 2026-09-22 | Kristof Gilis | Initialized operational evidence with known platform defaults and known regions. | Fill portal-backed retention/RBAC values and add DSAR procedure records. |

## Remaining inputs to fully close this file

1. Exact Azure SQL PITR retention value configured for the database.
2. Exact App Service log retention settings.
3. RBAC identities/roles allowed to access restore operations and logs.
4. Confirmation whether logs are exported to storage/SIEM and retention there.
5. DSAR process owner, verification steps, and tracking method. ✅ documented in `docs/GDPR-DSAR-PROCEDURE.md`; register tracking remains in this file.
