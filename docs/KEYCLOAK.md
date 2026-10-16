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
role per permission the application checks (see `RefTestManagement.Security/Permissions.cs`).
Assign roles to users through the realm's client role mappings.

### `ref-test-management-admin` (new, service account)

Used by the API to read and create client roles and to read user role mappings. It does not
sign anyone in.

1. Create client `ref-test-management-admin`, client authentication **On**, standard flow **Off**.
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
Identity:Keycloak:AdminClientId         = ref-test-management-admin
Identity:Keycloak:AdminClientSecret     = <ref-test-management-admin secret>
```

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
dotnet user-secrets set "Identity:Keycloak:AdminClientId" "ref-test-management-admin"
dotnet user-secrets set "Identity:Keycloak:AdminClientSecret" "<secret>"
```

## Permission sync

- Sync writes permission roles to the `reftest-api` client. It only adds missing roles (409 is
  treated as success). It never deletes roles, so manual changes in Keycloak are kept.
- Reads use `users/{id}/role-mappings/clients/{clientUuid}`. A disabled or missing user gets an
  empty permission set (fail closed).
- Snapshots are held in memory, so a restart clears them and they are rebuilt on demand.
- The `sub` claim is used as the Keycloak user ID. It must be the Keycloak user UUID.

## Switching providers

1. Set `Identity:Provider` to the target value and restart the API.
2. Users sign in again. Their Auth0 identity is not linked to a Keycloak identity.
3. Assign permission roles in Keycloak, then run a sync (or wait for the snapshot refresh).

## Verification checklist

- [ ] `reftest-beta` redirect URI matches the environment.
- [ ] Access token contains `aud: reftest-api`.
- [ ] `ref-test-management-admin` has the four `realm-management` roles.
- [ ] Sign-in works and the user sees their permissions.
- [ ] A disabled user loses access on the next snapshot refresh.

## Open items

- Keycloak version and Admin API behaviour are not yet verified against the live server.
