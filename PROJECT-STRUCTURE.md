# 📁 Project Structure

```
ref-test-management/
├── .github/workflows/                    # CI/CD Pipelines
│   ├── pr.yml                            # PR validation (lint, test, build)
│   ├── beta-release.yml                  # Beta pre-release on every push to main
│   └── stable-release.yml                # Stable release pipeline (manual, from release branch)
│
├── .husky/                               # Git Hooks
│   ├── commit-msg                        # Validates commit format with commitlint
│   └── pre-commit                        # Runs Angular build before commit
│
├── badges/                               # Auto-generated release badge images
│   ├── release.png                       # Latest stable release badge
│   └── pre-release.png                   # Latest pre-release badge
│
├── RefTestManagement.Api/                # 🔷 .NET Web API (.NET 10)
│   ├── Controllers/
│   │   └── AccountController.cs          # Authentication endpoints
│   ├── BackgroundServices/               # Background services
│   │   ├── BackgroundJobService.cs       # Job queue processor (emails, PDFs, reports, approval notifications)
│   │   ├── PermissionSyncService.cs      # Syncs Auth0 API permissions on startup
│   │   ├── RefTestExpirationService.cs   # Automatic RefTest expiration (runs every 5 min)
│   │   └── AuditLogCleanupService.cs     # Deletes audit log entries older than retention period
│   ├── Graphql/                          # Hot Chocolate GraphQL (Clean Architecture)
│   │   ├── Mutations/                    # 🔷 All GraphQL Mutations (organized by domain)
│   │   │   ├── Lifecycle/                # User test execution mutations
│   │   │   │   ├── RefTestLifecycleMutations.cs      # Start, SaveProgress, Complete
│   │   │   │   ├── SaveRefTestProgressInput.cs       # Progress input model
│   │   │   │   └── CompleteRefTestInput.cs           # Completion input model
│   │   │   ├── Creation/                 # Test creation mutations
│   │   │   │   ├── RefTestCreationMutations.cs       # CreateRefTests
│   │   │   │   ├── CreateRefTestsInput.cs            # Creation input model
│   │   │   │   └── CreateRefTestsResult.cs           # Creation result model
│   │   │   ├── Approval/                 # Approval workflow mutations
│   │   │   │   ├── RefTestApprovalMutations.cs       # ApproveRefTests, RejectRefTests
│   │   │   │   └── ApprovalModels.cs                 # Input/result models
│   │   │   ├── Email/                    # Email operation mutations
│   │   │   │   ├── RefTestEmailMutations.cs          # Send invitations/results/reports
│   │   │   │   ├── SendInvitationsInput.cs
│   │   │   │   ├── SendInvitationsResult.cs
│   │   │   │   ├── SendResultsInput.cs
│   │   │   │   ├── SendResultsResult.cs
│   │   │   │   ├── SendReportInput.cs
│   │   │   │   └── SendReportResult.cs
│   │   │   ├── Update/                   # Update operation mutations
│   │   │   │   ├── RefTestUpdateMutations.cs         # All update operations
│   │   │   │   ├── UpdateRefTestDetailsInput.cs      # With ResendInvitation flag
│   │   │   │   ├── UpdateRefTestConfigurationInput.cs
│   │   │   │   ├── ExtendRefTestTimeInput.cs
│   │   │   │   └── UpdateRefTestNotificationSettingsInput.cs
│   │   │   ├── Reset/                    # Reset & revive mutations
│   │   │   │   ├── RefTestResetMutations.cs          # ResetRefTests, ReviveExpiredRefTests
│   │   │   │   ├── ResetRefTestsInput.cs             # Input with List<Guid> RefTestIds
│   │   │   │   ├── ResetRefTestsResult.cs            # Result for reset operations
│   │   │   │   └── ReviveRefTestsResult.cs           # Result for revive operations
│   │   │   ├── Deletion/                 # Delete operation mutations
│   │   │   │   ├── RefTestDeletionMutations.cs       # DeleteRefTests
│   │   │   │   ├── DeleteRefTestsInput.cs
│   │   │   │   └── DeleteRefTestsResult.cs
│   │   │   └── Shared/                   # Shared DTOs used across mutations
│   │   │       ├── Title.cs              # Title DTO (Title Id, or string)
│   │   │       └── User.cs               # User DTO (FirstName, LastName, Email)
│   │   ├── Queries/                      # 🔷 All GraphQL Queries
│   │   │   ├── RefTestQueries.cs         # RefTest queries (list, detail, titles)
│   │   │   ├── AuditLogQueries.cs        # Audit log paginated query (requires audit-logs:view)
│   │   │   └── DataLoaders.cs            # Batch loading for N+1 optimization
│   │   ├── Subscriptions/                # 🔷 Real-time event subscriptions
│   │   │   ├── RefTestSubscriptions.cs   # Subscription definitions with authorization
│   │   │   ├── RefTestUpdatedEvents.cs   # Event payloads (incl. RefTestApproved, RefTestRejected)
│   │   │   └── RefTestTimeExtended.cs    # Time extension event payload
│   │   ├── Types/                        # 🔷 GraphQL Type Definitions
│   │   │   ├── RefTestType.cs            # RefTest GraphQL type (incl. RejectionReason field)
│   │   │   ├── RefTestTitleType.cs       # RefTestTitle GraphQL type
│   │   │   ├── QuestionType.cs           # Question GraphQL type
│   │   │   ├── AnswerType.cs             # Answer GraphQL type
│   │   │   ├── RefTestFilterType.cs      # RefTest filtering configuration
│   │   │   ├── RefTestSortType.cs        # RefTest sorting configuration
│   │   │   ├── RefTestTitleFilterType.cs # RefTestTitle filtering
│   │   │   └── RefTestTitleSortType.cs   # RefTestTitle sorting
│   │   └── ReadModels/                   # GraphQL response DTOs
│   │       ├── RefTestDto.cs             # RefTest read model (incl. RejectionReason)
│   │       ├── RefTestMappings.cs        # RefTest mapping extensions
│   │       ├── RefTestExtensions.cs      # RefTest query extensions
│   │       ├── RefTestTitleDto.cs        # RefTestTitle read model
│   │       ├── RefTestTitleMappings.cs   # RefTestTitle mapping extensions
│   │       └── AuditLogDto.cs            # Audit log entry read model
│   ├── Program.cs                        # Application entry point & DI setup
│   ├── SecurityStartup.cs                # Auth0 JWT configuration
│   ├── RefTestManagementMigrationExtensions.cs # EF Core migration runner
│   ├── appsettings.json                  # Configuration (DB, Auth0, Email, etc.)
│   └── wwwroot/                          # Angular production build (post-build)
│
├── RefTestManagement.AuditLog/           # 📋 Audit Log Library (.NET 10)
│   ├── AuditLogEntry.cs                  # Audit log entity (Id, EntityType, EntityId, Action, Changes, Actor, Timestamp)
│   ├── AuditLogEntryConfiguration.cs     # EF Core entity configuration with indexes
│   ├── AuditLogOptions.cs                # Configuration (RetentionDays, CleanupInterval, exclusions, list properties)
│   ├── AuditSaveChangesInterceptor.cs    # EF Core interceptor — captures all write operations in same transaction
│   ├── AuditLogServiceExtensions.cs      # AddAuditLogging() DI extension
│   └── RefTestManagement.AuditLog.csproj # Depends on EF Core, ASP.NET Core HTTP Abstractions
│
├── RefTestManagement.Auth0/              # 🔐 Auth0 Management API Client (.NET 10)
│   ├── Auth0ServiceExtensions.cs         # AddAuth0ManagementServices() DI extension
│   ├── Configurations/
│   │   └── Auth0ManagementConfiguration.cs  # M2M client credentials config
│   ├── Models/
│   │   └── Auth0User.cs                  # User record (Name, Email)
│   ├── Services/
│   │   ├── IAuth0ManagementService.cs    # Service interface
│   │   └── Auth0ManagementService.cs     # M2M token + role-based user discovery
│   └── RefTestManagement.Auth0.csproj    # Depends on Security project
│
├── RefTestManagement.Application/        # 🔷 Business Logic Layer (.NET 10)
│   ├── Services/
│   │   ├── IHFRulesQuestionsService.cs   # StrawberryShake client for IHF questions API
│   │   └── LanguageConfiguration.cs      # Language settings service
│   ├── GraphQL/                          # GraphQL schemas and queries for external APIs
│   │   ├── schema.graphql                # IHF Rules Questions schema
│   │   ├── schema.extensions.graphql     # Schema extensions
│   │   └── Queries/                      # GraphQL query definitions
│   │       ├── calculateScore.graphql
│   │       ├── getQuestionsById.graphql
│   │       ├── getQuestionsByNumber.graphql
│   │       ├── getQuestionsByNumbers.graphql
│   │       ├── getRandomQuestionIds.graphql
│   │       └── searchQuestionsByNumber.graphql
│   ├── Models/                           # Application models
│   │   ├── Answer.cs                     # Answer model
│   │   ├── JobPayloads.cs                # Job payload DTOs (InvitationEmail, ResultEmail, ReportEmail, RefTestExpiration, ApprovalNotificationEmail)
│   │   ├── Question.cs                   # Question model
│   │   ├── RefTestExpirationConfiguration.cs # Expiration configuration model
│   │   └── ScoreCalculation.cs           # Score calculation model
│   ├── Configurations/                   # Configuration models
│   │   ├── BackgroundJobConfiguration.cs # Job queue settings
│   │   ├── EmailConfiguration.cs         # Email/Brevo settings
│   │   ├── ReportConfiguration.cs        # Report recipient settings
│   │   └── ScoreConfiguration.cs         # Scoring rules settings
│   └── RefTestManagement.Application.csproj # Dependencies: StrawberryShake.Server
│
├── RefTestManagement.Domain/             # 🔷 Domain Layer (.NET 10)
│   ├── Jobs/                             # Job queue domain entities
│   │   ├── Job.cs                        # Job queue entity
│   │   ├── JobStatus.cs                  # Job status enum (Pending, Processing, Completed, Failed)
│   │   └── JobType.cs                    # Job type enum (incl. ApprovalNotificationEmail)
│   ├── RefTests/                         # RefTest domain entities
│   │   ├── RefTest.cs                    # RefTest aggregate root (Approve/Reject methods, RejectionReason)
│   │   ├── RefTestStatus.cs              # Status enum (incl. PendingApproval, Rejected)
│   │   └── RefTestExceptions.cs          # Domain exceptions (incl. RefTestValidationException)
│   ├── RefTestTitles/                    # RefTest title domain entities
│   │   └── RefTestTitle.cs               # RefTest title entity
│   └── RefTestManagement.Domain.csproj   # No external dependencies (pure domain)
│
├── RefTestManagement.Security/           # 🔐 Security Layer (.NET 10)
│   ├── Permissions.cs                    # All permission constants (incl. ref-tests:approve)
│   ├── TaskPermissionRequirement.cs      # Single-permission authorization requirement
│   ├── TaskPermissionHandler.cs          # Handles exact match, wildcard, superadmin bypass
│   ├── AnyTaskPermissionRequirement.cs   # OR-semantics requirement (any of N permissions)
│   ├── AnyTaskPermissionHandler.cs       # Handles OR authorization logic
│   ├── TaskAuthorizationPolicyProvider.cs # Dynamic policy provider (anyof: prefix)
│   ├── SecurityServiceExtensions.cs      # AddSecurity() DI extension method
│   └── RefTestManagement.Security.csproj # No project dependencies (standalone)
│
├── RefTestManagement.Infrastructure/     # 🔷 Infrastructure Layer (.NET 10)
│   ├── RefTestManagementContext.cs       # EF Core DbContext (incl. AuditLogs DbSet)
│   ├── Migrations/                       # Database migrations
│   ├── Configurations/                   # EF Core entity configurations
│   │   ├── RefTestConfiguration.cs       # Includes RejectionReason column
│   │   ├── RefTestTitleConfiguration.cs
│   │   └── JobConfiguration.cs           # Job queue configuration
│   ├── Logging/                          # 📝 Centralized Logging (Source-Generated)
│   │   └── ServiceLoggerMessages.cs      # Reusable logger methods (zero allocation)
│   ├── Services/                         # 🎯 External service implementations
│   │   ├── EmailService.cs               # 📧 Send emails via Brevo API (incl. SendApprovalNotificationAsync)
│   │   ├── EmailTemplateService.cs       # 🎨 Generate HTML email templates (incl. approval notification)
│   │   ├── TranslationService.cs         # 🌐 Manage all translations (incl. approval email strings)
│   │   ├── RefTestResultsPdfService.cs   # 📄 Generate PDF test results (QuestPDF)
│   │   ├── RefTestReportService.cs       # 📊 Generate Excel + PDF system reports
│   │   ├── LogoService.cs                # 🖼️ Fetch and cache application logo (singleton)
│   │   ├── JobEnqueueService.cs          # ➕ Enqueue background jobs (incl. EnqueueApprovalNotificationAsync)
│   │   ├── RefTestSessionService.cs      # 🔒 Session lock management for concurrent test-taking
│   │   └── RefTestSubscriptionService.cs # 🔔 Pub/sub for GraphQL subscriptions (incl. Approved/Rejected events)
│   └── RefTestManagement.Infrastructure.csproj # Dependencies: EF Core, QuestPDF, ClosedXML
│
├── RefTestManagement.Ui/                 # 🅰️ Angular 21 Frontend
│   ├── src/
│   │   ├── app/
│   │   │   ├── app.ts                    # Root component (header, router-outlet, footer)
│   │   │   ├── app.config.ts             # App configuration (providers, i18n, Apollo)
│   │   │   ├── app.routes.ts             # Route definitions
│   │   │   │
│   │   │   ├── auth/                     # 🔐 Authentication & Permissions Module
│   │   │   │   ├── guards/
│   │   │   │   │   ├── auth-guard.ts         # Route protection (requires login)
│   │   │   │   │   └── permission-guard.ts   # Route protection (requires permission)
│   │   │   │   ├── directives/
│   │   │   │   │   └── has-permission.directive.ts # Structural directive (*hasPermission)
│   │   │   │   ├── services/
│   │   │   │   │   ├── auth.ts               # Auth service (login, logout, user state)
│   │   │   │   │   └── permissions.ts        # PermissionsService (signal-based, cached)
│   │   │   │   └── models/
│   │   │   │       ├── user.ts               # User model
│   │   │   │       └── permissions.ts        # Permission string constants (incl. Approve)
│   │   │   │
│   │   │   ├── home/                     # 🏠 Home Page
│   │   │   │   └── home.ts               # Landing page component (incl. Audit Logs quick action)
│   │   │   │
│   │   │   ├── audit-logs/               # 📋 Audit Log
│   │   │   │   ├── audit-log-data.ts     # Signal-based service (paginated GraphQL query)
│   │   │   │   ├── list-audit-logs.ts    # List component (filters, expandable changes)
│   │   │   │   └── list-audit-logs.html  # Template (table, entity linking, list diffs)
│   │   │   │
│   │   │   ├── ref-tests/                # 📋 RefTest Management
│   │   │   │   ├── services/             # 🎯 Shared ref-tests services
│   │   │   │   │   └── ref-test-data.ts  # Data, mutations, subscriptions (incl. approve/reject)
│   │   │   │   ├── create/               # Create RefTests page
│   │   │   │   │   ├── create-ref-tests.ts
│   │   │   │   │   └── components/
│   │   │   │   │       ├── question-import-modal/
│   │   │   │   │       ├── question-search-autocomplete/
│   │   │   │   │       ├── ref-test-user-list-item/
│   │   │   │   │       ├── title-autocomplete/
│   │   │   │   │       └── user-import-modal/
│   │   │   │   ├── detail/               # RefTest detail page
│   │   │   │   │   ├── ref-test-detail.ts
│   │   │   │   │   ├── services/
│   │   │   │   │   │   ├── ref-test-detail-data.ts
│   │   │   │   │   │   └── ref-test-detail-operation-manager.ts
│   │   │   │   │   └── components/
│   │   │   │   │       ├── ref-test-detail-tab/
│   │   │   │   │       │   ├── ref-test-detail-tab.ts
│   │   │   │   │       │   └── components/
│   │   │   │   │       │       ├── dialogs/
│   │   │   │   │       │       │   ├── update-details-dialog/
│   │   │   │   │       │       │   ├── update-configuration-dialog/
│   │   │   │   │       │       │   ├── update-notification-settings-dialog/
│   │   │   │   │       │       │   ├── extend-time-dialog/
│   │   │   │   │       │       │   ├── regenerate-token-dialog/
│   │   │   │   │       │       │   ├── reset-ref-test-dialog/
│   │   │   │   │       │       │   └── revive-ref-test-dialog/
│   │   │   │   │       │       ├── details-card/
│   │   │   │   │       │       ├── scores-card/
│   │   │   │   │       │       ├── status-info-card/
│   │   │   │   │       │       ├── test-info-card/
│   │   │   │   │       │       └── timeline-card/
│   │   │   │   │       └── ref-test-questions-tab/
│   │   │   │   │           ├── ref-test-questions-tab.ts
│   │   │   │   │           └── components/
│   │   │   │   │               ├── answer-item/
│   │   │   │   │               ├── answers-summary/
│   │   │   │   │               ├── empty-questions-state/
│   │   │   │   │               └── question-card/
│   │   │   │   └── list/                 # RefTests list page
│   │   │   │       ├── list-ref-tests.ts         # Main list component
│   │   │   │       ├── services/                 # 🎯 Business Logic Services
│   │   │   │       │   ├── ref-test-filter-state.ts
│   │   │   │       │   ├── ref-test-filter-actions.ts
│   │   │   │       │   ├── ref-test-query-builder.ts
│   │   │   │       │   ├── ref-test-local-state-manager.ts
│   │   │   │       │   ├── ref-test-operation-manager.ts # Incl. approveDialog, rejectDialog
│   │   │   │       │   ├── ref-test-selection-manager.ts # Incl. PendingApproval selection helpers
│   │   │   │       │   ├── column-visibility-manager.ts
│   │   │   │       │   ├── ref-test-ui-helpers.ts
│   │   │   │       │   ├── types.ts
│   │   │   │       │   └── constants.ts
│   │   │   │       └── components/
│   │   │   │           ├── filters/
│   │   │   │           │   ├── date-range-filter/
│   │   │   │           │   ├── performance-filters/
│   │   │   │           │   ├── ref-test-filters-card/
│   │   │   │           │   ├── sorting-panel/
│   │   │   │           │   ├── status-filter-tabs/      # Incl. Pending Approval & Rejected tabs
│   │   │   │           │   └── title-filter/
│   │   │   │           ├── ref-test-display/
│   │   │   │           │   ├── ref-test-mobile-card/
│   │   │   │           │   └── ref-test-table-row/
│   │   │   │           ├── ref-test-mobile-list/
│   │   │   │           ├── ref-test-table/
│   │   │   │           ├── ref-test-list-hero/
│   │   │   │           ├── ref-test-list-toolbar/
│   │   │   │           ├── ref-test-pagination/
│   │   │   │           ├── ref-test-performance-warning/
│   │   │   │           ├── ref-test-empty-state/
│   │   │   │           ├── dialogs/                     # 💬 Modal Dialogs
│   │   │   │           │   ├── delete-ref-tests-dialog/
│   │   │   │           │   ├── send-invitations-dialog/
│   │   │   │           │   ├── send-results-dialog/
│   │   │   │           │   ├── generate-report-dialog/
│   │   │   │           │   ├── reset-ref-tests-dialog/
│   │   │   │           │   ├── revive-ref-tests-dialog/
│   │   │   │           │   ├── approve-ref-tests-dialog/ # Bulk approval confirmation
│   │   │   │           │   └── reject-ref-tests-dialog/  # Bulk rejection with required reason
│   │   │   │           ├── column-visibility-menu/
│   │   │   │           └── ref-test-actions/
│   │   │   │
│   │   │   ├── ref-test/                  # 🎯 RefTest Taking
│   │   │   │   ├── welcome/
│   │   │   │   │   └── components/
│   │   │   │   ├── take/
│   │   │   │   │   ├── take-ref-test.ts
│   │   │   │   │   ├── guards/
│   │   │   │   │   ├── state/
│   │   │   │   │   │   ├── ref-test.facade.ts
│   │   │   │   │   │   ├── ref-test.models.ts
│   │   │   │   │   │   └── ref-test.store.ts
│   │   │   │   │   └── components/
│   │   │   │   └── components/
│   │   │   │       └── ref-test-error/
│   │   │   │
│   │   │   ├── pipes/
│   │   │   │   └── translation-pipe.ts
│   │   │   │
│   │   │   ├── services/                 # 🔧 Global Services
│   │   │   │   ├── banner.ts
│   │   │   │   ├── global-error-handler.ts
│   │   │   │   ├── language-config.ts
│   │   │   │   └── pwa-update.ts
│   │   │   │
│   │   │   └── shared/
│   │   │       ├── components/
│   │   │       │   ├── banner/
│   │   │       │   ├── datepicker/
│   │   │       │   ├── datetime-picker/
│   │   │       │   ├── dialog/
│   │   │       │   └── pull-to-refresh/
│   │   │       ├── pipes/
│   │   │       └── utils/
│   │   │
│   │   ├── index.html
│   │   ├── main.ts
│   │   ├── styles.css
│   │   └── version.ts                    # Auto-generated (gitignored)
│   │
│   ├── public/
│   │   ├── i18n/                         # Translation files (en, nl, fr, de)
│   │   ├── icons/
│   │   ├── *.svg
│   │   ├── favicon.ico
│   │   ├── manifest.webmanifest
│   │   └── robots.txt
│   │
│   ├── graphql/                          # 📡 GraphQL Operations
│   │   ├── generated.ts                  # 🤖 Auto-generated TypeScript types
│   │   ├── audit-logs/                   # Audit log operations
│   │   │   └── queries/
│   │   │       └── get-audit-logs.graphql  # Paginated audit log query
│   │   ├── ref-test/                     # Single RefTest operations (test-taking)
│   │   │   ├── mutations/
│   │   │   │   ├── complete-ref-test.graphql
│   │   │   │   ├── save-ref-test-progress.graphql
│   │   │   │   └── start-ref-test.graphql
│   │   │   ├── subscriptions/
│   │   │   │   └── ref-test-time-extended.graphql
│   │   │   └── queries/
│   │   │       ├── get-ref-test-by-token.graphql
│   │   │       ├── get-results-email-delay-minutes.graphql
│   │   │       └── get-score-configuration.graphql
│   │   └── ref-tests/                    # Multiple RefTests operations (admin management)
│   │       ├── mutations/
│   │       │   ├── approve-ref-tests.graphql         # Approve pending RefTests
│   │       │   ├── create-ref-tests.graphql
│   │       │   ├── delete-ref-tests.graphql
│   │       │   ├── extend-ref-test-time.graphql
│   │       │   ├── regenerate-ref-test-token.graphql
│   │       │   ├── reject-ref-tests.graphql          # Reject with required reason
│   │       │   ├── reset-ref-tests.graphql
│   │       │   ├── revive-ref-tests.graphql
│   │       │   ├── send-invitations.graphql
│   │       │   ├── send-report.graphql
│   │       │   ├── send-results.graphql
│   │       │   ├── update-ref-test-configuration.graphql
│   │       │   ├── update-ref-test-details.graphql
│   │       │   └── update-ref-test-notification-settings.graphql
│   │       ├── subscriptions/
│   │       │   ├── ref-test-updated.graphql          # Incl. RefTestApproved, RefTestRejected
│   │       │   └── ref-tests-updated.graphql         # Incl. RefTestApproved, RefTestRejected
│   │       └── queries/
│   │           ├── get-enabled-languages.graphql
│   │           ├── get-questions-by-number.graphql
│   │           ├── get-ref-test-by-id.graphql
│   │           ├── get-ref-tests-all-counts.graphql  # Incl. pendingApproval, rejected counts
│   │           ├── get-ref-tests.graphql
│   │           ├── get-titles.graphql
│   │           └── search-questions-by-number.graphql
│   │
│   ├── scripts/
│   │   └── generate-pwa-icons.mjs
│   │
│   ├── angular.json
│   ├── codegen.ts
│   ├── ngsw-config.json
│   ├── proxy.conf.json
│   ├── package.json
│   ├── tsconfig.json
│   ├── tsconfig.app.json
│   └── tsconfig.spec.json
│
├── scripts/
│   └── generate-badges.mjs
│
├── .releaserc.json
├── ARCHITECTURE-DIAGRAM.md               # Architecture & approval workflow diagrams
├── commitlint.config.mjs
├── dotnet-tools.json
├── package.json                          # Root dependencies (semantic-release, husky)
├── renovate.json
├── RefTestManagement.sln
├── SECURITY.md                           # Security setup, permissions reference & suggested roles
└── README.md
```

