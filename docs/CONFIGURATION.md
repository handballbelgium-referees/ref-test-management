# Configuration Reference

Full reference for `RefTestManagement.Api/appsettings.json`. For local development, prefer [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) over editing `appsettings.json` directly — see [Using User Secrets](#using-user-secrets) below.

## Full Example

```json
{
  "DatabaseProvider": "SqlServer",
  "ConnectionStrings": {
    "RefTestManagement": "Server=localhost;Database=RefTestManagement;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Auth0": {
    "Domain": "your-tenant.auth0.com",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "Audience": "your-api-identifier",
    "ManagementClientId": "your-m2m-client-id",
    "ManagementClientSecret": "your-m2m-client-secret"
  },
  "EmailConfiguration": {
    "BaseUrl": "https://localhost:7039",
    "BrevoApiKey": "your-brevo-api-key",
    "BrevoApiUrl": "https://api.brevo.com/v3",
    "FromEmail": "noreply@yourdomain.com",
    "FromName": "IHF RefTest",
    "ScheduledDelayMinutes": 0
  },
  "RulesQuestions": {
    "Url": "https://your-question-bank-api.com/graphql"
  },
  "LanguageConfiguration": {
    "DefaultPhraseLanguage": "en",
    "EnabledLanguages": ["en", "nl", "fr", "de"]
  },
  "ScoreConfiguration": {
    "PassingPercentage": 80,
    "Correct": 1,
    "InCorrect": -1,
    "NotAnswered": 0,
    "NegativeScore": false,
    "PenalizeGuessingStrategy": false
  },
  "ReportConfiguration": {
    "RecipientEmails": []
  },
  "PrivacyConfiguration": {
    "ControllerName": "Your Name",
    "ControllerAddress": "Your Address",
    "ContactEmail": "privacy@yourdomain.com",
    "NoticeVersion": "1.0",
    "NoticeEffectiveDate": "2026-08-03",
    "RetentionYears": 3
  },
  "RefTestExpirationConfiguration": {
    "ExpirationCheckIntervalMinutes": 5,
    "StartupDelaySeconds": 30,
    "ExpirationIfNotStarted": "7.00:00:00"
  },
  "BackgroundJobConfiguration": {
    "PollingIntervalSeconds": 5,
    "LockDurationMinutes": 5,
    "MaxAttempts": 3,
    "BatchSize": 10,
    "StartupDelaySeconds": 10,
    "EnableCleanup": true,
    "CleanupIntervalHours": 24,
    "RetainCompletedJobsDays": 7,
    "RetainFailedJobsDays": 30
  },
  "AuditLogConfiguration": {
    "EnableCleanup": true,
    "CleanupIntervalHours": 24,
    "RetentionDays": 90
  }
}
```

## Sections

### DatabaseProvider

| Key                | Description                           | Required | Default       |
| ------------------ | ------------------------------------- | -------- | ------------- |
| `DatabaseProvider` | Selects the EF Core database provider | No       | `"SqlServer"` |

Valid values:

| Value        | Provider                                        | Connection string format                                                       |
| ------------ | ----------------------------------------------- | ------------------------------------------------------------------------------ |
| `SqlServer`  | SQL Server / Azure SQL                          | `Server=...;Database=...;Trusted_Connection=True;TrustServerCertificate=True;` |
| `PostgreSQL` | PostgreSQL (via Npgsql)                         | `Host=...;Database=...;Username=...;Password=...`                              |
| `SQLite`     | SQLite                                          | `Data Source=RefTestManagement.db`                                             |
| `MySQL`      | MySQL / MariaDB (via MySql.EntityFrameworkCore) | `Server=...;Database=...;User=...;Password=...;`                               |

### ConnectionStrings

| Key                 | Description                                                       | Required |
| ------------------- | ----------------------------------------------------------------- | -------- |
| `RefTestManagement` | Database connection string (format depends on `DatabaseProvider`) | Yes      |

### Auth0

| Key                      | Description                                                              | Required |
| ------------------------ | ------------------------------------------------------------------------ | -------- |
| `Domain`                 | Auth0 tenant domain                                                      | Yes      |
| `ClientId`               | Auth0 application client ID (OIDC/JWT audience validation)               | Yes      |
| `ClientSecret`           | Auth0 application client secret                                          | Yes      |
| `Audience`               | Auth0 API identifier                                                     | Yes      |
| `ManagementClientId`     | Client ID for a Machine-to-Machine app authorized for the Management API | Yes      |
| `ManagementClientSecret` | Secret for the Management API M2M app                                    | Yes      |

The Management API credentials are used by `RefTestManagement.Auth0` to sync permissions on startup (`PermissionSyncService`) and to resolve approvers by permission for approval-workflow notifications.

### EmailConfiguration

| Key                     | Description                                  | Required | Default |
| ----------------------- | -------------------------------------------- | -------- | ------- |
| `BaseUrl`               | Base URL used to build links in emails       | Yes      | –       |
| `BrevoApiKey`           | Brevo API key                                | Yes      | –       |
| `BrevoApiUrl`           | Brevo API endpoint                           | Yes      | –       |
| `FromEmail`             | Sender email address                         | Yes      | –       |
| `FromName`              | Sender display name                          | Yes      | –       |
| `ScheduledDelayMinutes` | Delay in minutes applied to scheduled emails | No       | `0`     |

### RulesQuestions

| Key   | Description                                 | Required |
| ----- | ------------------------------------------- | -------- |
| `Url` | External IHF question-bank GraphQL endpoint | Yes      |

### LanguageConfiguration

| Key                     | Description                                         | Required | Default  |
| ----------------------- | --------------------------------------------------- | -------- | -------- |
| `DefaultPhraseLanguage` | Default language for question phrasing              | Yes      | –        |
| `EnabledLanguages`      | Array of enabled UI languages (`en`/`nl`/`fr`/`de`) | No       | all four |

### ScoreConfiguration

| Key                        | Description                                     | Default |
| -------------------------- | ----------------------------------------------- | ------- |
| `PassingPercentage`        | Percentage required to pass a RefTest           | `80`    |
| `Correct`                  | Points for each correct answer selected         | `1`     |
| `InCorrect`                | Points for each incorrect answer selected       | `-1`    |
| `NotAnswered`              | Points for each correct answer NOT selected     | `0`     |
| `NegativeScore`            | Allow a negative score per question             | `false` |
| `PenalizeGuessingStrategy` | Zero the question score if all answers selected | `false` |

### ReportConfiguration

| Key               | Description                                | Default |
| ----------------- | ------------------------------------------ | ------- |
| `RecipientEmails` | Emails to receive automated system reports | `[]`    |

### PrivacyConfiguration

| Key                   | Description                                                               | Default |
| --------------------- | ------------------------------------------------------------------------- | ------- |
| `ControllerName`      | Data controller name shown in the privacy notice                          | –       |
| `ControllerAddress`   | Data controller correspondence address                                    | –       |
| `ContactEmail`        | Privacy contact email                                                     | –       |
| `NoticeVersion`       | Current privacy-notice version string; participants must accept it        | –       |
| `NoticeEffectiveDate` | Effective date of the current notice version                              | –       |
| `RetentionYears`      | Years to retain completed/expired RefTests before automatic anonymization | `3`     |

See [docs/PRIVACY.md](PRIVACY.md) for how retention and erasure actually work.

### RefTestExpirationConfiguration

| Key                              | Description                                           | Default               |
| -------------------------------- | ----------------------------------------------------- | --------------------- |
| `ExpirationCheckIntervalMinutes` | How often to check for expired tests                  | `5`                   |
| `StartupDelaySeconds`            | Delay before the first check                          | `30`                  |
| `ExpirationIfNotStarted`         | Validity period for unused invitations (`d.hh:mm:ss`) | `7.00:00:00` (7 days) |

### BackgroundJobConfiguration

| Key                       | Description                               | Default |
| ------------------------- | ----------------------------------------- | ------- |
| `PollingIntervalSeconds`  | How often to poll for new jobs            | `5`     |
| `LockDurationMinutes`     | How long a job is locked while processing | `5`     |
| `MaxAttempts`             | Maximum retry attempts for failed jobs    | `3`     |
| `BatchSize`               | Maximum jobs processed per cycle          | `10`    |
| `StartupDelaySeconds`     | Delay before job processing starts        | `10`    |
| `EnableCleanup`           | Enable automatic cleanup of old jobs      | `true`  |
| `CleanupIntervalHours`    | How often cleanup runs                    | `24`    |
| `RetainCompletedJobsDays` | Days to keep completed jobs               | `7`     |
| `RetainFailedJobsDays`    | Days to keep failed jobs                  | `30`    |

### AuditLogConfiguration

| Key                    | Description                                         | Default |
| ---------------------- | --------------------------------------------------- | ------- |
| `EnableCleanup`        | Enable automatic redaction of old audit events      | `true`  |
| `CleanupIntervalHours` | How often cleanup runs                              | `24`    |
| `RetentionDays`        | Audit events older than this are redacted           | `90`    |

## Managing Migrations

Each provider has its own migrations project. Migrations must be generated separately per provider because the DDL (identity columns, data types, etc.) differs between databases.

### Adding migrations

When setting up a new provider for the first time, generate its initial migration. The EF CLI uses the startup project's DI to resolve the `DbContext`, so you must override both `DatabaseProvider` **and** the connection string to match the target provider — environment variables override user secrets and `appsettings.json`.

**SQL Server** (default — no env vars needed if user secrets already hold a SQL Server connection string):

```bash
dotnet ef migrations add Initial \
  --project RefTestManagement.Migrations.SqlServer \
  --startup-project RefTestManagement.Api
```

**PostgreSQL:**

```bash
DatabaseProvider=PostgreSQL \
ConnectionStrings__RefTestManagement="Host=localhost;Database=RefTestManagement;Username=postgres;Password=postgres" \
dotnet ef migrations add Initial \
  --project RefTestManagement.Migrations.PostgreSQL \
  --startup-project RefTestManagement.Api
```

**SQLite:**

```bash
DatabaseProvider=SQLite \
ConnectionStrings__RefTestManagement="Data Source=RefTestManagement.db" \
dotnet ef migrations add Initial \
  --project RefTestManagement.Migrations.SQLite \
  --startup-project RefTestManagement.Api
```

**MySQL:**

```bash
DatabaseProvider=MySQL \
ConnectionStrings__RefTestManagement="Server=localhost;Database=RefTestManagement;User=root;Password=root;" \
dotnet ef migrations add Initial \
  --project RefTestManagement.Migrations.MySQL \
  --startup-project RefTestManagement.Api
```

> `ConnectionStrings__RefTestManagement` uses double underscores (`__`) because that is how .NET maps environment variables to nested configuration keys (`ConnectionStrings:RefTestManagement`).

### Applying migrations

Migrations are applied automatically on startup via `MigrateAsync()`. No manual `dotnet ef database update` is needed in production.

For local development you can still run it manually:

```bash
DatabaseProvider=PostgreSQL \
ConnectionStrings__RefTestManagement="Host=localhost;Database=RefTestManagement;Username=postgres;Password=postgres" \
dotnet ef database update \
  --project RefTestManagement.Migrations.PostgreSQL \
  --startup-project RefTestManagement.Api
```

### Listing applied migrations

```bash
# SQL Server (no env vars needed if user secrets are set)
dotnet ef migrations list \
  --project RefTestManagement.Migrations.SqlServer \
  --startup-project RefTestManagement.Api

# Other providers — add the same env var pair as shown above
```

## Using User Secrets

```bash
cd RefTestManagement.Api

dotnet user-secrets set "ConnectionStrings:RefTestManagement" "Server=localhost;Database=RefTestManagement;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet user-secrets set "Auth0:Domain" "your-tenant.auth0.com"
dotnet user-secrets set "Auth0:ClientId" "your-client-id"
dotnet user-secrets set "Auth0:ClientSecret" "your-client-secret"
dotnet user-secrets set "Auth0:Audience" "your-api-identifier"
dotnet user-secrets set "Auth0:ManagementClientId" "your-m2m-client-id"
dotnet user-secrets set "Auth0:ManagementClientSecret" "your-m2m-client-secret"
dotnet user-secrets set "EmailConfiguration:BrevoApiKey" "your-brevo-api-key"
```

Any key from the sections above can be set the same way, using `:` to nest sections and array indices (e.g. `LanguageConfiguration:EnabledLanguages:0`).
