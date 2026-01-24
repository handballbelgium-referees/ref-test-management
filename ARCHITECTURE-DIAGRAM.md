# BackgroundJobService Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         BackgroundJobService Flow                           │
└─────────────────────────────────────────────────────────────────────────────┘

┌───────────────────┐
│   Application     │
│   Code / GraphQL  │
│   Mutations       │
└─────────┬─────────┘
          │
          │ 1. Enqueue Job
          ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│  IJobEnqueueService                                                          │
│  ┌─────────────────────────────────────────────────────────────────────┐     │
│  │ - EnqueueInvitationEmailAsync(payload, executeAfter?)               │     │
│  │ - EnqueueResultEmailAsync(payload, executeAfter?)                   │     │
│  │ - EnqueueReportEmailAsync(payload, executeAfter?)                   │     │
│  └─────────────────────────────────────────────────────────────────────┘     │
└────────────────────────────────┬─────────────────────────────────────────────┘
                                 │
                                 │ 2. Serialize & Save to DB
                                 ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│  SQL Database - Jobs Table                                                   │
│  ┌────────────────────────────────────────────────────────────────────┐      │
│  │ Id | JobType | Payload | Status | Attempts | LockedUntil | ...     │      │
│  ├────────────────────────────────────────────────────────────────────┤      │
│  │ guid1 | InvitationEmail | {...} | Pending | 0 | null | ...         │      │
│  │ guid2 | ResultEmail     | {...} | Pending | 0 | null | ...         │      │
│  │ guid3 | ReportEmail     | {...} | Processing | 1 | 2026-... | ...  │      │
│  └────────────────────────────────────────────────────────────────────┘      │
│  Indexes:                                                                    │
│  - IX_Jobs_Status_ExecuteAfter_LockedUntil (composite)                       │
│  - IX_Jobs_CreatedAt                                                         │
└────────────────────────────────┬─────────────────────────────────────────────┘
                                 │
                                 │ 3. Poll every 5s (configurable)
                                 ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│  BackgroundJobService (Hosted Service)                                       │
│  ┌─────────────────────────────────────────────────────────────────────┐     │
│  │  Loop:                                                              │     │
│  │    1. Query: WHERE Status = Pending                                 │     │
│  │              AND ExecuteAfter <= NOW                                │     │
│  │              AND (LockedUntil IS NULL OR LockedUntil <= NOW)        │     │
│  │              ORDER BY ExecuteAfter                                  │     │
│  │              TAKE @BatchSize (default 10)                           │     │
│  │                                                                     │     │
│  │    2. For each job:                                                 │     │
│  │       a. Lock: Status = Processing, LockedUntil = NOW + 5min        │     │
│  │       b. Deserialize Payload to DTO                                 │     │
│  │       c. Process based on JobType                                   │     │
│  │       d. On Success: Status = Completed                             │     │
│  │       e. On Failure: Attempts++, Status = Failed/Pending            │     │
│  │                                                                     │     │
│  │    3. Wait 5 seconds (if no jobs) or continue                       │     │
│  └─────────────────────────────────────────────────────────────────────┘     │
└────────────────────────────────┬─────────────────────────────────────────────┘
                                 │
                                 │ 4. Process Job
                                 ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│  Job Processing (Switch on JobType)                                          │
│                                                                              │
│  ┌────────────────────┐  ┌────────────────────┐  ┌────────────────────┐      │
│  │ InvitationEmail    │  │ ResultEmail        │  │ ReportEmail        │      │
│  ├────────────────────┤  ├────────────────────┤  ├────────────────────┤      │
│  │ 1. Deserialize     │  │ 1. Deserialize     │  │ 1. Deserialize     │      │
│  │    to Invitation   │  │    to Result       │  │    to Report       │      │
│  │    EmailPayload    │  │    EmailPayload    │  │    EmailPayload    │      │
│  │                    │  │                    │  │                    │      │
│  │ 2. Call Email      │  │ 2. Get Questions   │  │ 2. Convert Data    │      │
│  │    Service         │  │    Service         │  │                    │      │
│  │    .SendRefTest    │  │                    │  │ 3. Call Report     │      │
│  │    InvitationAsync │  │ 3. Call Email      │  │    Service         │      │
│  │                    │  │    Service         │  │    .SendReport     │      │
│  │ 3. On Success:     │  │    .SendRefTest    │  │    Async           │      │
│  │    Update RefTest  │  │    ResultsAsync    │  │                    │      │
│  │    InvitationSentAt│  │                    │  │                    │      │
│  │    = DateTime.Now  │  │ 4. On Success:     │  │                    │      │
│  │                    │  │    Update RefTest  │  │                    │      │
│  │                    │  │    ResultsSentAt   │  │                    │      │
│  │                    │  │    = DateTime.Now  │  │                    │      │
│  └────────────────────┘  └────────────────────┘  └────────────────────┘      │
└────────────────────────────────┬─────────────────────────────────────────────┘
                                 │
                                 │ 5. Send Email
                                 ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│  EmailService / ReportService                                                │
