# Okta Demo

中文版本：[README.zh-CN.md](README.zh-CN.md)

Starter implementation for a bank KYC identity-provider demo:

- `web/`: React + Vite frontend showing the RM invite flow and client login/redeem flow
- `api/`: ASP.NET Core API with SQLite persistence, EF Core entities, and real token validation for Okta and Entra ID

For the division of responsibilities between frontend, backend, Okta, and Entra ID, see [Authentication And Invitation Flow](docs/auth-flow.md).

## Run the API

```bash
cd api
dotnet run
```

The API will expose:

- `GET /api/cases`
- `POST /api/cases/invite`
- `POST /api/auth/login`
- `POST /api/auth/redeem-invite`
- `GET /api/client/session`
- `GET /api/rm/session`

## Run the web app

```bash
cd web
npm install
cp .env.example .env
npm run dev
```

The Vite dev server proxies `/api` to `http://localhost:5204`.

## Persistence model

The API uses EF Core with SQLite for local demo persistence. The database is created automatically at `api/app_data/okta-demo.db` when the API starts.

Concrete entity classes live under `api/Data/Entities`:

- `ApplicationUser`: local app user record shared by clients and RMs
- `UserRole`: role catalog, currently `client` and `rm`
- `UserRoleAssignment`: local user-to-role mapping
- `IdentityProviderAccount`: Okta or Microsoft Entra ID account link
- `KycCase`: bank KYC case owned by an RM and assigned to a client
- `ClientInvite`: invitation, activation URL, and redemption status for the client onboarding flow

`DatabaseSeeder` creates the demo roles, one RM, one existing client, one case, and one invite on first startup.

## Current scope

This repo now includes real redirect flows for both user populations:

- Frontend uses `@okta/okta-auth-js` and handles `/login/callback`
- Frontend uses `@azure/msal-browser` and handles `/rm/callback`
- API validates client bearer tokens against the configured Okta issuer
- API validates RM bearer tokens against the configured Microsoft Entra ID tenant
- Protected demo endpoint: `GET /api/client/session`
- Protected RM endpoint: `GET /api/rm/session`
- RM-only invite endpoint: `POST /api/cases/invite`

## Okta setup

Use an Okta SPA application and an authorization server that issues access tokens for your API.

Frontend values go in `web/.env`:

```bash
VITE_OKTA_ISSUER=https://your-domain.okta.com/oauth2/default
VITE_OKTA_CLIENT_ID=yourClientId
VITE_OKTA_REDIRECT_URI=http://localhost:5173/login/callback
VITE_OKTA_SCOPES=openid,profile,email,offline_access

VITE_ENTRA_TENANT_ID=your-tenant-id
VITE_ENTRA_CLIENT_ID=your-spa-application-client-id
VITE_ENTRA_REDIRECT_URI=http://localhost:5173/rm/callback
VITE_ENTRA_API_SCOPE=api://your-api-application-client-id/access_as_user
```

Backend values go in `api/appsettings.Development.json`:

```json
{
  "Okta": {
    "Issuer": "https://your-domain.okta.com/oauth2/default",
    "Audience": "api://default"
  },
  "OktaManagement": {
    "BaseUrl": "https://your-domain.okta.com/",
    "ApiToken": "your-management-api-token"
  },
  "Entra": {
    "TenantId": "your-tenant-id",
    "Audience": "api://your-api-application-client-id"
  }
}
```

`OktaManagement.ApiToken` needs permission to create and activate users. The official docs list `okta.users.manage` for these lifecycle operations.

In Okta, make sure the SPA app allows:

- Sign-in redirect URI: `http://localhost:5173/login/callback`
- Sign-out redirect URI: `http://localhost:5173`
- Trusted Origin for your frontend origin if required by your org setup

## Microsoft Entra ID setup

Use a SPA app registration for the frontend and an exposed API scope for the backend.

In Microsoft Entra ID, make sure the SPA app allows:

- Redirect URI: `http://localhost:5173/rm/callback`
- Account type appropriate for internal RM users
- API permission for the exposed backend scope, for example `api://your-api-application-client-id/access_as_user`

For the backend API registration:

- Expose an API scope such as `access_as_user`
- Use the Application ID URI as `api://your-api-application-client-id`
- Set `Entra.Audience` to that Application ID URI
- The RM invite form calls `POST /api/cases/invite` with the Entra access token

## Real invite redemption

For a new client without an existing Okta account:

1. RM submits the invite form in the app
2. API creates a staged Okta user with `activate=false`
3. API calls Okta lifecycle activation with `sendEmail=false`
4. The returned `activationUrl` is shown in the UI
5. Client opens that link to complete the real Okta activation flow
