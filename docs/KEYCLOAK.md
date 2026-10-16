# Keycloak identity provider runbook

The API can sign users in through Auth0 or Keycloak. The active provider is chosen by
`Identity:Provider` (`Auth0` or `Keycloak`, case-insensitive). Switching providers is a clean
slate: users, roles, and permissions are not migrated. Users sign in again through the new
provider, and permissions are synchronised from the new provider's client roles.

## Environments

| Environment | Realm | Authority | UI client | API audience |
|---|---|---|---|---|
| Beta | `referees-handballbelgium-beta` | `https://login.referees-handballbelgium.be/realms/referees-handballbelgium-beta` | `reftest-beta` | `reftest-api` |
| Production | `referees-handballbelgium` | `https://login.referees-handballbelgium.be/realms/referees-handballbelgium` | `reftest` | `reftest-api` |

## Clients

### `reftest-beta` (existing, OIDC login)

Confidential client used by the API for the authorization code flow. The name refers to the UI
application, but the API performs the token exchange with the client secret.

- Client authentication: **On** (confidential). The API keeps the secret server-side.
- Standard flow: **On**. Direct access grants: **Off**.
- Valid redirect URIs: `https://reftest-beta.referees-handballbelgium.be/callback` (beta),
  `https://reftest.referees-handballbelgium.be/callback` (production)
- Valid post-logout redirect URIs: `https://reftest-beta.referees-handballbelgium.be/*` (beta),
  `https://reftest.referees-handballbelgium.be/*` (production)
- Web origins: `https://reftest-beta.referees-handballbelgium.be` (beta),
  `https://reftest.referees-handballbelgium.be` (production)
- Audience: add an **Audience** protocol mapper (included custom audience `reftest-api`) so that
  access tokens carry `aud: reftest-api`.

### `reftest-api` (existing, permission roles)

Client that owns the permission client roles. Its client ID is the audience. Create one client
role per permission the application checks (see `RefTestManagement.Security/Authorization/Permissions.cs`).
Assign roles to users through the realm's client role mappings.

### `reftest-management-admin` (new, service account)

Used by the API to read and create client roles and to read user role mappings. It does not
sign anyone in.

1. Create client `reftest-management-admin`, client authentication **On**, standard flow **Off**.
2. On the **Service account roles** tab, assign these roles from `realm-management`:
   `view-clients`, `view-users`, `manage-users`, and `manage-clients`.
   `manage-clients` is needed to create missing permission roles on `reftest-api`.
3. Copy the client secret into `Identity:Keycloak:AdminClientSecret`.

## Configuration

Set the values through user secrets (development), environment variables, or Key Vault. Use the
`Identity__Keycloak__*` form for environment variables.

```text
Identity:Provider                       = Keycloak
Identity:Keycloak:Authority             = https://login.referees-handballbelgium.be/realms/referees-handballbelgium-beta
Identity:Keycloak:ClientId              = reftest-beta
Identity:Keycloak:ClientSecret          = <reftest-beta secret>
Identity:Keycloak:Audience              = reftest-api
Identity:Keycloak:AdminClientId         = reftest-management-admin
Identity:Keycloak:AdminClientSecret     = <reftest-management-admin secret>
Identity:PermissionCacheSeconds         = 240
```

`Identity:PermissionCacheSeconds` sets how long a successful permission snapshot is reused (default `240`,
must be positive). Lower it during local role testing, for example to `30`, to see role changes sooner.

`ValidateSettings` fails fast at startup when Authority, ClientId, ClientSecret, or Audience is
blank for Keycloak, and when Authority does not contain `/realms/`. Admin credentials are validated
when permission sync runs, not at startup.

Sign-out redirects to the realm's `protocol/openid-connect/logout` endpoint with `client_id` (URL-escaped).
If `ClientId` is blank, the `client_id` parameter is omitted.

Example:

```powershell
dotnet user-secrets set "Identity:Provider" "Keycloak"
dotnet user-secrets set "Identity:Keycloak:Authority" "https://login.referees-handballbelgium.be/realms/referees-handballbelgium-beta"
dotnet user-secrets set "Identity:Keycloak:ClientId" "reftest-beta"
dotnet user-secrets set "Identity:Keycloak:ClientSecret" "<secret>"
dotnet user-secrets set "Identity:Keycloak:Audience" "reftest-api"
dotnet user-secrets set "Identity:Keycloak:AdminClientId" "reftest-management-admin"
dotnet user-secrets set "Identity:Keycloak:AdminClientSecret" "<secret>"
```

## Permission sync

- Sync writes permission roles to the `reftest-api` client. It only adds missing roles (409 is
  treated as success). It never deletes roles, so manual changes in Keycloak are kept.
- Reads collect the `reftest-api` client roles from the user's direct mappings
  (`users/{id}/role-mappings/clients/{clientUuid}`), from the user's groups and their ancestor
  groups (`groups/{id}/role-mappings/clients/{clientUuid}`), and from composite roles, which are
  expanded transitively (`roles-by-id/{id}/composites/clients/{clientUuid}`). Cycles terminate.
- A disabled or missing user gets an empty permission set (fail closed).
- The admin client needs read access to users, groups, and roles in addition to the role-write
  access used by sync.
- Snapshots are held in memory, so a restart clears them and they are rebuilt on demand.

## User lookup

`GetUsersWithPermissionAsync` (used to notify approvers) follows the same rules as Auth0: a user
matches a permission when they hold that exact role, `superadmin`, or the `ref-tests:*` wildcard.
Users are collected from direct client-role mappings, group mappings (including subgroups), and
users of composite parent roles. Results are cached in memory for 5 minutes per permission.

- Only enabled users with an email address are returned. Disabled users are skipped.
- Display name is first and last name, then username, then email.
- Discovery is capped at 128 Admin API requests. Exceeding the cap, or any Keycloak HTTP error,
  fails the call rather than returning a partial approver list.
- Requires the `view-users` and `view-clients` roles already listed above.
- The `sub` claim is used as the Keycloak user ID. It must be the Keycloak user UUID.

## Switching providers

1. Set `Identity:Provider` to the target value and restart the API.
2. Users sign in again. Their Auth0 identity is not linked to a Keycloak identity.
3. Assign permission roles in Keycloak, then run a sync (or wait for the snapshot refresh).

## Verification checklist

- [ ] `reftest-beta` redirect URI matches the environment.
- [ ] Access token contains `aud: reftest-api`.
- [ ] `reftest-management-admin` has the four `realm-management` roles.
- [ ] Sign-in works and the user sees their permissions.
- [ ] A disabled user loses access on the next snapshot refresh.

## Open items

- Keycloak version and Admin API behaviour are not yet verified against the live server.