│  ┌─────────────────────────────────────────────────────────────────────┐     │
│  │ - Generate email content                                            │     │
│  │ - Generate PDFs (if needed)                                         │     │
│  │ - Send via SMTP                                                     │     │
│  └─────────────────────────────────────────────────────────────────────┘     │
└──────────────────────────────────────────────────────────────────────────────┘

═══════════════════════════════════════════════════════════════════════════════

Concurrency Control (Multiple Instances)

Instance 1:                     Instance 2:
┌────────────────┐             ┌────────────────┐
│ Poll DB        │             │ Poll DB        │
└────────┬───────┘             └────────┬───────┘
         │                              │
         │ Lock Job A                   │ Try Lock Job A
         │ (LockedUntil = NOW+5min)     │ (SKIP - Locked)
         ▼                              │
┌────────────────┐                      │ Lock Job B
│ Process Job A  │                      │ (LockedUntil = NOW+5min)
└────────┬───────┘                      ▼
         │                     ┌────────────────┐
         │ Complete            │ Process Job B  │
         ▼                     └────────┬───────┘
┌────────────────┐                      │ Complete
│ Status =       │                      ▼
│ Completed      │             ┌────────────────┐
└────────────────┘             │ Status =       │
                               │ Completed      │
                               └────────────────┘

═══════════════════════════════════════════════════════════════════════════════

Retry Logic

Attempt 1:                 Attempt 2:                 Attempt 3:
┌────────────────┐        ┌────────────────┐        ┌────────────────┐
│ Process Job    │        │ Process Job    │        │ Process Job    │
│ Attempts = 0   │        │ Attempts = 1   │        │ Attempts = 2   │
└────────┬───────┘        └────────┬───────┘        └────────┬───────┘
         │                         │                         │
         │ FAIL                    │ FAIL                    │ FAIL
         ▼                         ▼                         ▼
┌────────────────┐        ┌────────────────┐        ┌────────────────┐
│ Attempts = 1   │        │ Attempts = 2   │        │ Attempts = 3   │
│ Status =       │        │ Status =       │        │ Status =       │
│   Pending      │        │   Pending      │        │   Failed       │
│ LockedUntil =  │        │ LockedUntil =  │        │   (PERMANENT)  │
│   null         │        │   null         │        │                │
│ ErrorMessage   │        │ ErrorMessage   │        │ ErrorMessage   │
│   stored       │        │   stored       │        │   stored       │
└────────────────┘        └────────────────┘        └────────────────┘
         │                         │
         │ Retry                   │ Retry
         └─────────────────────────┘

═══════════════════════════════════════════════════════════════════════════════

Scheduled Execution

Now: 10:00 AM                                          Now: 11:00 AM
┌────────────────────────────────────┐               ┌──────────────────┐
│ Job Created                        │               │ Time to Process! │
│ ExecuteAfter = 11:00 AM            │ ... Wait ...  │                  │
│ Status = Pending                   │───────────────▶ Process Job      │
│                                    │               │                  │
│ Background service sees job but    │               └──────────────────┘
│ skips it (ExecuteAfter > NOW)      │
└────────────────────────────────────┘

═══════════════════════════════════════════════════════════════════════════════

Automatic Cleanup (Every 24 Hours by Default)

┌────────────────────────────────────────────────────────────────────────────┐
│ Cleanup Process                                                            │
│                                                                            │
│ 1. Check if cleanup is due (last run + 24 hours)                           │
│    └─ If yes, continue to step 2                                           │
│                                                                            │
│ 2. Query old jobs:                                                         │
│    WHERE (Status = 'Completed' AND CompletedAt < NOW - 7 days)             │
│       OR (Status = 'Failed' AND CompletedAt < NOW - 30 days)               │
│                                                                            │
│ 3. Delete old jobs in batch                                                │
│                                                                            │
│ 4. Log cleanup results                                                     │
│                                                                            │
│ Configurable via appsettings.json:                                         │
│ - RetainCompletedJobsDays (default: 7)                                     │
│ - RetainFailedJobsDays (default: 30)                                       │
│ - CleanupIntervalHours (default: 24)                                       │
│ - EnableCleanup (default: true)                                            │
└────────────────────────────────────────────────────────────────────────────┘

═══════════════════════════════════════════════════════════════════════════════

Performance Features

✓ Indexed Queries:          Fast job lookup with composite index
✓ Batch Processing:         Process up to 10 jobs per cycle
✓ Low CPU Usage:            5-second sleep when no jobs
✓ Optimistic Locking:       LockedUntil prevents double-processing
✓ Scoped Services:          Fresh DI scope per job
✓ Async Throughout:         Non-blocking I/O operations
✓ Source-Gen Logging:       Minimal logging overhead
✓ Graceful Shutdown:        Cancellation token support
✓ Auto Cleanup:             Old jobs automatically purged (configurable retention)
✓ DateTime Tracking:        Precise timestamps when emails are delivered
```
