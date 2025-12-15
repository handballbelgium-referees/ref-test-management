# PR Title

```
feat!: initial release - ihf rules quiz management platform - handball belgium v1.0.0
```

## PR Description

This PR introduces the complete IHF Rules Quiz Management Platform for Handball Belgium referees. This is the first production-ready version with comprehensive features for quiz management, session handling, automated deployment, and a polished user interface.

### ✨ Major Features

#### Quiz Management

- Complete quiz session lifecycle (create, manage, delete, take quiz)
- Bulk session creation with user import and configuration
- Question search and bulk import functionality
- Random answer ordering for sessions
- Time-limited quiz sessions with auto-submit
- Comprehensive scoring and results display with PDF generation (QuestPDF)
- Quiz navigation with leave/submit confirmation dialogs

#### Session Management

- **Advanced filtering system**:
  - Status filter with responsive horizontal scroll and arrow navigation
  - Performance filters (score, percentage, questions ranges)
  - Title autocomplete with GraphQL integration
  - Date range filters for started/completed dates
  - Collapsible sorting panel
- **Bulk operations**: Send invitations, results, and delete sessions
- **Loading states** with visual feedback for all async operations
- **Responsive design**: Optimized mobile card view and desktop table with customizable column visibility
- **Real-time status tracking** and progress monitoring

#### Email Automation

- Email automation settings for invitations and results
- Multilingual email templates (EN, NL, FR, DE)
- Brevo API integration for reliable delivery
- Participant name personalization

#### Authentication & Security

- Auth0 integration with JWT Bearer authentication
- OAuth2 authentication flow
- Protected routes with authentication guards
- User profile management with initials display

#### Internationalization

- Full support for 4 languages (English, Dutch, French, German)
- Language preference persistence in local storage
- Dynamic page titles
- Localized quiz content, emails, and PDF reports

#### Developer Experience

- **Semantic versioning** with automated releases
- **Commitlint** for conventional commits
- **Husky** git hooks for commit message validation and pre-commit checks
- **Version display** in application footer
- **CI/CD pipelines** for PR validation and automated deployment
- **Comprehensive documentation** with project structure and setup guides

### 🎨 UI Improvements

#### Component Architecture

- **Extracted filter components**: Split large session-filters-card (646 lines) into 5 focused child components (58% size reduction)
- **Feature-based organization**: Reorganized 13 components into logical folders:
  - `filters/` - 6 filter components (date-range, performance, status, sorting, title)
  - `session-display/` - 2 display components (mobile card, desktop table row)
  - `dialogs/` - 3 modal components (delete, send invitations, send results)
- **Reusable components**: Date range filter used across multiple filters

#### Design Polish

- **Footer redesign**: Cleaner layout with centered content and version display
- **Mobile card styling**: Improved spacing, typography, and status badges
- **Desktop table**: Enhanced row hover states and action button placement
- **Status filter tabs**: Unified responsive design with smooth scroll and arrow buttons
- **Logo optimization**: Cropped URBH-KBHB logo viewBox to remove empty space

### 🔧 Infrastructure & DevOps

#### Semantic Release Setup

- Automated version management from conventional commits
- Shared versioning between .NET API and Angular UI
- Auto-generated changelog with emoji categories
- Version synchronization script (ES modules)

#### GitHub Actions Workflows

- **PR CI**: Linting, testing, and building for pull requests
- **Main CI/CD**: Three-stage pipeline
  - Release: Semantic versioning and changelog generation
  - Build: Angular + .NET API bundled deployment package
  - Deploy: Automated deployment to Azure App Service

#### Project Architecture

- **.NET 10** backend with GraphQL API (Hot Chocolate 15)
- **Angular 21** frontend with standalone components and signals
- **Azure SQL Database** with EF Core 10
- **Angular SPA** served from .NET API wwwroot

### 📦 Technical Highlights

- **Angular 21**: Standalone components, signals, computed values, native control flow (@if, @for)
- **TypeScript 5.9**: Strict type checking throughout
- **Accessibility**: WCAG AA compliant with focus management and ARIA attributes
- **Performance**: OnPush change detection, lazy loading, NgOptimizedImage
- **Code Quality**: Prettier formatting, modular architecture, conventional commits
- **State Management**: Signal-based reactivity with computed derived state
- **GraphQL**: Type-safe operations with GraphQL Code Generator

### 🔐 Required Secrets

Before merging, configure these GitHub secrets:

- `AZURE_CLIENT_ID` - Azure service principal client ID
- `AZURE_TENANT_ID` - Azure AD tenant ID
- `AZURE_SUBSCRIPTION_ID` - Azure subscription ID
- `AZURE_WEBAPP_NAME` - Azure App Service name

### 🚀 Deployment

Once merged to main, semantic-release will:

1. Generate v1.0.0 release
2. Create CHANGELOG.md with emoji-categorized sections
3. Sync version to Angular package.json
4. Build and bundle Angular into .NET API wwwroot
5. Deploy to Azure App Service

### 📚 Documentation

- Comprehensive root README with project setup, configuration, and deployment guides
- Updated project structure with organized component hierarchy
- Frontend architecture documentation explaining feature-based organization
- All emojis and formatting corrected in documentation

---

**BREAKING CHANGE:** This is the initial production release. Future changes will use semantic versioning (MAJOR.MINOR.PATCH) based on conventional commits.