## Key Directories

| Directory                                                        | Purpose                                                                       |
| ---------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| `RefTestManagement.AuditLog`                                     | Self-contained audit log library — interceptor, entity, options, DI extension |
| `RefTestManagement.Api/Graphql`                                  | GraphQL schema, queries, mutations, and type definitions                      |
| `RefTestManagement.Api/Graphql/Mutations/Approval`               | Approve/reject mutations (requires `ref-tests:approve`)                       |
| `RefTestManagement.Auth0`                                        | Auth0 Management API client — resolves approvers by permission at runtime     |
| `RefTestManagement.Application/GraphQL`                          | External GraphQL client schemas and queries (IHF Rules)                       |
| `RefTestManagement.Security`                                     | Permission constants, authorization handlers and policy provider              |
| `RefTestManagement.Infrastructure/Services`                      | PDF/Excel generation, email delivery (Brevo), approval notifications          |
| `RefTestManagement.Ui/src/app/ref-tests`                         | RefTest creation, detail view, and management UI                              |
| `RefTestManagement.Ui/src/app/ref-tests/list`                    | List view with mobile/desktop layouts, filters and operations                 |
| `RefTestManagement.Ui/src/app/ref-tests/list/services`           | Business logic services for data, filters, state and operations               |
| `RefTestManagement.Ui/src/app/ref-tests/list/components/dialogs` | All bulk-action dialogs incl. approve & reject                                |
| `RefTestManagement.Ui/src/app/audit-logs`                        | Audit log list page with filters, expandable change diffs and entity linking  |
| `RefTestManagement.Ui/src/app/auth`                              | Auth guard, permission guard, `HasPermission` directive, `PermissionsService` |
| `RefTestManagement.Ui/src/app/ref-test`                          | RefTest-taking experience (welcome, take, results)                            |
| `RefTestManagement.Ui/graphql/ref-tests/mutations`               | All management mutations incl. `approve-ref-tests` and `reject-ref-tests`     |
| `.github/workflows`                                              | CI/CD pipelines for automated testing and deployment                          |
