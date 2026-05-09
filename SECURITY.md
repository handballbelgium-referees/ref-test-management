# Security & Permissions

This document describes the task-based permission system used by the RefTest Management Platform.

## Overview

The application uses **Auth0** for authentication and a **task-based permission system** for authorization. Every protected GraphQL operation maps 1:1 to a named permission. Permissions are issued by Auth0 as claims in the JWT access token and forwarded to the cookie session on login.

The backend enforcement lives in the `RefTestManagement.Security` class library. The Angular frontend uses a `PermissionsService` and a `HasPermission` structural directive for reactive, signal-based UI control.

---

## Auth0 Setup

### 1. Enable RBAC on your API

1. Go to **Auth0 Dashboard → Applications → APIs**
2. Select the API that your application authenticates against
3. Open the **Settings** tab → scroll to **RBAC Settings**
4. Enable **"Enable RBAC"**
5. Enable **"Add Permissions in the Access Token"**
6. Save

### 2. Register permissions on the API

On the same API → **Permissions** tab, add each permission string listed in the [Permission Reference](#permission-reference) below.

### 3. Create roles and assign permissions

1. Go to **User Management → Roles** → create a role (e.g. `Admin`, `Viewer`)
2. On the role → **Permissions** tab → add permissions from your API
3. Go to **User Management → Users** → select a user → **Roles** tab → assign the role

### 4. Verify the token

Decode your access token at [jwt.io](https://jwt.io). You should see a `permissions` array:

```json
{
  "permissions": [
    "ref-tests:create",
    "ref-tests:view-list",
    "ref-tests:view-detail"
  ]
}
```

> **Important**: Make sure your application requests the correct **audience** that matches your API identifier. Without the audience, Auth0 returns an opaque token with no permissions.

---

## Permission Reference

### Special Permissions

| Permission   | Description                                                                    |
| ------------ | ------------------------------------------------------------------------------ |
| `superadmin` | Bypasses all permission checks — grants unrestricted access to every operation |

### RefTests

| Permission                        | Protects                                          | Type     |
| --------------------------------- | ------------------------------------------------- | -------- |
| `ref-tests:create`                | `createRefTests` mutation                         | Mutation |
| `ref-tests:delete`                | `deleteRefTests` mutation                         | Mutation |
| `ref-tests:update-details`        | `updateRefTestDetails` mutation                   | Mutation |
| `ref-tests:update-configuration`  | `updateRefTestConfiguration` mutation             | Mutation |
| `ref-tests:update-notifications`  | `updateRefTestNotificationSettings` mutation      | Mutation |
| `ref-tests:extend-time`           | `extendRefTestTime` mutation                      | Mutation |
| `ref-tests:regenerate-token`      | `regenerateRefTestToken` mutation                 | Mutation |
| `ref-tests:reset`                 | `resetRefTests` mutation                          | Mutation |
| `ref-tests:revive`                | `reviveRefTests` mutation                         | Mutation |
| `ref-tests:send-invitations`      | `sendRefTestInvitations` mutation                 | Mutation |
| `ref-tests:send-results`          | `sendRefTestResults` mutation                     | Mutation |
| `ref-tests:send-report`           | `sendRefTestReport` mutation                      | Mutation |
| `ref-tests:view-list`             | `refTests` query + `refTestsUpdated` subscription | Query    |
| `ref-tests:view-detail`           | `refTest` query + `refTestUpdated` subscription   | Query    |
| `ref-tests:view-detail-questions` | `questions` field on `RefTest` (detail page)      | Query    |
| `ref-tests:view-titles`           | `refTestTitles` query                             | Query    |
| `ref-tests:*`                     | Wildcard — grants all `ref-tests:*` permissions   | Wildcard |

### Questions

| Permission         | Protects                                        | Type     |
| ------------------ | ----------------------------------------------- | -------- |
| `questions:search` | `searchQuestionsByNumber` query                 | Query    |
| `questions:view`   | `getQuestionsByNumber` query                    | Query    |
| `questions:*`      | Wildcard — grants all `questions:*` permissions | Wildcard |

---

## Wildcard & Superadmin Resolution

The `TaskPermissionHandler` resolves permissions in the following order:

1. **Superadmin**: if the user holds the `superadmin` permission, all checks succeed immediately
2. **Exact match**: `ref-tests:create` matches only `ref-tests:create`
3. **Namespace wildcard**: `ref-tests:*` matches any `ref-tests:…` permission

For OR-semantics (a field accessible with any one of several permissions), the `AnyTaskPermissionHandler` applies the same resolution logic against a set of permissions and succeeds on the first match.

---

## Suggested Roles

| Role      | Permissions                                                                                                                                                        |
| --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Admin`   | `ref-tests:*`, `questions:*`                                                                                                                                       |
| `Manager` | `ref-tests:view-list`, `ref-tests:view-detail`, `ref-tests:view-detail-questions`, `ref-tests:send-invitations`, `ref-tests:send-results`, `ref-tests:send-report` |
| `Viewer`  | `ref-tests:view-list`, `ref-tests:view-detail`                                                                                                                     |

---

## Backend Implementation

The `RefTestManagement.Security` project (no dependencies on other projects) contains:

| Type                              | Purpose                                                          |
| --------------------------------- | ---------------------------------------------------------------- |
| `Permissions`                     | Single source of truth for all permission string constants       |
| `TaskPermissionRequirement`       | `IAuthorizationRequirement` for a single required permission     |
| `TaskPermissionHandler`           | Handles exact match, wildcard, and superadmin bypass             |
| `AnyTaskPermissionRequirement`    | Requirement satisfied by any one of N permissions (OR semantics) |
| `AnyTaskPermissionHandler`        | Handler for OR-permission requirements                           |
| `TaskAuthorizationPolicyProvider` | Resolves `anyof:perm1\|perm2` policy names at runtime            |
| `SecurityServiceExtensions`       | `AddTaskBasedAuthorization()` — registers all of the above       |

Registered in `Program.cs`:

```csharp
services.AddSecurityConfiguration(configuration); // Auth0 OIDC + JWT Bearer
services.AddTaskBasedAuthorization();             // task-based permission policies
```

Permissions are extracted from the Auth0 JWT access token in `SecurityStartup.cs` via the `OnTokenValidated` OIDC event and copied into the cookie identity as `permissions` claims. This means cookie-authenticated browser sessions also carry full permission information.

---

## Frontend Implementation

### `PermissionsService`

Fetches permissions once from `/Account/Permissions` when the user is authenticated. Exposes a reactive `permissions` signal (`undefined` while loading) and a `hasPermission(permission)` helper.

```typescript
// Force a re-fetch after role changes (optional)
permissionsService.refresh();
```

### `HasPermission` Directive

Structural directive that conditionally renders elements based on a permission:

```html
<button *hasPermission="Permissions.RefTests.Create">Create</button>
```

### Route Guards

Protected routes use `permissionGuard` after `authGuard`. The guard waits for permissions to load before evaluating:

```typescript
{
  path: 'ref-tests',
  canActivate: [authGuard, permissionGuard(Permissions.RefTests.ViewList)],
}
```

| Route               | Required Permission     |
| ------------------- | ----------------------- |
| `/ref-tests`        | `ref-tests:view-list`   |
| `/ref-tests/create` | `ref-tests:create`      |
| `/ref-tests/:id`    | `ref-tests:view-detail` |
