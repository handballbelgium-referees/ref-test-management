<p align="center">
  <img src="RefTestManagement.Ui/public/RefTest-logo.svg" alt="RefTest Logo" width="150">
</p>

# RefTest Management Platform

A comprehensive web application for managing and taking IHF (International Handball Federation) RefTests for Handball Belgium referees. Built with .NET 10 and Angular 22, this platform enables administrators to create RefTests, manage participants, send automated email invitations and results, while providing referees with an intuitive, multilingual interface to take timed RefTests.

<p>
  <a href="https://github.com/handballbelgium-referees/ref-test-management/releases/latest"><img src="badges/release.png" alt="Latest Release" height="20"></a>
  <a href="https://github.com/handballbelgium-referees/ref-test-management/releases"><img src="badges/pre-release.png" alt="Latest Pre-Release" height="20"></a>
  <a href="https://github.com/semantic-release/semantic-release"><img src="https://img.shields.io/badge/%20%20%F0%9F%93%A6%F0%9F%9A%80-semantic--release-e10079.svg" alt="semantic-release"></a>
</p>

## 📋 Table of Contents

- [Features](#-features)
- [Architecture](#-architecture)
- [Prerequisites](#-prerequisites)
- [Getting Started](#-getting-started)
- [Project Structure](#-project-structure)
- [Configuration](#-configuration)
- [Background Services](#️-background-services)
- [Security & Permissions](docs/SECURITY.md)
- [Development Workflow](#-development-workflow)
- [Deployment](#-deployment)
- [Contributing](#-contributing)
- [Versioning](#-versioning)
- [License](#-license)

## 🎯 Features

### 📝 RefTest Management

- **RefTest Creation**: Create multiple RefTests simultaneously with customizable settings
- **Question Bank Integration**: Search and import questions from the central question database
- **Randomization**: Optional random answer order per RefTest to prevent pattern memorization
- **Time Management**: Configurable time limits with auto-submit functionality
- **Automatic Expiration**: Background service automatically expires and completes tests (no extra cost on Azure)
- **Audit Log**: Domain-event–driven audit trail using a Marten-style event store. Each business operation on a `RefTest` raises a typed domain event (e.g. `RefTestApprovedEvent`, `RefTestDetailsUpdatedEvent`) that is persisted as an immutable `AuditEvent` record with stream ID, per-stream version, actor, and a JSON payload of what changed
- **Instant Scoring**: Automatic score calculation with detailed answer feedback
- **PDF Generation**: Professional PDF reports with QuestPDF for RefTest results

### 🗂️ RefTest Management

- **Advanced Filtering**: Filter RefTests by status, score range, percentage, and date ranges
- **Operations**: Send invitations, results, and delete multiple RefTests efficiently
- **Real-time Status**: Monitor RefTest completion and participant progress
- **Detail View**: Dedicated detail page with tabbed interface and real-time updates
  - **Edit Dialogs**: Inline editing for participant details, test configuration, notification settings, time extensions, and token regeneration
  - **Reset & Revive**: Soft/hard reset dialogs for test retakes and expired test revival
  - **Card Components**: Organized information display with DetailsCard, TestInfoCard, StatusInfoCard, TimelineCard, and ScoresCard
  - **Question Display**: Detailed question and answer components with visual feedback
  - **Real-time Sync**: Automatic updates via GraphQL subscriptions
- **Responsive Design**:
  - **Mobile View**: Card-based layout with touch-optimized interactions and select all functionality
  - **Desktop View**: Responsive table with sortable columns and customizable column visibility
  - **Adaptive UI**: Seamless transitions between mobile and desktop layouts
- **Performance Optimizations**:
  - Efficient count retrieval across all filters with optimized GraphQL queries
  - Cache-first policy for improved data loading performance
  - Load more functionality with pagination and maximum capacity handling
  - Performance warning banners for large result sets
- **Loading Indicators**: Visual feedback for all asynchronous operations (send, delete)
- **Report Banners**: Display success or error states for report generation operations

### 🔄 RefTest Update & Reset

- **Update Operations**: Modify RefTests with status-aware validation
  - **Update Details**: Change participant name and email (Pending/Expired only)
    - Optional `ResendInvitation` flag to send invitation to updated email
  - **Update Configuration**: Modify test title, questions, and time limits (Pending/Expired only)
  - **Extend Time**: Add additional time for in-progress tests (InProgress only)
    - **Real-Time Notifications**: GraphQL subscription pushes time extension events to test takers instantly
    - UI countdown timer recalculates automatically without page refresh
  - **Update Notifications**: Enable/disable automatic invitation and result emails
  - **Regenerate Token**: Create new access token with automatic invitation resend
- **Reset Operations**: Allow test retakes with flexible options (handles multiple tests)
  - **Soft Reset**: Clear progress while preserving audit trail (`CreatedAt`, `InvitationSentAt`)
  - **Hard Reset**: Complete fresh start with new `CreatedAt` (resets expiration timer)
  - Optional token regeneration with soft reset
  - Automatic invitation resending when token is regenerated
  - **Real-time Notifications**: Triggers subscription events on successful reset
- **Revive Operations**: Restore expired tests (handles multiple tests)
  - Reset expired tests to Pending status
  - Always regenerates token and resets expiration timer
  - Automatically resends invitations
  - **Real-time Notifications**: Triggers subscription events on successful revival
- **Approval Workflow**: Optional pre-activation review for RefTests created by users without `ref-tests:approve`
  - Tests land in **Pending Approval** status instead of Pending
  - Approvers receive an email listing all new tests with a direct link to the review queue
  - Bulk **Approve** (activates tests) or **Reject** (requires reason) from the list view
  - Rejected tests can be re-approved later; rejection reason is stored and visible to approvers
  - Approver list is resolved dynamically via the Auth0 Management API at notification time
  - **Pending Approval** and **Rejected** status tabs visible only to users with `ref-tests:approve`
- **Status-Aware Rules**: Different capabilities based on RefTest status
  - **Pending**: Full flexibility for updates and resets
  - **InProgress**: Can only extend time and update notification settings
  - **Completed**: Can only update result notification settings and reset for retake
  - **Expired**: Can update like Pending, reset, or revive with fresh timer

### 📧 Email Automation

- **Background Job Queue**: All emails processed asynchronously for **instant API responses** and automatic retry
- **Enhanced Job Management**: Improved job creation with automatic subscription notifications
  - **Real-time Updates**: Job creation triggers RefTest event subscriptions
  - **Smart Cleanup**: Automatic deletion of pending jobs when RefTests are deleted
  - **Batch Operations**: Efficient handling of multiple job deletions
- **Automated Invitations**: Optionally send RefTest invitations automatically upon RefTest creation
- **Result Notifications**: Automatically email results upon RefTest completion (with PDFs attached)
- **Automatic Retry**: Failed email jobs retry automatically (up to 3 attempts with exponential backoff)
- **Scheduled Delivery**: Support for delayed/scheduled email sending
- **Multilingual Templates**: Email templates in **English, Dutch, French, and German**
- **Personalization**: Emails include participant names and RefTest-specific details
- **Brevo Integration**: Reliable email delivery via Brevo API (formerly SendGrid)
- **Self-Cleaning**: Old email jobs automatically cleaned up from the database
- **High Performance**:
  - **Thread-safe operations** (no race conditions)
  - **Memory optimized** (75% reduction in peak memory usage)
  - **Fast processing** (~120 jobs/minute throughput)
- **Centralized Translations**: All email text managed in dedicated TranslationService (276+ translations)

### 🔐 Authentication & Security

- **Auth0 Integration**: Secure OAuth2/OpenID Connect authentication via cookie session (browser) and JWT Bearer (direct API)
- **Task-Based Permissions**: Every GraphQL operation is protected by a named permission (e.g. `ref-tests:create`). Permissions are defined in Auth0 and included in the access token
- **Superadmin Role**: The `superadmin` permission bypasses all checks
- **Namespace Wildcards**: `ref-tests:*` grants all ref-tests permissions; useful for admin roles
- **Route Guards**: Angular `permissionGuard` blocks navigation to routes the user lacks permission for
- **UI Enforcement**: `HasPermission` structural directive hides buttons and cards reactively
- **OR Permissions**: `AnyTaskPermissionHandler` + dynamic policy provider support field-level OR authorization

See [SECURITY.md](docs/SECURITY.md) for the full permission reference, Auth0 setup guide, and suggested roles.

### 🌍 Internationalization

- **4 Languages**: Full support for English, Dutch, French, and German
- **Persistent Preferences**: Language selection saved per user in local storage
- **Complete Localization**: All UI elements, emails, and PDF reports translated
- **Dynamic Switching**: Change language instantly without a page reload
- **Fallback Support**: Default to English if translation missing

### ⚡ Real-Time Features

- **GraphQL Subscriptions**: WebSocket-based real-time communication with enhanced authorization
- **RefTest Event Subscriptions**: Real-time updates for test changes, invitations, results, expirations, and time extensions
- **Automatic UI Sync**: Test list and detail views automatically update when backend changes occur
- **Time Extension Notifications**: Test takers receive instant updates when admins extend test time
- **Automatic Timer Recalculation**: UI countdown updates without page refresh
- **In-Memory Pub/Sub**: Efficient event distribution for subscriptions via RefTestSubscriptionService
- **Topic Isolation**: Each RefTest has its own subscription topic for security
- **Event Types**: `RefTestUpdated`, `RefTestInvitationSent`, `RefTestResultSent`, `RefTestExpired`, `RefTestTimeExtended`

### 🎨 User Experience

- **Banner Notification System**: Modern, non-intrusive notification system replacing toast notifications
  - **Isolated Banner Manager**: Each dialog has its own banner instance for isolated error/success messages
  - **Global Banner**: Page-level banner for application-wide notifications
  - **Dismissible Alerts**: User-controlled notification dismissal with visual feedback
  - **Success & Error States**: Clear visual distinction between success and error messages
- **Global Error Handler**: Centralized error handling with automatic banner notifications
- **Detail Page Components**: Modular card-based components for enhanced organization
  - **Question & Answer Components**: Dedicated `QuestionCard`, `AnswerItem`, and `AnswersSummary` components
  - **Info Cards**: Reusable `DetailsCard`, `TestInfoCard`, `StatusInfoCard`, `TimelineCard`, and `ScoresCard`
  - **Empty States**: Friendly empty state component when no questions exist
- **Progressive Enhancement**: Improved navigation with automatic redirection after test creation

## 🏗️ Architecture

This application follows a **clean architecture pattern** with clear separation of concerns and optimized for performance:

```
┌─────────────────────────────────────────────────┐
│         Angular 22 SPA (Frontend)               │
│  Standalone Components + Signals + i18n         │
└──────┬──────────────┬───────────────────────────┘
       │              │ GraphQL (Apollo Client)
       │              │
       │ OAuth2/OIDC  │
       │              │
┌──────▼──────┐       │
│   Auth0     │       │
│  (External) │       │
│             │       │
│ RBAC Roles  │       │
│ permissions │       │
│ claim in JWT│       │
└──────┬──────┘       │
       │              │
       │ JWT Token /  │
       │ Cookie + perm│
       │ claims       │
┌──────┴──────────────▼───────────────────────────┐
│     .NET 10 Web API (Backend)                   │
│     Hot Chocolate 16 GraphQL Server             │
│                                                 │
│   Background Services:                          │
│     • BackgroundJobService (Email Queue)        │
│     • RefTestExpirationService (Auto-expire)    │
│     • AuditLogCleanupService (Archive logs)     │
└──────────────┬─────────────────────────────────┘
               │
      ┌────────┼──────────────┬───────────────────┐
      │        │              │                   │
┌─────▼──────┐ │ ┌────────────▼──┐  ┌────────────▼────────────┐
│Application │ │ │   Domain      │  │  Infrastructure Layer   │
│   Layer    │ │ │   Models      │  │                         │
│            │ │ │               │  │ Services:               │
│• GraphQL   │ │ │• RefTest      │  │ • EmailService          │
│  Queries   │ │ │  (domain      │  │ • TemplateService       │
│• Payloads  │ │ │   events)     │  │ • TranslationSvc        │
│• Config    │ │ │• IDomainEvent │  │ • PDFService            │
│            │ │ │• Job, Status  │  │ • ReportService         │
│            │ │ └───────────────┘  │ • LogoService           │
│            │ │                    │ • JobEnqueueSvc         │
└─────┬──────┘ │                    └──────┬──────────────────┘
      │        │                           │
      │   ┌────▼──────────────┐            │
      │   │  Security Layer   │            │
      │   │                   │            │
      │   │• Permissions.cs   │            │
      │   │  (all constants)  │            │
      │   │• TaskPermission   │            │
      │   │  Handler          │            │
      │   │• AnyTaskPermission│            │
      │   │  Handler (OR)     │            │
      │   │• PolicyProvider   │            │
      │   │  (anyof: dynamic) │            │
      │   └───────────────────┘            │
      │                                    │
      │           ┌────────────────────────┼──────────────┐
      │           │                        │              │
      │     ┌─────▼──────┐    ┌────────────▼──┐   ┌──────▼────────┐
      │     │Azure SQL   │    │   Brevo       │   │  QuestPDF +   │
      │     │ Database   │    │    API        │   │  ClosedXML    │
      │     │            │    │               │   │               │
      │     │• RefTests  │    │(Email         │   │(PDF/Excel     │
      │     │• Jobs      │    │ Delivery)     │   │ Generation)   │
      │     │• Titles    │    └───────────────┘   └───────────────┘
      │     └────────────┘
      │
      │ GraphQL (StrawberryShake Client)
      │
┌─────▼──────────────────┐
│  IHF Rules Questions   │
│   External GraphQL API │
└────────────────────────┘
```

### Key Architectural Highlights

#### ✅ Clean Architecture

- **Clear separation** between Domain, Application, Infrastructure, and Security layers
- **Dependency Inversion** - Infrastructure depends on Application abstractions
- **Security isolation** - `RefTestManagement.Security` has zero dependencies on other projects
- **SOLID principles** throughout the codebase

#### 🚀 Performance Optimizations

- **Singleton services** for shared state (TranslationService, EmailTemplateService, LogoService)
- **Static caching** of email templates and translations (loaded once at startup)
- **Compiled regex** for HTML parsing (zero allocation)
- **Source-generated logging** for minimal overhead
- **Read-only dictionaries** to prevent accidental mutations
- **Sequential PDF generation** to reduce memory pressure by 75%

#### 🔐 Task-Based Authorization

- **`RefTestManagement.Security`** is a standalone class library with no project dependencies
- **Every GraphQL operation** is protected by a named permission (e.g. `ref-tests:create`)
- **`TaskPermissionHandler`** resolves exact matches, namespace wildcards (`ref-tests:*`), and superadmin bypass
- **`AnyTaskPermissionHandler`** supports OR-semantics for field-level authorization
- **`TaskAuthorizationPolicyProvider`** resolves `anyof:perm1|perm2` policy names dynamically at runtime
- **Permissions propagated via cookie** — `OnTokenValidated` copies Auth0 JWT `permissions` claims into the cookie identity
- See [SECURITY.md](docs/SECURITY.md) for full setup, permission reference, and suggested roles

#### 🔄 Background Processing

- **Asynchronous job queue** for email operations (no blocking)
- **Automatic retry** with exponential backoff (up to 3 attempts)
- **Batch processing** (configurable batch size)
- **Row-level locking** for concurrent job processing
- **Self-cleaning** old jobs (configurable retention periods)

#### 🎯 Service Responsibilities

Each service has a **single, clear responsibility**:

- **EmailService** → Send emails via Brevo API
- **EmailTemplateService** → Generate HTML email templates
- **TranslationService** → Manage all UI/email translations
- **RefTestResultsPdfService** → Generate PDF reports for test results
- **RefTestReportService** → Generate Excel/PDF system reports
- **LogoService** → Fetch and cache application logo
- **JobEnqueueService** → Enqueue background jobs
- **BackgroundJobService** → Process job queue
- **RefTestExpirationService** → Auto-expire old tests
- **RefTestSubscriptionService** → Publish real-time events via GraphQL subscriptions
- **RefTestSessionService** → Manage session locks for concurrent test-taking
- **AuditLogCleanupService** → Soft-archive audit events older than retention period
- **TaskPermissionHandler** → Enforce single-permission authorization
- **AnyTaskPermissionHandler** → Enforce OR-permission authorization
- **TaskAuthorizationPolicyProvider** → Dynamically resolve permission policies

### 🎨 Clean GraphQL Architecture

The GraphQL layer has been **completely refactored** for maximum maintainability and clarity:

#### ✅ Perfect Co-location

All mutation files are co-located with their input/output models in organized subfolders:

```
Graphql/
├── Mutations/                    # All mutations organized by domain
│   ├── Lifecycle/                (3 files) - Start, Progress, Complete + inputs
│   ├── Creation/                 (3 files) - CreateRefTests + input/output
│   ├── Approval/                 (2 files) - ApproveRefTests, RejectRefTests + models
│   ├── Email/                    (7 files) - Send invitations, results, reports
│   ├── Update/                   (5 files) - 5 update operations + inputs
│   ├── Reset/                    (4 files) - Reset & Revive operations + results
│   ├── Deletion/                 (3 files) - Delete + input/output
│   └── Shared/                   (2 files) - Shared Title and User DTOs
├── Queries/                      (2 files) - Queries + DataLoaders
├── Subscriptions/                (3 files) - Real-time event subscriptions with authorization
│   ├── RefTestSubscriptions.cs   - Main subscription resolver with access control
│   ├── RefTestUpdatedEvents.cs   - Event types for RefTestUpdated subscription
│   └── RefTestTimeExtended.cs    - Time extension event payload
├── Types/                        (8 files) - Type definitions + filters/sorts
└── ReadModels/                   - Response DTOs
```

#### ✅ Key Benefits

- **From**: 1 monolithic 860-line mutation file
- **To**: 8 organized subfolders with 29 well-organized files
- **Average file size**: ~180 lines (highly maintainable)
- **Perfect co-location**: Each mutation with its input/output models
- **Shared DTOs**: Common models in dedicated Shared folder
- **Clear namespacing**: Reflects folder structure (`.Mutations.Lifecycle`, `.Mutations.Update`, etc.)

#### ✅ Mutation Organization

| Folder        | Files | Mutations | Purpose                                                    |
| ------------- | ----- | --------- | ---------------------------------------------------------- |
| **Lifecycle** | 3     | 3         | User test execution (Start, SaveProgress, Complete)        |
| **Creation**  | 3     | 1         | Create multiple tests with question selection              |
| **Approval**  | 2     | 2         | Approve/reject tests in Pending Approval status            |
| **Email**     | 7     | 3         | Send invitations, results, and reports via email           |
| **Update**    | 5     | 5         | Update details, config, time, notifications, token         |
| **Reset**     | 4     | 2         | Reset tests for retake and revive expired tests            |
| **Deletion**  | 3     | 1         | Delete operations                                          |
| **Shared**    | 2     | -         | Shared DTOs (Title and User records) used across mutations |

#### ✅ Developer Experience

**Before:**

- ❌ 860 lines to scroll through
- ❌ Models scattered in separate folder
- ❌ Hard to find related code

**After:**

- ✅ Instant discovery: `cd Mutations/Update/` → all update operations + models
- ✅ Easy navigation: Everything where you expect it
- ✅ Clear boundaries: Each category isolated

### 🏆 Service Architecture Quality

The backend services have been **architected for excellence** with focus on performance, reliability, and maintainability:

#### ✅ Single Responsibility Principle (10/10)

Every service has ONE clear, focused responsibility. No mixed concerns, no god classes.

#### ✅ Performance Optimization (9.5/10)

- **Thread-safe operations** - Zero race conditions with proper dictionary handling
- **Memory efficient** - 75% reduction in peak memory usage for PDF generation
- **Cached resources** - Translations, templates, and logos loaded once at startup
- **Compiled regex** - Zero allocation for HTML parsing
- **Source-generated logging** - Minimal overhead logging throughout
- **Read-only APIs** - Immutable dictionaries prevent accidental mutations

#### ✅ Scalability Features

- **Singleton services** for shared state (TranslationService, EmailTemplateService)
- **Batch processing** for job queue (configurable batch sizes)
- **Row-level locking** for concurrent job processing
- **Efficient database queries** with proper indexing
- **Connection pooling** via EF Core and HttpClient factory

#### ✅ Code Quality

- **SOLID principles** throughout
- **Dependency injection** for all services
- **Clear interfaces** with well-defined contracts
- **Comprehensive logging** with structured events
- **Proper error handling** with graceful degradation

#### Performance Metrics

| Scenario                   | Memory Usage | CPU Usage | Throughput    |
| -------------------------- | ------------ | --------- | ------------- |
| Single email (no PDF)      | ~50 KB       | < 1%      | N/A           |
| Single email (with 4 PDFs) | ~2 MB        | < 5%      | N/A           |
| Job queue processing       | ~100 KB      | < 2%      | ~120 jobs/min |
| RefTest expiration check   | ~10 MB       | < 1%      | Every 5 min   |
| 100 concurrent users       | ~200 MB      | ~15%      | Good          |

**Optimization Highlights:**

- **Before optimization**: 8 MB peak memory per email with PDFs
- **After optimization**: 2 MB peak memory (75% reduction)
- **Thread safety issue**: Fixed critical dictionary mutation bug
- **API clarity**: Changed to IReadOnlyDictionary for immutability

### 📝 Centralized Logging Architecture

All services use **centralized source-generated logging** for optimal performance and consistency:

#### ServiceLoggerMessages.cs

**Single file** with **53 reusable logger methods** organized by category:

```csharp
namespace Handball.Belgium.RefTestManagement.Infrastructure.Logging;

public static partial class ServiceLoggerMessages
{
    // Generic Service Operations (3 methods)
    [LoggerMessage(LogLevel.Information, "Service {serviceName} is starting")]
    public static partial void LogServiceStarting(ILogger logger, string serviceName);

    // Email Operations (9 methods)
    [LoggerMessage(LogLevel.Information, "Sending email to {email} with subject: {subject}")]
    public static partial void LogSendingEmail(ILogger logger, string email, string subject);

    // Job Processing Operations (11 methods)
    [LoggerMessage(LogLevel.Information, "Processing job {jobId} of type {jobType}")]
    public static partial void LogProcessingJob(ILogger logger, Guid jobId, JobType jobType, ...);

    // RefTest Expiration Operations (9 methods)
    [LoggerMessage(LogLevel.Information, "Enqueued {count} RefTest expiration jobs")]
    public static partial void LogEnqueuedExpirationJobs(ILogger logger, int count);

    // ... 31 more methods across 9 categories
}
```

#### Benefits

✅ **Zero Allocation** - Source-generated logging (no runtime overhead)  
✅ **Zero Boilerplate** - No `[LoggerMessage]` attributes in services  
✅ **Perfect Consistency** - Same log format across all services  
✅ **No CA1873 Warnings** - All expressions evaluated only if logging enabled  
✅ **Type-Safe** - Compile-time validation of log messages  
✅ **Discoverable** - IntelliSense shows all 53 available methods

#### Logger Method Categories (53 Total)

1. **Generic Service Operations**: 3 methods (start, stop, error)
2. **Email Operations**: 9 methods (send, success, failure, invitations, results, reports)
3. **Job Processing**: 11 methods (enqueue, process, complete, fail, retry)
4. **Database Operations**: 3 methods (query, no results, error)
5. **PDF Generation**: 3 methods (generating, generated, error)
6. **Cleanup Operations**: 3 methods (start, complete, error)
7. **External API**: 4 methods (call, success, failure, error)
8. **Validation**: 2 methods (failed, passed)
9. **Performance Monitoring**: 2 methods (duration, slow operation)
10. **Report Service**: 1 method (no recipients)
11. **Logo Service**: 1 method (download failed)
12. **RefTest Expiration**: 9 methods (checking, auto-complete, expire, enqueue)
13. **Job Enqueue**: 2 methods (invitation, result, report jobs)

#### Usage Example

```csharp
// No boilerplate - just call centralized method
ServiceLoggerMessages.LogProcessingJob(_logger, job.Id, job.JobType, attempt, maxAttempts);
ServiceLoggerMessages.LogEmailSentSuccessfully(logger, email);
ServiceLoggerMessages.LogEnqueuedExpirationJobs(_logger, count);
```

**Performance Impact**: Zero overhead when logging is disabled. The source generator creates code that checks `logger.IsEnabled()` FIRST before evaluating any expressions.

### Tech Stack

#### Backend (.NET 10)

| Technology                | Version  | Purpose                                                        |
| ------------------------- | -------- | -------------------------------------------------------------- |
| **.NET**                  | 10.0     | Latest .NET framework for high-performance APIs                |
| **Hot Chocolate**         | 16.0.3   | GraphQL server with authorization, data loaders, and filtering |
| **Entity Framework Core** | 10.0.8   | ORM for database access with migrations                        |
| **SQL Server**            | -        | Primary data store (Azure SQL or local)                        |
| **ClosedXML**             | 0.105.0  | Excel file generation and manipulation                         |
| **QuestPDF**              | 2026.5.0 | PDF generation for RefTest results                             |
| **Auth0**                 | -        | OAuth2/OpenID Connect authentication                           |
| **Brevo API**             | -        | Email delivery service                                         |

**Project Structure:**

- `RefTestManagement.Api` - Web API, GraphQL schema, controllers
- `RefTestManagement.Application` - Business logic, services
- `RefTestManagement.Domain` - Domain entities, value objects
- `RefTestManagement.Infrastructure` - Data access, external services (PDF, Email)

#### Frontend (Angular 22)

| Technology                 | Version  | Purpose                                         |
| -------------------------- | -------- | ----------------------------------------------- |
| **Angular**                | 22.0.0   | Modern SPA framework with standalone components |
| **TypeScript**             | 6.0.3    | Strict type-checking for reliability            |
| **Signals**                | Built-in | Reactive state management                       |
| **TailwindCSS**            | 4.3.0    | Utility-first CSS framework                     |
| **Apollo Client**          | 4.1.0    | GraphQL client with caching                     |
| **GraphQL Code Generator** | 7.0.0    | Auto-generate TypeScript types from GraphQL     |
| **ngx-translate**          | 17.0.0   | i18n and localization                           |
| **Vitest**                 | 4.1.0    | Fast unit testing framework                     |
| **RxJS**                   | 7.8.2    | Reactive programming                            |

**Key Patterns:**

- Standalone components (no NgModules)
- Signal-based state management with facade/store pattern for complex components
- Reactive services for shared business logic
- OnPush change detection strategy
- Route guards for authentication
- GraphQL operations in separate `.graphql` files
- Service-oriented architecture for business logic separation
- Optimistic UI updates with local state management
- Cache-first Apollo Client policy for performance

#### DevOps & Tooling

| Tool                   | Purpose                                        |
| ---------------------- | ---------------------------------------------- |
| **GitHub Actions**     | CI/CD pipelines (PR validation, deployment)    |
| **Semantic Release**   | Automated versioning from conventional commits |
| **Commitlint**         | Enforce conventional commit format             |
| **Husky**              | Git hooks (commit-msg, pre-commit)             |
| **Azure App Service**  | Production hosting platform                    |
| **EF Core Migrations** | Database schema versioning                     |

## 📋 Prerequisites

Before you begin, ensure you have the following installed:

| Requirement    | Version            | Download                                                      |
| -------------- | ------------------ | ------------------------------------------------------------- |
| **.NET SDK**   | 10.0 or later      | [Download](https://dotnet.microsoft.com/download/dotnet/10.0) |
| **Node.js**    | 22.x or later      | [Download](https://nodejs.org/)                               |
| **npm**        | 11.7.0 or later    | Included with Node.js                                         |
| **SQL Server** | 2019+ or Azure SQL | [Download](https://www.microsoft.com/sql-server)              |
| **Git**        | Latest             | [Download](https://git-scm.com/)                              |

**Additional Accounts Required:**

- **Auth0 Account**: For authentication ([Sign up](https://auth0.com/))
- **Brevo Account** (optional): For email delivery ([Sign up](https://www.brevo.com/))
- **Azure Account** (optional): For deployment ([Sign up](https://azure.microsoft.com/))

## 🚀 Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/handballbelgium-referees/ref-test-management.git
cd ref-test-management
```

### 2. Database Setup

#### Create Database

Create a new SQL Server database (local or Azure SQL):

```sql
CREATE DATABASE RefTestManagement;
```

#### Configure Connection String

Update `RefTestManagement.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "RefTestManagement": "Server=localhost;Database=RefTestManagement;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

**For Azure SQL:**

```json
{
  "ConnectionStrings": {
    "RefTestManagement": "Server=tcp:yourserver.database.windows.net,1433;Initial Catalog=RefTestManagement;Persist Security Info=False;User ID=yourusername;Password=yourpassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;"
  }
}
```

#### Run Migrations

```bash
cd RefTestManagement.Api
dotnet ef database update
```

This creates all necessary tables, indexes, and seed data.

### 3. Configure Authentication (Auth0)

#### Set up Auth0 Application

1. Create an account at [auth0.com](https://auth0.com)
2. Create a new **Regular Web Application**
3. Configure Allowed Callback URLs: `https://localhost:7039/callback, http://localhost:4200/callback`
4. Configure Allowed Logout URLs: `https://localhost:7039, http://localhost:4200`
5. Configure Allowed Web Origins: `http://localhost:4200`

#### Update Configuration

In `RefTestManagement.Api/appsettings.json`:

```json
{
  "Auth0": {
    "Domain": "your-tenant.auth0.com",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "Audience": "your-api-identifier"
  }
}
```

**Using User Secrets (Recommended for Development):**

```bash
cd RefTestManagement.Api
dotnet user-secrets set "Auth0:Domain" "your-tenant.auth0.com"
dotnet user-secrets set "Auth0:ClientId" "your-client-id"
dotnet user-secrets set "Auth0:ClientSecret" "your-client-secret"
dotnet user-secrets set "Auth0:Audience" "your-api-identifier"
```

### 4. Configure Email Service (Optional)

#### Brevo API Setup

1. Create account at [brevo.com](https://www.brevo.com/)
2. Generate API key from Settings > API Keys
3. Configure sender email

In `RefTestManagement.Api/appsettings.json`:

```json
{
  "EmailConfiguration": {
    "BaseUrl": "https://localhost:7039",
    "BrevoApiKey": "your-brevo-api-key",
    "BrevoApiUrl": "https://api.brevo.com/v3",
    "FromEmail": "noreply@yourdomain.com",
    "FromName": "IHF RefTest"
  }
}
```

**Using User Secrets:**

```bash
dotnet user-secrets set "EmailConfiguration:BaseUrl" "https://localhost:7039"
dotnet user-secrets set "EmailConfiguration:BrevoApiKey" "your-api-key"
dotnet user-secrets set "EmailConfiguration:BrevoApiUrl" "https://api.brevo.com/v3"
dotnet user-secrets set "EmailConfiguration:FromEmail" "your-email@domain.com"
dotnet user-secrets set "EmailConfiguration:FromName" "IHF RefTest"
```

### 5. Configure Question Bank URL

If using an external question repository:

```json
{
  "RulesQuestions": {
    "Url": "https://your-question-api.com/graphql"
  }
}
```

### 6. Configure Enabled Languages (Optional)

By default, all 4 languages (English, Dutch, French, German) are enabled. To limit available languages:

```json
{
  "LanguageConfiguration": {
    "DefaultPhraseLanguage": "en",
    "EnabledLanguages": ["en", "nl"]
  }
}
```

**Supported languages:** `en` (English), `nl` (Dutch), `fr` (French), `de` (German)

The frontend will dynamically load only the enabled languages from the backend configuration, allowing you to control which languages are available in the UI without code changes.

### 7. Configure Score Requirements (Optional)

Customize scoring behavior and passing criteria:

```json
{
  "ScoreConfiguration": {
    "PassingPercentage": 80,
    "Correct": 1,
    "InCorrect": -1,
    "NotAnswered": 0,
    "NegativeScore": false,
    "PenalizeGuessingStrategy": false
  }
}
```

**Configuration options:**

- `PassingPercentage`: Percentage required to pass (default: 80)
- `Correct`: Points for each correct answer selected (default: 1)
- `InCorrect`: Points for each incorrect answer selected (default: -1)
- `NotAnswered`: Points for each correct answer NOT selected (default: 0)
- `NegativeScore`: Allow negative scores per question (default: false)
- `PenalizeGuessingStrategy`: Set score to 0 if all answers selected (default: false)

### 8. Configure Report Recipients (Optional)

To receive automated reports of completed RefTests:

```json
{
  "ReportConfiguration": {
    "RecipientEmails": ["admin1@domain.com", "admin2@domain.com"]
  }
}
```

### 9. Start the Backend

```bash
cd RefTestManagement.Api
dotnet run
```

The API will start at `https://localhost:7039`

**Verify the API:**

- GraphQL Playground: `https://localhost:7039/graphql/`
- Health Check: `https://localhost:7039/Account/IsAuthenticated`

### 10. Start the Frontend

#### Install Dependencies

```bash
cd RefTestManagement.Ui
npm install
```

#### Configure Development Proxy

The `proxy.conf.json` is pre-configured for local development:

```json
{
  "/Account": {
    "target": "https://localhost:7039",
    "secure": false,
    "changeOrigin": true,
    "headers": {
      "Connection": "Keep-Alive"
    }
  },
  "/graphql": {
    "target": "https://localhost:7039",
    "secure": false,
    "changeOrigin": true,
    "ws": true,
    "headers": {
      "Connection": "Keep-Alive"
    }
  },
  "/callback": {
    "target": "https://localhost:7039",
    "secure": false,
    "changeOrigin": true,
    "headers": {
      "Connection": "Keep-Alive"
    }
  }
}
```

#### Start Development Server

```bash
npm start
```

The application will be available at `http://localhost:4200`

### 11. Access the Application

1. Open browser to `http://localhost:4200`
2. Click **"Sign In"** in the top-right corner
3. Authenticate with Auth0
4. You'll be redirected back to the application
5. Navigate to **RefTests** to create your first RefTest

### 12. Generate GraphQL Types (If Modifying Queries)

After modifying any `.graphql` files:

```bash
cd RefTestManagement.Ui
npm run codegen
```

This regenerates TypeScript types in `graphql/generated.ts`.

## 📁 Project Structure

See [PROJECT-STRUCTURE.md](docs/PROJECT-STRUCTURE.md) for the full annotated directory tree.

**Backend projects:**

| Project                            | Role                                                                                                                                                 |
| ---------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| `RefTestManagement.Api`            | .NET 10 Web API — GraphQL schema, controllers, background services                                                                                   |
| `RefTestManagement.Auth0`          | Auth0 Management API client — resolves approvers by permission at runtime                                                                            |
| `RefTestManagement.Application`    | Business logic, StrawberryShake IHF client, job payloads, configurations                                                                             |
| `RefTestManagement.Domain`         | Core entities and domain exceptions; `RefTest` and `RefTestTitle` raise typed domain events via `IHasDomainEvents`                                   |
| `RefTestManagement.Security`       | Permission constants, authorization handlers, dynamic policy provider (no project dependencies)                                                      |
| `RefTestManagement.Infrastructure` | EF Core, email (Brevo), PDF/Excel, subscriptions, background job enqueue                                                                             |
| `RefTestManagement.AuditLog`       | Marten-style event store — `IDomainEvent`, `IDomainEventWithResolution`, `AuditEvent`, EF Core interceptor, entity name resolvers, retention options |

**Frontend:** `RefTestManagement.Ui` — Angular 22, Apollo Client, GraphQL Codegen, Tailwind CSS, PWA.

## ⚙️ Configuration

### Backend Configuration (`appsettings.json`)

Complete configuration file structure:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",

  "ConnectionStrings": {
    "RefTestManagement": "Server=localhost;Database=RefTestManagement;Trusted_Connection=True;TrustServerCertificate=True;"
  },

  "Auth0": {
    "Domain": "your-tenant.auth0.com",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "Audience": "your-api-identifier"
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
    "PassingPercentage": 80
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

  "ReportConfiguration": {
    "RecipientEmails": []
  },

  "AuditLogConfiguration": {
    "EnableCleanup": true,
    "CleanupIntervalHours": 24,
    "RetentionDays": 90
  }
}
```

### Configuration Options Explained

| Section                            | Key                              | Description                                                               | Required                                      |
| ---------------------------------- | -------------------------------- | ------------------------------------------------------------------------- | --------------------------------------------- |
| **ConnectionStrings**              | `RefTestManagement`              | SQL Server or Azure SQL connection string                                 | ✅ Yes                                        |
| **Auth0**                          | `Domain`                         | Auth0 tenant domain                                                       | ✅ Yes                                        |
|                                    | `ClientId`                       | Auth0 application client ID                                               | ✅ Yes                                        |
|                                    | `ClientSecret`                   | Auth0 application client secret                                           | ✅ Yes                                        |
|                                    | `Audience`                       | Auth0 API identifier                                                      | ✅ Yes                                        |
| **EmailConfiguration**             | `BaseUrl`                        | Base URL for email links                                                  | ✅ Yes                                        |
|                                    | `BrevoApiKey`                    | Brevo (SendGrid) API key                                                  | ✅ Yes                                        |
|                                    | `BrevoApiUrl`                    | Brevo API endpoint                                                        | ✅ Yes                                        |
|                                    | `FromEmail`                      | Sender email address                                                      | ✅ Yes                                        |
|                                    | `FromName`                       | Sender display name                                                       | ✅ Yes                                        |
|                                    | `ScheduledDelayMinutes`          | Delay in minutes for scheduled emails                                     | ⚠️ Optional (defaults to 0)                   |
| **RulesQuestions**                 | `Url`                            | External question bank GraphQL endpoint                                   | ✅ Yes                                        |
| **LanguageConfiguration**          | `DefaultPhraseLanguage`          | Default language for questions                                            | ✅ Yes                                        |
|                                    | `EnabledLanguages`               | Array of enabled UI languages (en/nl/fr/de)                               | ⚠️ Optional (defaults to all 4)               |
| **ScoreConfiguration**             | `PassingPercentage`              | Percentage required to pass a RefTest                                     | ⚠️ Optional (defaults to 80)                  |
|                                    | `Correct`                        | Points awarded for correct answer selected                                | ⚠️ Optional (defaults to 1)                   |
|                                    | `InCorrect`                      | Points for incorrect answer selected                                      | ⚠️ Optional (defaults to -1)                  |
|                                    | `NotAnswered`                    | Points for correct answer NOT selected                                    | ⚠️ Optional (defaults to 0)                   |
|                                    | `NegativeScore`                  | Allow negative scores per question                                        | ⚠️ Optional (defaults to false)               |
|                                    | `PenalizeGuessingStrategy`       | Zero score if all answers selected                                        | ⚠️ Optional (defaults to false)               |
| **RefTestExpirationConfiguration** | `ExpirationCheckIntervalMinutes` | How often to check for expired tests (minutes)                            | ⚠️ Optional (defaults to 5)                   |
|                                    | `StartupDelaySeconds`            | Delay before first expiration check (seconds)                             | ⚠️ Optional (defaults to 30)                  |
|                                    | `ExpirationIfNotStarted`         | TimeSpan for how long a test is valid if not started (format: d.hh:mm:ss) | ⚠️ Optional (defaults to 7.00:00:00 - 7 days) |
| **BackgroundJobConfiguration**     | `PollingIntervalSeconds`         | How often to poll for new jobs (seconds)                                  | ⚠️ Optional (defaults to 5)                   |
|                                    | `LockDurationMinutes`            | How long a job is locked during processing (minutes)                      | ⚠️ Optional (defaults to 5)                   |
|                                    | `MaxAttempts`                    | Maximum retry attempts for failed jobs                                    | ⚠️ Optional (defaults to 3)                   |
|                                    | `BatchSize`                      | Maximum jobs to process per cycle                                         | ⚠️ Optional (defaults to 10)                  |
|                                    | `StartupDelaySeconds`            | Delay before starting job processing (seconds)                            | ⚠️ Optional (defaults to 10)                  |
|                                    | `EnableCleanup`                  | Enable automatic cleanup of old jobs                                      | ⚠️ Optional (defaults to true)                |
|                                    | `CleanupIntervalHours`           | How often to run cleanup (hours)                                          | ⚠️ Optional (defaults to 24)                  |
|                                    | `RetainCompletedJobsDays`        | Keep successful jobs for X days                                           | ⚠️ Optional (defaults to 7)                   |
|                                    | `RetainFailedJobsDays`           | Keep failed jobs for X days                                               | ⚠️ Optional (defaults to 30)                  |
| **ReportConfiguration**            | `RecipientEmails`                | Array of emails to receive system reports                                 | ⚠️ Optional (defaults to empty)               |
| **AuditLogConfiguration**          | `EnableCleanup`                  | Enable automatic soft-archive of old audit events                         | ⚠️ Optional (defaults to true)                |
|                                    | `CleanupIntervalHours`           | How often cleanup runs (hours)                                            | ⚠️ Optional (defaults to 24)                  |
|                                    | `RetentionDays`                  | Soft-archive events older than this many days                             | ⚠️ Optional (defaults to 90)                  |

### User Secrets (Development)

For sensitive data, use .NET User Secrets instead of `appsettings.json`:

```bash
cd RefTestManagement.Api

# Database
dotnet user-secrets set "ConnectionStrings:RefTestManagement" "Server=localhost;Database=RefTestManagement;Trusted_Connection=True;TrustServerCertificate=True;"

# Auth0
dotnet user-secrets set "Auth0:Domain" "your-tenant.auth0.com"
dotnet user-secrets set "Auth0:ClientId" "your-client-id"
dotnet user-secrets set "Auth0:ClientSecret" "your-client-secret"
dotnet user-secrets set "Auth0:Audience" "your-api-identifier"

# Email Configuration
dotnet user-secrets set "EmailConfiguration:BaseUrl" "https://localhost:7039"
dotnet user-secrets set "EmailConfiguration:BrevoApiKey" "your-api-key"
dotnet user-secrets set "EmailConfiguration:BrevoApiUrl" "https://api.brevo.com/v3"
dotnet user-secrets set "EmailConfiguration:FromEmail" "noreply@domain.com"
dotnet user-secrets set "EmailConfiguration:FromName" "IHF RefTest"
dotnet user-secrets set "EmailConfiguration:ScheduledDelayMinutes" "0"

# Question Bank
dotnet user-secrets set "RulesQuestions:Url" "https://your-question-api.com/graphql"

# Language Configuration
dotnet user-secrets set "LanguageConfiguration:DefaultPhraseLanguage" "en"
dotnet user-secrets set "LanguageConfiguration:EnabledLanguages:0" "en"
dotnet user-secrets set "LanguageConfiguration:EnabledLanguages:1" "nl"
dotnet user-secrets set "LanguageConfiguration:EnabledLanguages:2" "fr"
dotnet user-secrets set "LanguageConfiguration:EnabledLanguages:3" "de"

# Score Configuration
dotnet user-secrets set "ScoreConfiguration:PassingPercentage" "80"

# RefTest Expiration Configuration
dotnet user-secrets set "RefTestExpirationConfiguration:ExpirationCheckIntervalMinutes" "5"
dotnet user-secrets set "RefTestExpirationConfiguration:StartupDelaySeconds" "30"
dotnet user-secrets set "RefTestExpirationConfiguration:ExpirationIfNotStarted" "7.00:00:00"

# Background Job Configuration
dotnet user-secrets set "BackgroundJobConfiguration:PollingIntervalSeconds" "5"
dotnet user-secrets set "BackgroundJobConfiguration:LockDurationMinutes" "5"
dotnet user-secrets set "BackgroundJobConfiguration:MaxAttempts" "3"
dotnet user-secrets set "BackgroundJobConfiguration:BatchSize" "10"
dotnet user-secrets set "BackgroundJobConfiguration:StartupDelaySeconds" "10"
dotnet user-secrets set "BackgroundJobConfiguration:EnableCleanup" "true"
dotnet user-secrets set "BackgroundJobConfiguration:CleanupIntervalHours" "24"
dotnet user-secrets set "BackgroundJobConfiguration:RetainCompletedJobsDays" "7"
dotnet user-secrets set "BackgroundJobConfiguration:RetainFailedJobsDays" "30"

# Report Configuration (for multiple recipients, use indexed keys)
dotnet user-secrets set "ReportConfiguration:RecipientEmails:0" "admin1@domain.com"
dotnet user-secrets set "ReportConfiguration:RecipientEmails:1" "admin2@domain.com"
```

### Frontend Configuration

#### Environment Files

The frontend uses auto-generated version files. Configuration is primarily through:

1. **`proxy.conf.json`** - Development API proxy
2. **`src/app/app.config.ts`** - Apollo GraphQL endpoint
3. **`src/environments/version.ts`** - Auto-generated from package.json (gitignored)

#### Translation Files

Located in `src/assets/i18n/` and `public/i18n/`:

- `en.json` - English
- `nl.json` - Dutch (Nederlands)
- `fr.json` - French (Français)
- `de.json` - German (Deutsch)

### GitHub Secrets (CI/CD)

Configure these secrets in **Settings > Secrets and variables > Actions**:

| Secret Name             | Description                       | How to Get                             |
| ----------------------- | --------------------------------- | -------------------------------------- |
| `AZURE_CLIENT_ID`       | Azure service principal client ID | From service principal creation        |
| `AZURE_TENANT_ID`       | Azure AD tenant ID                | From service principal creation        |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID             | From Azure Portal or `az account show` |
| `AZURE_WEBAPP_NAME`     | Azure App Service name            | From Azure Portal                      |

**Setup Azure OIDC Authentication (Recommended):**

1. **Create Service Principal:**
   ```bash
   az ad sp create-for-rbac --name "handball-ref-test-deploy" \\
   --role contributor \\
   --scopes /subscriptions/{subscription-id}/resourceGroups/{resource-group}
   ```

Note the `appId` (client ID), `tenant` (tenant ID) from output.

2. **Configure Federated Credentials:**

   ```bash
   az ad app federated-credential create \\
   --id {app-id} \\
   --parameters '{
   "name": "github-deploy",
   "issuer": "https://token.actions.githubusercontent.com",
   "subject": "repo:handballbelgium-referees/ref-test-management:ref:refs/heads/main",
   "audiences": ["api://AzureADTokenExchange"]
   }'
   ```

3. **Get Subscription ID:**

   ```bash
   az account show --query id -o tsv
   ```

4. **Add to GitHub Secrets:**
   - `AZURE_CLIENT_ID`: The `appId` from step 1
   - `AZURE_TENANT_ID`: The `tenant` from step 1
   - `AZURE_SUBSCRIPTION_ID`: Output from step 3
   - `AZURE_WEBAPP_NAME`: Your Azure App Service name

## ⏱️ Background Services

The application includes **three built-in background services** that run continuously within your application at **no additional cost** on Azure.

### 1. Background Job Queue Service

The `BackgroundJobService` provides a **reliable, asynchronous job queue** for email delivery and PDF generation. All email operations are processed in the background, ensuring **fast API responses** and **automatic retry** on failure.

#### How It Works

```
┌─────────────┐       ┌──────────────┐       ┌─────────────┐
│   GraphQL   │       │   Database   │       │  Background │
│  Mutation   │──1──► │   Job Queue  │◄──2───│  Job Service│
└─────────────┘       └──────────────┘       └─────────────┘
  (Immediate               (Pending)          (Polls every
   response)                                   5 seconds)
                               │
                               │ 3. Process
                               ▼
                       ┌───────────────┐
                       │  Job Handler  │
                       │               │
                       │ • Invitation  │
                       │ • Result      │
                       │ • Report      │
                       │ • Expiration  │
                       └───────┬───────┘
                               │
                     ┌─────────┴─────────┐
                     │                   │
                     ▼                   ▼
              ┌────────────┐      ┌──────────┐
              │EmailService│      │PDFService│
              └────────────┘      └──────────┘
```

The job queue system:

1. **Enqueues jobs** when GraphQL mutations are called (e.g., create RefTest, send results)
2. **Polls the database** every 5 seconds for pending jobs (configurable)
3. **Processes jobs** by type:
   - **InvitationEmail**: Sends RefTest invitation emails
   - **ResultEmail**: Generates PDFs and sends result emails
   - **ReportEmail**: Generates Excel/PDF reports and distributes to admins
   - **RefTestExpiration**: Auto-completes or marks expired RefTests
4. **Retries failed jobs** automatically (up to 3 attempts with configurable retry logic)
5. **Marks jobs complete** or failed based on outcome
6. **Auto-cleans old jobs** (completed jobs after 7 days, failed after 30 days)

#### Key Features

✅ **Asynchronous Processing** - API returns immediately, emails sent in background  
✅ **Automatic Retry** - Failed jobs retry up to 3 times with exponential backoff  
✅ **Batch Processing** - Process up to 10 jobs per cycle (configurable)  
✅ **Concurrency Safe** - Row-level locking prevents duplicate processing  
✅ **Self-Cleaning** - Automatically removes old completed/failed jobs  
✅ **Observable** - Full logging of all job state transitions  
✅ **Configurable** - All timeouts, intervals, and limits are configurable  
✅ **Thread-Safe** - Optimized for high concurrency with zero race conditions  
✅ **Memory Efficient** - PDFs generated sequentially (75% memory reduction)

#### Performance Characteristics

| Metric                 | Value                                             |
| ---------------------- | ------------------------------------------------- |
| **Polling Interval**   | 5 seconds (configurable)                          |
| **Batch Size**         | 10 jobs per cycle (configurable)                  |
| **Lock Duration**      | 5 minutes (configurable)                          |
| **Max Retry Attempts** | 3 (configurable)                                  |
| **Cleanup Interval**   | Every 24 hours (configurable)                     |
| **Job Retention**      | Completed: 7 days, Failed: 30 days (configurable) |
| **Memory per Email**   | ~2 MB (with PDFs)                                 |
| **Throughput**         | ~120 jobs/minute                                  |

#### Database Schema

The job queue uses a **single table** with optimized indexes:

```sql
CREATE TABLE Jobs (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    JobType NVARCHAR(50) NOT NULL,           -- InvitationEmail, ResultEmail, ReportEmail, RefTestExpiration
    Payload NVARCHAR(MAX) NOT NULL,          -- JSON payload
    Status NVARCHAR(50) NOT NULL,            -- Pending, Processing, Completed, Failed
    Attempts INT NOT NULL DEFAULT 0,         -- Retry counter
    ErrorMessage NVARCHAR(MAX) NULL,         -- Last error (if failed)
    CreatedAt DATETIME2 NOT NULL,            -- When job was created
    ExecuteAfter DATETIME2 NOT NULL,         -- Scheduled execution time
    LockedUntil DATETIME2 NULL,              -- Concurrency lock
    CompletedAt DATETIME2 NULL,              -- When job finished

    -- Optimized indexes for job processing
    INDEX IX_Jobs_Status_ExecuteAfter_LockedUntil (Status, ExecuteAfter, LockedUntil)
);
```

#### Configuration

Configure in `appsettings.json`:

```json
{
  "BackgroundJobConfiguration": {
    "PollingIntervalSeconds": 5, // How often to check for jobs
    "LockDurationMinutes": 5, // Job processing timeout
    "MaxAttempts": 3, // Retry limit
    "BatchSize": 10, // Jobs per cycle
    "StartupDelaySeconds": 10, // Delay before first poll
    "EnableCleanup": true, // Auto-cleanup old jobs
    "CleanupIntervalHours": 24, // How often to cleanup
    "RetainCompletedJobsDays": 7, // Keep successful jobs
    "RetainFailedJobsDays": 30 // Keep failed jobs longer for debugging
  }
}
```

#### Job Types & Payloads

**InvitationEmail**:

```json
{
  "name": "John Doe",
  "email": "john@example.com",
  "token": "abc123",
  "numberOfQuestions": 20,
  "maxTimeInMinutes": 30,
  "refTestId": "guid"
}
```

**ResultEmail**:

```json
{
  "name": "John Doe",
  "email": "john@example.com",
  "questionScore": 18,
  "answerScore": 45,
  "totalQuestions": 20,
  "answerTotal": 60,
  "percentage": 85.5,
  "selectedAnswerIds": ["id1", "id2"],
  "wrongQuestionIds": ["qid1"],
  "wrongAnswerIds": ["aid1"],
  "questionsWithCorrectAnswers": [...],
  "refTestId": "guid"
}
```

**ReportEmail**:

```json
{
  "refTests": [...],
  "recipientEmails": ["admin@example.com"]
}
```

### 2. RefTest Expiration Service (Job-Based Architecture)

The `RefTestExpirationService` uses an **optimized job-based architecture** to automatically expire and complete RefTests that have exceeded their time limit. This smart scheduler checks for expired tests and creates specific jobs only for tests that need action.

#### How It Works

```
RefTestExpirationService (Every 5 minutes)
    ↓
Query for potentially expired tests (lightweight - only 5 fields)
    ↓
Check expiration logic in-memory (fast)
    ↓
For EACH expired test: Enqueue specific job with action
    ↓
BackgroundJobService processes jobs (can run in parallel)
    ↓
Each job handles ONE RefTest:
    • AutoComplete (for in-progress tests)
    • MarkAsExpired (for pending tests)
```

**Detailed Flow:**

1. **Scheduler checks every 5 minutes** (configurable)
2. **Queries for potentially expired tests** - loads only needed data (ID, Status, timestamps)
3. **Checks expiration in-memory** - fast calculation, no heavy database operations
4. **Determines action**:
   - In-progress tests → `AutoComplete` job
   - Pending tests → `MarkAsExpired` job
5. **Enqueues specific jobs** - one job per expired test with pre-determined action
6. **BackgroundJobService processes** - with automatic retry, job history, and parallel processing

#### Key Features

✅ **Smart Job Creation** - Only creates jobs for tests that actually need action  
✅ **Memory Efficient** - 93% less memory usage (10 KB vs 150 KB)  
✅ **Parallel Processing** - Multiple expired tests processed concurrently (batch size: 10)  
✅ **Automatic Retry** - Failed jobs retry automatically (up to 3 attempts)  
✅ **Job History** - Full audit trail with one job ID per expired test  
✅ **Failure Isolation** - If one test fails, others still succeed  
✅ **Configurable Grace Period** - 7-day validity for unused invitations  
✅ **Fair Scoring** - Uses submitted answers up to expiration time  
✅ **Email Notifications** - Optionally sends results for expired tests

#### Performance Comparison

| Metric             | Old Approach       | New Approach        | Improvement          |
| ------------------ | ------------------ | ------------------- | -------------------- |
| **Memory Usage**   | 150 KB             | 10 KB               | **93% reduction**    |
| **Query Load**     | Heavy (all fields) | Light (5 fields)    | **95% reduction**    |
| **Processing**     | Sequential         | Parallel (up to 10) | **10× throughput**   |
| **Failure Impact** | All tests affected | Single test only    | **Better isolation** |
| **Job History**    | Generic            | Per-test detail     | **Full audit trail** |

#### Configuration

```json
{
  "RefTestExpirationConfiguration": {
    "ExpirationCheckIntervalMinutes": 5, // How often scheduler checks
    "StartupDelaySeconds": 30, // Delay before first check
    "ExpirationIfNotStarted": "7.00:00:00" // 7 days validity for unused invitations
  },
  "BackgroundJobConfiguration": {
    "BatchSize": 10 // Process up to 10 expired tests in parallel
  }
}
```

#### Job Types Created

**RefTestExpiration Jobs** with specific actions:

- **AutoComplete**: For in-progress tests that exceeded time limit
- **MarkAsExpired**: For pending tests that were never started

Each job includes:

- `RefTestId`: Specific test to process
- `Action`: Pre-determined action (AutoComplete or MarkAsExpired)
- Automatic retry on failure
- Full job history tracking

### Why Background Services?

#### ✅ Cost Efficiency

- **No separate infrastructure** needed (runs in your App Service)
- **No Azure Functions** required (saves $10-50/month)
- **No Service Bus** needed (saves $10/month)
- **Zero additional cost** on Azure

#### ✅ Reliability

- **Start automatically** with your application
- **Health monitoring** via application logs
- **Graceful shutdown** on app restart
- **Exception handling** prevents crashes

#### ✅ Performance

- **Optimized queries** with proper indexing
- **Batch processing** reduces database load
- **Source-generated logging** (zero allocation)
- **Thread-safe** operations throughout

#### ✅ Observability

- **Structured logging** for all operations
- **Job status tracking** in database
- **Error messages** stored for failed jobs
- **Azure Application Insights** integration

### Monitoring Background Services

Check service health via logs:

```bash
# View BackgroundJobService logs
az webapp log tail --name YourAppName --resource-group YourResourceGroup \\
| grep "BackgroundJobService"

# View RefTestExpirationService logs
az webapp log tail --name YourAppName --resource-group YourResourceGroup \\
| grep "RefTestExpirationService"
```

**Log Events to Monitor:**

- `BackgroundJobService is starting` - Service initialized
- `Found {count} jobs to process` - Jobs available
- `Successfully completed job {jobId}` - Job succeeded
- `Job {jobId} failed (attempt {attempt})` - Job failed, will retry
- `Found {count} expired tests` - Tests auto-expired
- `Auto-completed expired RefTest {refTestId}` - Test completed

### Summary

✅ **FREE** - All services run within your existing App Service, no additional resources required  
✅ **Reliable** - Start automatically with your application  
✅ **Scalable** - Handle thousands of operations efficiently  
✅ **Zero Configuration** - Work out of the box with sensible defaults  
✅ **High Performance** - Optimized queries, minimal CPU/memory usage  
✅ **Observable** - Full logging and error tracking

### 3. Audit Log Cleanup Service

The `AuditLogCleanupService` periodically **soft-archives** `AuditEvent` records older than the configured retention period by setting `IsArchived = true` (no rows are hard-deleted).

#### How It Works

On startup (after a 10-second delay) and then every `CleanupIntervalHours` hours:

1. Opens a scoped `RefTestManagementContext`
2. Calculates the cutoff date (`UtcNow - RetentionDays`)
3. Issues a single bulk `ExecuteUpdateAsync` setting `IsArchived = true` — no entities loaded into memory
4. Logs the number of archived rows

#### Configuration

```json
{
  "AuditLogConfiguration": {
    "EnableCleanup": true,
    "CleanupIntervalHours": 24,
    "RetentionDays": 90
  }
}
```

| Setting                | Default | Description                                    |
| ---------------------- | ------- | ---------------------------------------------- |
| `EnableCleanup`        | `true`  | Set to `false` to disable the cleanup service  |
| `CleanupIntervalHours` | `24`    | How often the cleanup runs (in hours)          |
| `RetentionDays`        | `90`    | Audit events older than this are soft-archived |

- **ResultEmail**: Generates result PDFs and sends them via email
- **ReportEmail**: Generates Excel-style reports and sends to admins

4. **Handles failures** with automatic retry (up to 3 attempts with exponential backoff)
5. **Cleans up** old jobs periodically to prevent database bloat

#### Key Benefits

✅ **Non-blocking operations**: GraphQL mutations return immediately without waiting for email delivery  
✅ **Automatic retries**: Failed jobs retry automatically (3 attempts max)  
✅ **Concurrency-safe**: Multiple instances can run simultaneously without job conflicts  
✅ **Scheduled delivery**: Jobs can be scheduled to execute at a future time  
✅ **Observable**: Comprehensive logging for monitoring and debugging  
✅ **Self-cleaning**: Automatically removes old completed/failed jobs  
✅ **Accurate tracking**: RefTests are marked with precise timestamps only when emails are successfully delivered

#### Email Delivery Tracking

The system tracks email delivery with precision using DateTime fields:

- **`InvitationSentAt`**: Timestamp when invitation email was successfully delivered
- **`ResultsSentAt`**: Timestamp when result email was successfully delivered

**Important:** RefTests are **not** marked as "sent" when jobs are enqueued. They are only marked with timestamps **after** the background job successfully delivers the email. This ensures:

- ✅ Accurate state: Failed jobs don't incorrectly mark emails as "sent"
- ✅ Retry-safe: Jobs can be retried without state corruption
- ✅ Auditing: Know exactly when emails were delivered, not just queued
- ✅ Analytics: Measure email delivery latency and performance

**Example:**

```
1. RefTest created (InvitationSentAt = NULL)
2. Job enqueued (InvitationSentAt = NULL)
3. Background job sends email successfully
4. RefTest updated (InvitationSentAt = 2026-01-21 10:00:05)
```

#### Configuration

Configure the job queue in `appsettings.json`:

```json
{
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
  }
}
```

| Setting                   | Description                                | Default    |
| ------------------------- | ------------------------------------------ | ---------- |
| `PollingIntervalSeconds`  | How often to check for new jobs            | 5 seconds  |
| `LockDurationMinutes`     | How long a job is locked during processing | 5 minutes  |
| `MaxAttempts`             | Maximum retry attempts for failed jobs     | 3          |
| `BatchSize`               | Maximum jobs to process per cycle          | 10         |
| `StartupDelaySeconds`     | Delay before starting job processing       | 10 seconds |
| `EnableCleanup`           | Enable automatic cleanup of old jobs       | true       |
| `CleanupIntervalHours`    | How often to run cleanup                   | 24 hours   |
| `RetainCompletedJobsDays` | Keep successful jobs for X days            | 7 days     |
| `RetainFailedJobsDays`    | Keep failed jobs for X days                | 30 days    |

#### Job Lifecycle

```
Pending → Processing → Completed (deleted after 7 days)
                    ↓
                  Failed → Retry (up to 3 attempts)
                        ↓
                  Failed (permanently, deleted after 30 days)
```

#### Monitoring

The service logs all job processing activities:

```
[Information] BackgroundJobService is starting
[Information] Found 3 jobs to process
[Information] Processing job {JobId} of type InvitationEmail (attempt 1/3)
[Information] Successfully completed job {JobId} of type InvitationEmail
[Warning] Job {JobId} of type ResultEmail failed (attempt 2/3): SMTP connection timeout
[Information] Running job cleanup - removing jobs older than: Completed=7 days, Failed=30 days
[Information] Job cleanup completed - removed 42 old jobs
```

**Database Queries:**

```sql
-- View job queue status
SELECT Status, COUNT(*) as Count
FROM Jobs
GROUP BY Status;

-- View recent failed jobs
SELECT TOP 10 Id, JobType, ErrorMessage, Attempts, CreatedAt
FROM Jobs
WHERE Status = 'Failed'
ORDER BY CreatedAt DESC;

-- Check email delivery times for RefTests
SELECT
    Email,
    CreatedAt,
    InvitationSentAt,
    CompletedAt,
    ResultsSentAt,
    DATEDIFF(SECOND, CreatedAt, InvitationSentAt) as InvitationDeliverySeconds,
    DATEDIFF(SECOND, CompletedAt, ResultsSentAt) as ResultDeliverySeconds
FROM RefTests
WHERE InvitationSentAt IS NOT NULL OR ResultsSentAt IS NOT NULL
ORDER BY CreatedAt DESC;

-- Find emails not delivered within 5 minutes
SELECT Email, CompletedAt, ResultsSentAt
FROM RefTests
WHERE CompletedAt IS NOT NULL
  AND SendResultsAutomatically = 1
  AND (ResultsSentAt IS NULL OR DATEDIFF(MINUTE, CompletedAt, ResultsSentAt) > 5)
ORDER BY CompletedAt DESC;
```

**For a detailed technical diagram of the job queue architecture, see [ARCHITECTURE-DIAGRAM.md](./docs/ARCHITECTURE-DIAGRAM.md).**

### 2. Automatic RefTest Expiration

The `RefTestExpirationService` automatically manages RefTest expiration.

#### How It Works

The service runs every 5 minutes (configurable) and:

1. **Checks for expired tests** based on:
   - **Started tests**: Expire after `MaxTimeInMinutes` from `StartedAt`
   - **Unstarted tests**: Expire after the configured `ExpirationIfNotStarted` period (default: 7 days) from creation

2. **Processes expired tests**:
   - **Pending tests** → Marked as `Expired`
   - **In-progress tests** → Automatically completed with current answers, then marked as `Completed`

3. **Sends notifications**: Result emails are enqueued in the job queue when tests are auto-completed

#### Configuration

Configure the expiration service in `appsettings.json`:

```json
{
  "RefTestExpirationConfiguration": {
    "ExpirationCheckIntervalMinutes": 5,
    "StartupDelaySeconds": 30,
    "ExpirationIfNotStarted": "7.00:00:00"
  }
}
```

| Setting                          | Description                                                           | Default             |
| -------------------------------- | --------------------------------------------------------------------- | ------------------- |
| `ExpirationCheckIntervalMinutes` | How often to check for expired tests                                  | 5 minutes           |
| `StartupDelaySeconds`            | Delay before first check (allows app to fully start)                  | 30 seconds          |
| `ExpirationIfNotStarted`         | How long a test is valid if not started (TimeSpan format: d.hh:mm:ss) | 7.00:00:00 (7 days) |

**TimeSpan Format Examples for `ExpirationIfNotStarted`:**

- `1.00:00:00` = 1 day
- `3.00:00:00` = 3 days
- `7.00:00:00` = 7 days (default)
- `0.12:00:00` = 12 hours
- `14.00:00:00` = 14 days (2 weeks)

> **Note:** This expiration period is displayed in the invitation emails sent to participants. The email automatically formats the duration appropriately (e.g., "valid for 1 day" vs "valid for 12 hours").

#### Monitoring

The service logs all activities to help you monitor expiration processing:

**Log Examples:**

```
[Information] RefTest Expiration Service is starting
[Information] Checking 3 potentially expired RefTests
[Information] Auto-completed expired RefTest {Id} for {Email}
[Information] Expired RefTest {Id} in status Pending for {Email}
[Information] Processed 2 expired RefTests: 1 expired, 1 auto-completed
[Information] RefTest Expiration Service is stopping
```

**View logs in Azure:**

- Azure Portal → App Service → Log Stream
- Application Insights → Logs → traces table
- Query: `traces | where message contains "Background"`

### Azure Deployment

✅ **FREE** - Both services run within your existing App Service, no additional resources required  
✅ **Reliable** - Start automatically with your application  
✅ **Scalable** - Handle thousands of operations efficiently  
✅ **Zero Configuration** - Work out of the box with sensible defaults  
✅ **High Performance** - Use LoggerMessage source generators for minimal overhead

## 🔄 Development Workflow

### Branching Strategy

- `main`: Default development branch, protected — pre-releases are automatically created from every push
- `release`: Stable release branch, protected — stable releases are created when `main` is promoted here
- `feat/*`: Feature branches
- `fix/*`: Bug fix branches
- `chore/*`: Maintenance branches

### Commit Convention

This project uses [Conventional Commits](https://www.conventionalcommits.org/):

```
type(scope): subject

body

footer
```

**Types:**

- `feat`: New feature (minor version bump)
- `fix`: Bug fix (patch version bump)
- `perf`: Performance improvement (patch version bump)
- `refactor`: Code refactoring (patch version bump)
- `style`: UI/styling changes (patch version bump)
- `test`: Test updates (patch version bump)
- `docs`: Documentation only (no release)
- `ci`: CI/CD changes (no release)
- `chore`: Maintenance tasks (patch version bump)

**Breaking Changes:**
Add `!` after type or `BREAKING CHANGE:` in footer for major version bump:

```
feat!: redesign authentication flow

BREAKING CHANGE: Users must re-authenticate
```

### Git Hooks

Husky enforces quality checks:

- **commit-msg**: Validates commit message format with commitlint
- **pre-commit**: Runs Angular build to catch errors early

## 🚢 Deployment

### CI/CD Pipelines (GitHub Actions)

Three workflows handle the full release lifecycle:

| Workflow               | File                 | Trigger                        | Purpose                                         |
| ---------------------- | -------------------- | ------------------------------ | ----------------------------------------------- |
| ✅ Pull Request Checks | `pr.yml`             | Pull request to `main`         | Build/lint/test validation                      |
| 🧪 Beta Pre-release    | `beta-release.yml`   | Push to `main`                 | Automated alpha pre-release + deploy to testing |
| 🚀 Stable Release      | `stable-release.yml` | Manual (from `release` branch) | Promote → stable release → deploy to production |

### Automated Deployment (GitHub Actions)

#### Beta Pre-release (automatic)

Every push to `main` automatically triggers the **🧪 Beta Pre-release** workflow:

1. **Semantic Release**: Analyzes unreleased commits and creates a `vX.Y.Z-alpha.N` tag and GitHub pre-release
2. **Build**: Builds Angular + .NET application
3. **Deploy**: Deploys to Azure App Service (testing environment)

#### Stable Release (manual)

Triggered manually via **Actions → 🚀 Stable Release → Run workflow** — must be run from the `release` branch:

1. **Verify branch**: Fails immediately if not triggered from `release`
2. **Promote**: Fast-forwards the `release` branch to `main`'s HEAD (preserving all commit SHAs)
3. **Semantic Release**: Analyzes commits on `release` and creates a `vX.Y.Z` tag and GitHub release
4. **Build**: Builds Angular + .NET application
5. **Deploy**: Deploys to Azure App Service (production environment — requires approval)
6. **Sync**: Rebases `main` onto `release` to keep histories aligned

### Manual Deployment

#### Build for Production

```bash

# Build Angular

cd RefTestManagement.Ui
npm run build

# Copy to API wwwroot

mkdir -p ../RefTestManagement.Api/wwwroot
cp -r dist/ReftestManagement.Ui/browser/* ../RefTestManagement.Api/wwwroot/

# Publish .NET

cd ../RefTestManagement.Api
dotnet publish -c Release -o ./publish
```

#### Deploy to Azure

```bash
az webapp deployment source config-zip \\
--resource-group YourResourceGroup \\
--name YourAppName \\
--src publish.zip
```

### GraphQL Code Generation

After modifying any `.graphql` files in `RefTestManagement.Ui/graphql/`:

```bash
cd RefTestManagement.Ui
npm run codegen
```

This regenerates TypeScript types in `graphql/generated.ts` based on your GraphQL schema.

### Manual Testing

1. **GraphQL Playground**: `https://localhost:7039/graphql/`
2. **Test Authentication**: Click "Sign In" and verify Auth0 redirect
3. **Create RefTest**: Navigate to RefTests > Create and test RefTest creation
4. **Take RefTest**: Use the generated token URL to take a RefTest
5. **Check Emails**: Verify invitation and result emails (if configured)

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feat/my-feature`
3. Make your changes following the coding standards
4. Commit using conventional commits: `git commit -m "feat: add new feature"`
5. Push to your fork: `git push origin feat/my-feature`
6. Open a Pull Request

### Code Style

- **Backend**: Follow [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- **Frontend**:
  - Use Prettier for formatting (configured in `package.json`)
  - Follow Angular style guide
  - Use standalone components
  - Prefer signals over observables for local state
  - Set `changeDetection: ChangeDetectionStrategy.OnPush`

### PR Guidelines

- Keep PRs focused on a single feature or fix
- Include tests for new functionality
- Update documentation as needed
- Ensure CI passes before requesting review
- Use conventional commit format in PR title

## 🔢 Versioning

This project uses [Semantic Versioning](https://semver.org/) with automated releases:

| Version Component    | Trigger                                                             | Example       |
| -------------------- | ------------------------------------------------------------------- | ------------- |
| **MAJOR** (breaking) | `feat!` or `BREAKING CHANGE:` in footer                             | 1.0.0 → 2.0.0 |
| **MINOR** (feature)  | `feat:` commit                                                      | 1.0.0 → 1.1.0 |
| **PATCH** (fix)      | `fix:`, `perf:`, `refactor:`, `style:`, `test:`, `build:`, `chore:` | 1.0.0 → 1.0.1 |

Versions are automatically determined by [semantic-release](https://github.com/semantic-release/semantic-release) based on conventional commits.

### Version Synchronization

- **Root `package.json`**: Master version managed by semantic-release
- **`RefTestManagement.Ui/package.json`**: Synced during release via `@semantic-release/exec`
- **`src/environments/version.ts`**: Auto-generated on build via `generate-version.mjs`
- **Footer Display**: Shows current version in app footer

### Release Process

#### Pre-releases (automatic)

Every push to `main` automatically triggers the **🧪 Beta Pre-release** workflow:

1. Semantic-release analyzes unreleased commits and determines the next version
2. Creates a `vX.Y.Z-alpha.N` Git tag and GitHub pre-release

#### Stable releases (manual)

To create a stable release:

1. Go to **Actions → 🚀 Stable Release → Run workflow** and select the **`release`** branch
2. Approve the deployment in the `promote` environment (required reviewers)
3. The workflow fast-forwards the `release` branch to `main`'s HEAD (preserving all commit SHAs)
4. Semantic-release runs on `release`, creates a `vX.Y.Z` tag and GitHub release, builds the application, deploys to production, and rebases `main` onto `release`

> **Important**: Never use squash or rebase merge strategies when merging into `release`. These create new commit SHAs, causing semantic-release to miscount unreleased commits and bump the version incorrectly. The `stable-release.yml` workflow uses fast-forward, which is the only correct strategy.

## 📄 License

Copyright (c) 2026 Kristof Gilis. All rights reserved.

This repository is publicly accessible for **educational and reference purposes only**. It is **not open-source** and is governed by a custom [Source Available – Educational Viewing Only License](LICENSE).

**You may:**

- Read and study the source code for personal learning

**You may NOT:**

- Use, copy, or incorporate any part of this code into your own projects
- Deploy or run this software in any production environment
- Distribute, sublicense, or sell this software
- Modify or create derivative works

For any use beyond personal study, contact [kristof.gilis@outlook.be](mailto:kristof.gilis@outlook.be).

## 👨‍💻 Authors

- **Kristof Gilis** – _Initial work and maintenance_

## 🙏 Acknowledgments

- Handball Belgium for the requirements and domain expertise
- IHF (International Handball Federation) for the rules content
- The open-source community for the amazing tools and libraries

## 💬 Support

For issues, questions, or contributions:

- **Issues**: [GitHub Issues](https://github.com/handballbelgium-referees/ref-test-management/issues)
- **Discussions**: [GitHub Discussions](https://github.com/handballbelgium-referees/ref-test-management/discussions)

**Made with ❤️ for Handball Belgium**
