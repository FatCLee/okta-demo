# Authentication And Invitation Flow

中文版本：[auth-flow.zh-CN.md](auth-flow.zh-CN.md)

This project separates identity, business authorization, and user experience:

- Frontend owns the browser experience.
- Backend owns business rules, authorization, and protected data.
- Okta and Microsoft Entra ID own identity proof.

## User Populations

| User | Identity Provider | App Role |
| --- | --- | --- |
| Relationship Manager | Microsoft Entra ID | Internal user who creates KYC cases and client invites |
| Client | Okta | External user who activates/signs in and completes KYC work |

## Frontend Responsibilities

The frontend coordinates browser flows, but it does not decide who someone truly is or what they are allowed to do.

The frontend handles:

- Showing RM/client sign-in choices.
- Redirecting clients to Okta.
- Redirecting RMs to Microsoft Entra ID.
- Handling callback URLs such as `/login/callback` and `/rm/callback`.
- Receiving access tokens through the Okta and MSAL SDKs.
- Sending access tokens to the backend with `Authorization: Bearer <token>`.
- Showing UI based on backend responses.

The frontend can say: "I have a token from Entra ID. Please create this invite."

The frontend should not say: "Trust me, I am an RM."

## Backend Responsibilities

The backend makes trust decisions and owns the KYC domain model.

The backend handles:

- Validating Okta access tokens for client APIs.
- Validating Microsoft Entra ID access tokens for RM APIs.
- Enforcing role-based access.
- Creating KYC cases.
- Creating local placeholder client records.
- Creating and storing invitation records.
- Calling Okta Management APIs to create and activate users.
- Returning case, invite, and session data to the frontend.
- Persisting users, roles, identity-provider links, cases, and invites.

For example, `POST /api/cases/invite` only works when the backend receives a valid Entra access token. The frontend sends the token; the backend validates and authorizes it.

## Client Login Flow

This is the external-user flow through Okta.

1. RM creates a KYC case and client invite.
2. Backend creates local client, case, and invite records.
3. Backend may call Okta to create a staged Okta user.
4. Okta returns an activation URL/token.
5. Client receives or opens the activation link.
6. Client activates or signs in through Okta.
7. Frontend handles the Okta callback at `/login/callback`.
8. Frontend receives an Okta access token.
9. Frontend calls backend client APIs with that token.
10. Backend validates the token against Okta.
11. Backend links the Okta identity to the local client/case context.
12. Client continues KYC work.

## RM Login Flow

This is the internal-user flow through Microsoft Entra ID.

1. RM clicks "Continue with Entra ID".
2. Frontend redirects to Microsoft Entra ID through MSAL.
3. RM signs in with corporate identity.
4. Entra redirects back to `/rm/callback`.
5. Frontend receives an Entra access token.
6. Frontend calls RM APIs such as `GET /api/rm/session` or `POST /api/cases/invite`.
7. Backend validates the token against Entra ID.
8. Backend confirms the user is authorized as an RM.
9. Backend allows RM-only actions.

## Token Handoff To The Backend

After Okta or Entra login, the frontend does not forward the whole login response object to the backend. The auth SDK handles the redirect callback, stores tokens, and gives the frontend an access token.

The frontend sends that access token to the backend with an HTTP header:

```http
Authorization: Bearer <access_token>
```

The backend validates the raw access token. It should not trust frontend-decoded profile data for authorization.

### Okta Token Handoff

Okta redirects the browser back to:

```text
/login/callback?code=...
```

The frontend handles the callback and gets the access token:

```ts
await oktaAuth.handleRedirect();

const isAuthenticated = await oktaAuth.isAuthenticated();
const userInfo = await oktaAuth.getUser();
const accessToken = await oktaAuth.tokenManager.get("accessToken");
```

Then the frontend calls the backend:

```ts
await fetch("/api/client/session", {
  headers: {
    Authorization: `Bearer ${accessToken.accessToken}`,
  },
});
```

A decoded Okta access token payload may look like this:

```json
{
  "ver": 1,
  "jti": "AT.xxxxx",
  "iss": "https://your-domain.okta.com/oauth2/default",
  "aud": "api://default",
  "sub": "00uabc123",
  "cid": "your-okta-client-id",
  "uid": "00uabc123",
  "scp": ["openid", "profile", "email"],
  "auth_time": 1710000000,
  "exp": 1710003600,
  "iat": 1710000000
}
```

If Okta groups are configured as an access-token claim, the token may also include:

```json
{
  "groups": ["KYC_Client"]
}
```

### Entra Token Handoff

Microsoft Entra ID redirects the browser back to:

```text
/rm/callback?code=...
```

The frontend handles the callback and gets the access token:

```ts
await msalInstance.initialize();

const redirectResult = await msalInstance.handleRedirectPromise();
const account = redirectResult?.account ?? msalInstance.getAllAccounts()[0];

const tokenResult = await msalInstance.acquireTokenSilent({
  account,
  scopes: getEntraApiScopes(),
});
```

Then the frontend calls the backend:

```ts
await fetch("/api/rm/session", {
  headers: {
    Authorization: `Bearer ${tokenResult.accessToken}`,
  },
});
```

RM invite creation uses the same pattern:

```ts
await fetch("/api/cases/invite", {
  method: "POST",
  headers: {
    Authorization: `Bearer ${tokenResult.accessToken}`,
    "Content-Type": "application/json",
  },
  body: JSON.stringify(inviteForm),
});
```

A decoded Entra access token payload may look like this:

