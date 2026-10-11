# Architecture Diagrams

Detailed flow diagrams for the background services and authorization system referenced from the [README](../README.md#background-services).

## Background Services Overview

In `Hosting:Mode=All`, the five hosted services run in the API process. In split mode the API
serves HTTP while the Worker process runs them. The SQL-backed job queue is durable in both
topologies; split mode also requires shared Redis for subscriptions, participant session locks, and
privacy challenge limits.

```mermaid
flowchart LR
    subgraph Hosted["Hosted background services"]
        BJS["BackgroundJobService\npoll every 5s"]
        RES["RefTestExpirationService\nevery 5 min"]
        PRS["PrivacyRetentionService\ndaily"]
        ALCS["AuditLogCleanupService\nevery 24h"]
        PSS["PermissionSyncService\nafter startup (background)"]
    end

    Jobs[("Jobs table")]
    RefTests[("RefTests table")]
    AuditEvents[("AuditEvents table")]
    Auth0API[("Auth0 API resource")]

    BJS -->|processes| Jobs
    RES -->|enqueues jobs into| Jobs
    PRS -->|anonymizes| RefTests
    ALCS -->|soft-archives| AuditEvents
    PSS -->|syncs permission list to| Auth0API
```

## Container hosting topologies

`Hosting:Mode` and `State:Mode` are provider-neutral. Azure maps them to Container Apps; Cloudflare
maps them to a Worker and Durable Object Containers. `All` keeps API and background services
together. Split mode runs the API and Worker separately and requires shared state.

```mermaid
flowchart LR
    Client["Browser / SPA"]
    subgraph Azure["Azure Container Apps"]
        AzureIngress["HTTPS ingress"]
        AzureApi["API container\nAll or Api"]
        AzureWorker["Worker container\nsplit only"]
        AzureMigration["Manual migration job"]
        AzureSql[("Azure SQL\nEntra-only")]
        AzureRedis[("Managed Redis\nsplit only")]
        AzureIngress --> AzureApi
        AzureApi --> AzureSql
        AzureWorker --> AzureSql
        AzureMigration --> AzureSql
        AzureApi --> AzureRedis
        AzureWorker --> AzureRedis
    end
    subgraph Cloudflare["Cloudflare Workers and Containers"]
        Domain["Existing active-zone hostname"]
        FrontDoor["Worker front door\none-minute scheduled wake"]
        ApiContainer["AppContainer\nAPI All or Api"]
        WorkerContainer["WorkerContainer\nsplit only"]
        Postgres[("External PostgreSQL")]
        Redis[("External TLS Redis\nsplit only")]
        Domain --> FrontDoor
        FrontDoor --> ApiContainer
        FrontDoor -.->|Cron wakes All| ApiContainer
        FrontDoor -.->|Cron wakes Split| WorkerContainer
        ApiContainer --> Postgres
        WorkerContainer --> Postgres
        ApiContainer --> Redis
        WorkerContainer --> Redis
    end
    Client --> AzureIngress
    Client --> Domain
```

Azure and Cloudflare are alternative hosting targets, not a single shared deployment. Azure
Terraform owns Azure infrastructure while GitHub release workflows own image rollout. Cloudflare
Terraform owns R2 state and the custom-domain binding; Wrangler owns Worker code, Container images,
Cron, and runtime secrets. See [the deployment guide](DEPLOYMENT.md).

## Job Queue Flow (`BackgroundJobService`)

```mermaid
sequenceDiagram
    participant M as GraphQL Mutation
    participant Q as IJobEnqueueService
    participant DB as Jobs table
    participant W as BackgroundJobService
    participant E as EmailService / PDF

    M->>Q: EnqueueInvitationEmailAsync(payload)
    Q->>DB: INSERT Job (Status = Pending)
    Note over M: API returns immediately

    loop every 5s (configurable)
        W->>DB: SELECT TOP N WHERE Status = Pending AND ExecuteAfter <= now
        DB-->>W: pending jobs (batch size 10)
        W->>DB: UPDATE Status = Processing, LockedUntil = now + 5min
        W->>E: send email / generate PDF
        alt success
            W->>DB: UPDATE Status = Completed
        else failure
            W->>DB: UPDATE Attempts += 1, Status = Pending or Failed
        end
    end
```

### Job Status Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Pending
    Pending --> Processing: picked up by poll
    Processing --> Completed: success
    Processing --> Pending: failure, attempts < max (3)
    Processing --> Failed: failure, attempts = max
    Completed --> [*]: cleaned up after retention (7 days)
    Failed --> [*]: cleaned up after retention (30 days)
```

## Authentication & Authorization Flow

```mermaid
sequenceDiagram
    participant SPA as Angular SPA
    participant API as .NET API
    participant Auth0 as Auth0

    SPA->>API: GET /Account/Login
    API->>Auth0: OIDC challenge (redirect)
    Auth0-->>API: authorization code
    API->>Auth0: exchange code for tokens
    Auth0-->>API: JWT access token (permissions[] claim)
    API->>API: OnTokenValidated copies permissions[] into cookie identity
    API-->>SPA: redirect, auth cookie set

    SPA->>API: GraphQL request (cookie or Bearer token)
    API->>API: [Authorize(Policy="ref-tests:create")] or "anyof:a|b"
    API->>API: TaskAuthorizationPolicyProvider resolves the policy
    API->>API: TaskPermissionHandler / AnyTaskPermissionHandler evaluates claims
    Note over API: superadmin bypass, exact match, or namespace wildcard (ref-tests:*)
    API-->>SPA: 200 OK or 403 Forbidden
```

See [SECURITY.md](SECURITY.md) for the full permission reference and Auth0 setup.

## Approval Workflow

```mermaid
sequenceDiagram
    participant C as Creator (no ref-tests:approve)
    participant API as .NET API
    participant Job as BackgroundJobService
    participant Auth0 as Auth0 Management API
    participant Ap as Approver

    C->>API: createRefTests mutation
    API->>API: RefTest.Status = PendingApproval
    API->>Job: enqueue ApprovalNotificationEmail job
    Job->>Auth0: GetUsersWithPermissionAsync("ref-tests:approve")
    Auth0-->>Job: approver list
    Job->>Ap: email with link to the review queue

    alt approve
        Ap->>API: approveRefTests mutation
        API->>API: Status: PendingApproval -> Pending
    else reject
        Ap->>API: rejectRefTests mutation (reason required)
        API->>API: Status: PendingApproval -> Rejected
    end
    API-->>Ap: RefTestApproved / RefTestRejected subscription event
```

```mermaid
stateDiagram-v2
    [*] --> PendingApproval
    PendingApproval --> Pending: Approve()
    PendingApproval --> Rejected: Reject(reason)
    Rejected --> Pending: Approve() (re-approve)
```
