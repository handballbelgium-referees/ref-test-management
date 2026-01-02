# RefTest Management Platform

A comprehensive web application for managing and taking IHF (International Handball Federation) reftests for Handball Belgium referees. Built with .NET 10 and Angular 21, this platform enables administrators to create quiz sessions, manage participants, send automated email invitations and results, while providing referees with an intuitive, multilingual interface to take timed reftests.

[![Latest Release](https://img.shields.io/github/v/release/handballbelgium/ruletests-quiz-management?label=release)](https://github.com/handballbelgium/ruletests-quiz-management/releases)
[![Latest Pre-Release](https://img.shields.io/github/v/release/handballbelgium/ruletests-quiz-management?include_prereleases&label=pre-release)](https://github.com/handballbelgium/ruletests-quiz-management/releases)
[![semantic-release](https://img.shields.io/badge/%20%20%F0%9F%93%A6%F0%9F%9A%80-semantic--release-e10079.svg)](https://github.com/semantic-release/semantic-release)

## 📋 Table of Contents

- [Features](#-features)
- [Architecture](#-architecture)
- [Prerequisites](#-prerequisites)
- [Getting Started](#-getting-started)
- [Project Structure](#-project-structure)
- [Configuration](#-configuration)
- [Development Workflow](#-development-workflow)
- [Deployment](#-deployment)
- [Testing](#-testing)
- [Contributing](#-contributing)
- [Versioning](#-versioning)
- [License](#-license)

## 🎯 Features

### 📝 RefTest Management

- **Bulk Session Creation**: Create multiple reftests simultaneously with customizable settings
- **Question Bank Integration**: Search and bulk import questions from the central question database
- **Randomization**: Optional random answer order per session to prevent pattern memorization
- **Time Management**: Configurable time limits with auto-submit functionality
- **Instant Scoring**: Automatic score calculation with detailed answer feedback
- **PDF Generation**: Professional PDF reports with QuestPDF for reftest results

### 🗂️ Session Management

- **Advanced Filtering**: Filter sessions by status, score range, percentage, and date ranges
- **Bulk Operations**: Send invitations, results, and delete multiple sessions efficiently
- **Real-time Status**: Monitor session completion and participant progress
- **Responsive Design**: Optimized mobile and desktop views with customizable column visibility
- **Loading Indicators**: Visual feedback for all asynchronous operations (send, delete)

### 📧 Email Automation

- **Automated Invitations**: Optionally send reftest invitations automatically upon session creation
- **Result Notifications**: Automatically email results upon reftest completion
- **Multilingual Templates**: Email templates in English, Dutch, French, and German
- **Personalization**: Emails include participant names and session-specific details
- **Brevo Integration**: Reliable email delivery via Brevo API (formerly SendGrid)

### 🔐 Authentication & Security

- **Auth0 Integration**: Secure OAuth2/OpenID Connect authentication
- **JWT Authorization**: Token-based API access control
- **Protected Routes**: Angular guards for authenticated-only pages
- **User Management**: Profile display with user initials

### 🌍 Internationalization

- **4 Languages**: Full support for English, Dutch, French, and German
- **Persistent Preferences**: Language selection saved per user in local storage
- **Complete Localization**: All UI elements, emails, and PDF reports translated
- **Dynamic Switching**: Change language instantly without page reload
- **Fallback Support**: Default to English if translation missing

## 🏗️ Architecture

This application follows a clean architecture pattern with clear separation of concerns:

```
┌─────────────────────────────────────────┐
│         Angular 21 SPA (Frontend)        │
│  Standalone Components + Signals + i18n │
└──────────────────┬──────────────────────┘
                   │ GraphQL (Apollo Client)
┌──────────────────▼──────────────────────┐
│     .NET 10 Web API (Backend)            │
│     Hot Chocolate 15 GraphQL Server      │
└──────────────────┬──────────────────────┘
                   │
      ┌────────────┼────────────┐
      │            │            │
┌─────▼─────┐ ┌───▼───┐  ┌────▼─────┐
│Application│ │Domain │  │Infrastructure│
│  Layer    │ │ Models│  │   Layer      │
└───────────┘ └───────┘  └────┬─────────┘
                               │
                    ┌──────────┼──────────┐
                    │          │          │
              ┌─────▼────┐ ┌──▼───┐ ┌───▼────┐
              │Azure SQL │ │Brevo │ │QuestPDF│
              │ Database │ │ API  │ │        │
              └──────────┘ └──────┘ └────────┘
```

### Tech Stack

#### Backend (.NET 10)

| Technology                | Version   | Purpose                                                        |
| ------------------------- | --------- | -------------------------------------------------------------- |
| **.NET**                  | 10.0      | Latest .NET framework for high-performance APIs                |
| **Hot Chocolate**         | 15.1.11   | GraphQL server with authorization, data loaders, and filtering |
| **Entity Framework Core** | 10.0.0    | ORM for database access with migrations                        |
| **SQL Server**            | -         | Primary data store (Azure SQL or local)                        |
| **QuestPDF**              | 2025.12.0 | PDF generation for reftest results                             |
| **Auth0**                 | -         | OAuth2/OpenID Connect authentication                           |
| **Brevo API**             | -         | Email delivery service                                         |

**Project Structure:**

- `QuizManagement.Api` - Web API, GraphQL schema, controllers
- `QuizManagement.Application` - Business logic, services
- `QuizManagement.Domain` - Domain entities, value objects
- `QuizManagement.Infrastructure` - Data access, external services (PDF, Email)

#### Frontend (Angular 21)

| Technology                 | Version  | Purpose                                         |
| -------------------------- | -------- | ----------------------------------------------- |
| **Angular**                | 21.0.0   | Modern SPA framework with standalone components |
| **TypeScript**             | 5.9.2    | Strict type-checking for reliability            |
| **Signals**                | Built-in | Reactive state management                       |
| **TailwindCSS**            | 4.1.12   | Utility-first CSS framework                     |
| **Apollo Client**          | 4.0.1    | GraphQL client with caching                     |
| **GraphQL Code Generator** | 6.1.0    | Auto-generate TypeScript types from GraphQL     |
| **ngx-translate**          | 17.0.0   | i18n and localization                           |
| **Vitest**                 | 4.0.8    | Fast unit testing framework                     |
| **RxJS**                   | 7.8.0    | Reactive programming                            |

**Key Patterns:**

- Standalone components (no NgModules)
- Signal-based state management
- OnPush change detection strategy
- Route guards for authentication
- GraphQL operations in separate `.graphql` files

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
git clone https://github.com/KristofGilis/handball-belgium-rules-quiz.git
cd handball-belgium-rules-quiz
```

### 2. Database Setup

#### Create Database

Create a new SQL Server database (local or Azure SQL):

```sql
CREATE DATABASE QuizManagement;
```

#### Configure Connection String

Update `QuizManagement.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "QuizManagement": "Server=localhost;Database=QuizManagement;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

**For Azure SQL:**

```json
{
  "ConnectionStrings": {
    "QuizManagement": "Server=tcp:yourserver.database.windows.net,1433;Initial Catalog=QuizManagement;Persist Security Info=False;User ID=yourusername;Password=yourpassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;"
  }
}
```

#### Run Migrations

```bash
cd QuizManagement.Api
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

In `QuizManagement.Api/appsettings.json`:

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
cd QuizManagement.Api
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

In `QuizManagement.Api/appsettings.json`:

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
dotnet user-secrets set "EmailConfiguration:BrevoApiKey" "your-api-key"
dotnet user-secrets set "EmailConfiguration:FromEmail" "your-email@domain.com"
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

### 7. Start the Backend

```bash
cd QuizManagement.Api
dotnet run
```

The API will start at `https://localhost:7039`

**Verify the API:**

- GraphQL Playground: `https://localhost:7039/graphql/`
- Health Check: `https://localhost:7039/Account/IsAuthenticated`

### 8. Start the Frontend

#### Install Dependencies

```bash
cd QuizManagement.Ui
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

### 9. Access the Application

1. Open browser to `http://localhost:4200`
2. Click **"Sign In"** in the top-right corner
3. Authenticate with Auth0
4. You'll be redirected back to the application
5. Navigate to **Sessions** to create your first quiz session

### 10. Generate GraphQL Types (If Modifying Queries)

After modifying any `.graphql` files:

```bash
cd QuizManagement.Ui
npm run codegen
```

This regenerates TypeScript types in `graphql/generated.ts`.

## 📁 Project Structure

```
handball-belgium-rules-quiz/
├── .github/workflows/                    # CI/CD Pipelines
│   ├── pr.yml                            # PR validation (lint, test, build)
│   └── main.yml                          # Main deployment (release, build, deploy)
│
├── .husky/                               # Git Hooks
│   ├── commit-msg                        # Validates commit format with commitlint
│   └── pre-commit                        # Runs Angular build before commit
│
├── QuizManagement.Api/                   # 🔷 .NET Web API (.NET 10)
│   ├── Controllers/
│   │   └── AccountController.cs          # Authentication endpoints
│   ├── Graphql/                          # Hot Chocolate GraphQL
│   │   ├── QuizQueries.cs                # GraphQL queries (sessions, questions, titles)
│   │   ├── QuizMutations.cs              # GraphQL mutations (create, delete, send)
│   │   ├── QuizSessionType.cs            # GraphQL type definitions
│   │   ├── DataLoaders.cs                # N+1 query optimization
│   │   └── Models/                       # Input/output models
│   ├── Program.cs                        # Application entry point & DI setup
│   ├── SecurityStartup.cs                # Auth0 JWT configuration
│   ├── QuizManagementMigrationExtensions.cs # EF Core migration runner
│   ├── appsettings.json                  # Configuration (DB, Auth0, Email, etc.)
│   └── wwwroot/                          # Angular production build (post-build)
│
├── QuizManagement.Application/           # 🔷 Business Logic Layer (.NET 10)
│   ├── Services/                         # Application services
│   ├── GraphQL/                          # GraphQL client for external APIs
│   └── QuizManagement.Application.csproj # Dependencies: StrawberryShake.Server
│
├── QuizManagement.Domain/                # 🔷 Domain Layer (.NET 10)
│   ├── Entities/                         # Domain entities (QuizSession, Question, etc.)
│   ├── ValueObjects/                     # Value objects
│   └── QuizManagement.Domain.csproj      # No external dependencies (pure domain)
│
├── QuizManagement.Infrastructure/        # 🔷 Infrastructure Layer (.NET 10)
│   ├── Data/
│   │   ├── QuizManagementDbContext.cs    # EF Core DbContext
│   │   └── Migrations/                   # Database migrations
│   ├── Services/
│   │   ├── EmailService.cs               # Brevo email integration
│   │   ├── QuizResultsPdfService.cs      # QuestPDF report generation
│   │   └── ...                           # Other external services
│   └── QuizManagement.Infrastructure.csproj # Dependencies: EF Core, QuestPDF
│
├── QuizManagement.Ui/                    # 🅰️ Angular 21 Frontend
│   ├── src/
│   │   ├── app/
│   │   │   ├── app.ts                    # Root component (header, router-outlet, footer)
│   │   │   ├── app.config.ts             # App configuration (providers, i18n, Apollo)
│   │   │   ├── app.routes.ts             # Route definitions
│   │   │   │
│   │   │   ├── auth/                     # 🔐 Authentication Module
│   │   │   │   ├── guards/auth-guard.ts  # Route protection
│   │   │   │   ├── services/auth.ts      # Auth service (login, logout, user state)
│   │   │   │   └── models/user.ts        # User model
│   │   │   │
│   │   │   ├── home/                     # 🏠 Home Page
│   │   │   │   └── home.ts               # Landing page component
│   │   │   │
│   │   │   ├── sessions/                 # 📋 Session Management
│   │   │   │   ├── create/               # Create sessions page
│   │   │   │   │   ├── create-sessions.ts
│   │   │   │   │   └── components/       # Question search, user import, etc.
│   │   │   │   └── list/                 # Sessions list page
│   │   │   │       ├── list-sessions.ts
│   │   │   │       └── components/
│   │   │   │           ├── filters/                     # 🔍 Filter Components
│   │   │   │           │   ├── date-range-filter/       # Reusable date range picker
│   │   │   │           │   ├── performance-filters/     # Score, percentage, questions
│   │   │   │           │   ├── session-filters-card/    # Main filter orchestration
│   │   │   │           │   ├── sorting-panel/           # Collapsible sort controls
│   │   │   │           │   ├── status-filter-tabs/      # Status tabs with scroll
│   │   │   │           │   └── title-filter/            # Autocomplete title search
│   │   │   │           ├── session-display/             # 📱 Display Components
│   │   │   │           │   ├── session-mobile-card/     # Mobile card view
│   │   │   │           │   └── session-table-row/       # Desktop table row
│   │   │   │           ├── dialogs/                     # 💬 Modal Dialogs
│   │   │   │           │   ├── delete-sessions-dialog/
│   │   │   │           │   ├── send-invitations-dialog/
│   │   │   │           │   └── send-results-dialog/
│   │   │   │           ├── column-visibility-menu/      # Table column toggles
│   │   │   │           └── session-bulk-actions/        # Bulk operations toolbar
│   │   │   │
│   │   │   ├── quiz/                     # 🎯 RefTest Taking
│   │   │   │   ├── welcome/              # RefTest start page
│   │   │   │   │   └── components/       # Instructions, session details, hero
│   │   │   │   ├── take/                 # RefTest taking page
│   │   │   │   │   ├── take-quiz.ts
│   │   │   │   │   ├── guards/           # Can deactivate guard
│   │   │   │   │   └── components/       # Question card, navigation, results, etc.
│   │   │   │   └── components/
│   │   │   │       └── quiz-error/       # Error display component
│   │   │   │
│   │   │   ├── pipes/                    # 🔧 Custom Pipes
│   │   │   │   └── translation-pipe.ts   # Translation utilities
│   │   │   │
│   │   │   └── shared/                   # 🔄 Shared Utilities
│   │   │       └── pipes/                # Additional shared pipes
│   │   │
│   │   ├── assets/
│   │   │   └── i18n/                     # 🌐 Translation Files
│   │   │       ├── en.json               # English
│   │   │       ├── nl.json               # Dutch
│   │   │       ├── fr.json               # French
│   │   │       └── de.json               # German
│   │   │
│   │   ├── environments/
│   │   │   └── version.ts                # Auto-generated version file (gitignored)
│   │   │
│   │   ├── index.html                    # HTML entry point
│   │   ├── main.ts                       # Bootstrap Angular app
│   │   └── styles.css                    # Global TailwindCSS styles
│   │
│   ├── graphql/                          # 📡 GraphQL Operations
│   │   ├── complete-session.graphql      # Complete RefTest mutation
│   │   ├── create-sessions.graphql       # Create sessions mutation
│   │   ├── delete-session.graphql        # Delete sessions mutation
│   │   ├── get-sessions.graphql          # List sessions query
│   │   ├── get-session-by-token.graphql  # Get RefTest for reftest taker
│   │   ├── send-invitations.graphql      # Send email invitations mutation
│   │   ├── send-results.graphql          # Send results mutation
│   │   ├── search-questions-by-number.graphql
│   │   ├── get-titles.graphql            # Get RefTest titles
│   │   ├── start-session.graphql         # Start RefTest mutation
│   │   └── generated.ts                  # 🤖 Auto-generated TypeScript types
│   │
│   ├── scripts/
│   │   └── generate-version.mjs          # Sync version from package.json
│   │
│   ├── public/                           # Static assets
│   │   └── i18n/                         # Translation files (copied to dist)
│   │
│   ├── angular.json                      # Angular CLI configuration
│   ├── codegen.ts                        # GraphQL Code Generator config
│   ├── proxy.conf.json                   # Dev server proxy settings
│   ├── package.json                      # Frontend dependencies
│   ├── tailwind.config.js                # TailwindCSS configuration
│   ├── tsconfig.json                     # TypeScript config
│   └── vite.config.ts                    # Vite build configuration
│
├── .releaserc.json                       # Semantic Release config (emojis, plugins)
├── commitlint.config.js                  # Conventional commits validation
├── package.json                          # Root dependencies (semantic-release, husky)
├── handball-belgium-rules-quiz-management.sln # .NET solution file
└── README.md                             # This file
```

### Key Directories Explained

| Directory                                | Purpose                                                  |
| ---------------------------------------- | -------------------------------------------------------- |
| `QuizManagement.Api/Graphql`             | GraphQL schema, queries, mutations, and type definitions |
| `QuizManagement.Infrastructure/Services` | PDF generation (QuestPDF) and email delivery (Brevo)     |
| `QuizManagement.Ui/src/app/sessions`     | Session creation and management UI                       |
| `QuizManagement.Ui/src/app/quiz`         | RefTest-taking experience (welcome, take, results)       |
| `QuizManagement.Ui/graphql`              | GraphQL operation files and auto-generated types         |
| `.github/workflows`                      | CI/CD pipelines for automated testing and deployment     |

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
    "QuizManagement": "Server=localhost;Database=QuizManagement;Trusted_Connection=True;TrustServerCertificate=True;"
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

  "ReportConfiguration": {
    "RecipientEmails": []
  }
}
```

### Configuration Options Explained

| Section                   | Key                     | Description                               | Required |
| ------------------------- | ----------------------- | ----------------------------------------- | -------- |
| **ConnectionStrings**     | `QuizManagement`        | SQL Server or Azure SQL connection string | ✅ Yes   |
| **Auth0**                 | `Domain`                | Auth0 tenant domain                       | ✅ Yes   |
|                           | `ClientId`              | Auth0 application client ID               | ✅ Yes   |
|                           | `ClientSecret`          | Auth0 application client secret           | ✅ Yes   |
|                           | `Audience`              | Auth0 API identifier                      | ✅ Yes   |
| **EmailConfiguration**    | `BaseUrl`               | Base URL for email links                  | ✅ Yes   |
|                           | `BrevoApiKey`           | Brevo (SendGrid) API key                  | ✅ Yes   |
|                           | `BrevoApiUrl`           | Brevo API endpoint                        | ✅ Yes   |
|                           | `FromEmail`             | Sender email address                      | ✅ Yes   |
|                           | `FromName`              | Sender display name                       | ✅ Yes   |
|                           | `ScheduledDelayMinutes` | Delay in minutes for scheduled emails     | ⚠️ Optional (defaults to 0) |
| **RulesQuestions**        | `Url`                   | External question bank GraphQL endpoint   | ✅ Yes   |
| **LanguageConfiguration** | `DefaultPhraseLanguage` | Default language for questions            | ✅ Yes   |
|                           | `EnabledLanguages`      | Array of enabled UI languages (en/nl/fr/de) | ⚠️ Optional (defaults to all 4) |
| **ScoreConfiguration**    | `PassingPercentage`     | Percentage required to pass a quiz        | ⚠️ Optional (defaults to 80) |
| **ReportConfiguration**   | `RecipientEmails`       | Array of emails to receive system reports | ⚠️ Optional (defaults to empty) |

### User Secrets (Development)

For sensitive data, use .NET User Secrets instead of `appsettings.json`:

```bash
cd QuizManagement.Api

# Database
dotnet user-secrets set "ConnectionStrings:QuizManagement" "Server=localhost;..."

# Auth0
dotnet user-secrets set "Auth0:Domain" "your-tenant.auth0.com"
dotnet user-secrets set "Auth0:ClientId" "your-client-id"
dotnet user-secrets set "Auth0:ClientSecret" "your-client-secret"
dotnet user-secrets set "Auth0:Audience" "your-api-identifier"

# Email
dotnet user-secrets set "EmailConfiguration:BrevoApiKey" "your-api-key"
dotnet user-secrets set "EmailConfiguration:FromEmail" "noreply@domain.com"
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
   az ad sp create-for-rbac --name "handball-quiz-deploy" \\
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
   "subject": "repo:KristofGilis/handball-belgium-rules-quiz:ref:refs/heads/main",
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

## 🔄 Development Workflow

### Branching Strategy

- `main`: Production branch, protected
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

### Automated Deployment (GitHub Actions)

The application deploys automatically when changes are merged to `main`:

1. **Semantic Release**: Analyzes commits and creates a new version
2. **Build**:
   - Builds Angular application
   - Copies build to `QuizManagement.Api/wwwroot/`
   - Builds and publishes .NET application
3. **Deploy**: Deploys to Azure App Service

### Manual Deployment

#### Build for Production

```bash

# Build Angular

cd QuizManagement.Ui
npm run build

# Copy to API wwwroot

mkdir -p ../QuizManagement.Api/wwwroot
cp -r dist/quiz-management.ui/browser/* ../QuizManagement.Api/wwwroot/

# Publish .NET

cd ../QuizManagement.Api
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

After modifying any `.graphql` files in `QuizManagement.Ui/graphql/`:

```bash
cd QuizManagement.Ui
npm run codegen
```

This regenerates TypeScript types in `graphql/generated.ts` based on your GraphQL schema.

### Manual Testing

1. **GraphQL Playground**: `https://localhost:7039/graphql/`
2. **Test Authentication**: Click "Sign In" and verify Auth0 redirect
3. **Create Session**: Navigate to Sessions > Create and test bulk creation
4. **Take RefTest**: Use the generated token URL to take a reftest
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
- **`QuizManagement.Ui/package.json`**: Synced during release via `@semantic-release/exec`
- **`src/environments/version.ts`**: Auto-generated on build via `generate-version.mjs`
- **Footer Display**: Shows current version in app footer

### Release Process

1. Commits merged to `main` trigger semantic-release
2. Semantic-release analyzes commits and determines version
3. Updates both `package.json` files
4. Generates `CHANGELOG.md` with emoji-categorized sections
5. Creates Git tag and GitHub release
6. Triggers build and deployment pipeline

## 📄 License

This project is licensed under the ISC License - see the LICENSE file for details.

## 👨‍💻 Authors

- **Kristof Gilis** - _Initial work and maintenance_

## 🙏 Acknowledgments

- Handball Belgium for the requirements and domain expertise
- IHF (International Handball Federation) for the rules content
- The open-source community for the amazing tools and libraries

## 💬 Support

For issues, questions, or contributions:

- **Issues**: [GitHub Issues](https://github.com/KristofGilis/handball-belgium-rules-quiz/issues)
- **Discussions**: [GitHub Discussions](https://github.com/KristofGilis/handball-belgium-rules-quiz/discussions)

**Made with ❤️ for Handball Belgium**