```json
{
  "aud": "api://your-api-application-client-id",
  "iss": "https://login.microsoftonline.com/<tenant-id>/v2.0",
  "iat": 1710000000,
  "nbf": 1710000000,
  "exp": 1710003600,
  "azp": "your-spa-client-id",
  "name": "Jamie RM",
  "oid": "00000000-0000-0000-0000-000000000000",
  "preferred_username": "jamie@company.com",
  "scp": "access_as_user",
  "sub": "...",
  "tid": "<tenant-id>",
  "ver": "2.0"
}
```

### Backend Validation

For both providers, the backend validates:

- Token signature.
- Issuer.
- Audience.
- Expiration.
- Scopes, groups, or claims if needed.
- Local user, role, case, and invite mapping.

The frontend may use decoded/profile info for display, such as "Hello, Jamie". Backend authorization uses the raw bearer token and local business data.

## How Invitation Works

Invitation has two parts working together:

```text
App invitation = business workflow
Okta activation = identity account setup
```

Okta does not know what a KYC case is. The backend does.

### Frontend Role

The frontend is the RM operator surface.

For RM invite creation, the frontend:

1. Signs the RM in with Microsoft Entra ID.
2. Gets an Entra access token from MSAL.
3. Collects client email, client name, existing-account status, and RM name.
4. Calls the backend invite endpoint with the Entra token.

Example request:

```http
POST /api/cases/invite
Authorization: Bearer <entra_access_token>
Content-Type: application/json
```

Example body:

```json
{
  "clientEmail": "client@example.com",
  "clientName": "Client Name",
  "hasExistingAccount": false,
  "relationshipManager": "Jamie RM"
}
```

The frontend does not create the Okta user. It does not decide whether the RM is allowed to create the invite. It sends the RM token and invite details to the backend.

### Backend Role

The backend owns the real invitation.

For invite creation, the backend:

1. Validates the Entra access token.
2. Confirms the caller is allowed to create an invite.
3. Creates or finds the local `ApplicationUser` for the client.
4. Creates or finds the local `ApplicationUser` for the RM.
5. Creates a `KycCase`.
6. Creates a `ClientInvite`.
7. Calls Okta if the client is new and Okta Management API is configured.
8. Stores Okta activation information on the invite.
9. Returns invite details to the frontend.

The backend stores business meaning in local records:

```text
ApplicationUser
UserRole
UserRoleAssignment
IdentityProviderAccount
KycCase
ClientInvite
```

That lets the backend represent facts such as:

```text
Jamie RM invited client@example.com for KYC case 123.
This invite is pending, sent, redeemed, or activation-ready.
This invite belongs to Okta user 00uabc123.
```

### Okta Role

Okta owns the external identity account lifecycle.

For a new client, the backend calls Okta to:

1. Create an Okta user with `activate=false`.
2. Call the Okta lifecycle activation endpoint.
3. Receive an `activationUrl`.

Okta knows:

```text
This Okta user exists.
This user needs activation.
This activation link is valid.
This user can set a password, enroll factors, and sign in.
```

Okta does not know:

```text
This invite belongs to KYC case 123.
This RM created the invite.
This client should see a specific banking workflow.
```

### New Client Flow

For a new client:

1. RM uses the frontend to submit invite details.
2. Frontend calls `POST /api/cases/invite` with the Entra access token.
3. Backend validates the Entra token.
4. Backend creates local client, case, and invite records.
5. Backend creates a staged Okta user.
6. Backend requests an Okta activation link.
7. Okta returns `activationUrl`.
8. Backend stores `activationUrl` on `ClientInvite`.
9. Backend returns invite details to the frontend.
10. Frontend shows or sends the activation link.
11. Client opens the Okta activation link.
12. Okta activates the account.
13. Client signs in through Okta.
14. Frontend receives an Okta token.
15. Backend validates the Okta token and maps the user to the case.

### Existing Client Flow

For a client who already has an Okta account:

1. RM uses the frontend to submit invite details.
2. Frontend calls `POST /api/cases/invite` with the Entra access token.
3. Backend validates the Entra token.
4. Backend creates local case and invite records.
5. Backend does not need to create a new Okta user.
6. Backend returns invite details to the frontend.
7. Client signs in with the existing Okta account.
8. Frontend receives an Okta token.
9. Backend validates the token and maps the user to the case/invite.

The guiding rule is:

```text
Frontend: collect invite details and display next step
Backend: create, authorize, and store the business invite
Okta: create, activate, and sign in the external identity
```

## Invitation Versus Activation

Invitation is the app's business concept. Activation is Okta's identity lifecycle.

The app owns:

- Which RM invited which client.
- Which KYC case the invite belongs to.
- Whether the client had an existing account.
- Invite status, audit trail, and case linkage.
- Bank-branded email content if custom email is used.

Okta owns:

- The Okta user record.
- Account activation.
- Activation URL/token.
- Hosted sign-in after activation.

For this demo, the preferred model is app-managed invitation with Okta-managed activation. The backend stores an invite record and calls Okta to create/activate the user. The activation link can be delivered by the app or, if desired, by Okta's built-in activation email flow.

## Password And Account Management

Password changes stay with the identity provider. The frontend can provide links into the provider-owned account pages, but the backend should not receive old or new passwords.

For clients, the app links to Okta account settings:

```text
https://<okta-org-url>/account-settings/home
```

The frontend derives `<okta-org-url>` from `VITE_OKTA_ISSUER`.

For RMs, the app links to Microsoft My Sign-Ins password change:

```text
https://mysignins.microsoft.com/security-info/password/change?tileType=ChangePassword
```

These links are frontend-only navigation. They do not call the backend API.

## Mental Model

The identity provider authenticates the person:

```text
This is alice@company.com.
```

The backend authorizes the action:

```text
Alice is an RM, so she can create a client invite.
```

The frontend facilitates the experience:

```text
Alice clicked create invite, so send the request with her token.
```

Okta and Entra prove identity. The backend connects those identities to the bank KYC domain model.
