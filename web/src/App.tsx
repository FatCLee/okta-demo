import { useEffect, useState, type FormEvent } from "react";
import type { AccountInfo } from "@azure/msal-browser";
import type { getEntraApiScopes, hasEntraConfig, msalInstance } from "./entra";
import type { hasOktaConfig, oktaAuth } from "./okta";

type UserType = "client" | "rm";

interface InviteForm {
  clientEmail: string;
  clientName: string;
  hasExistingAccount: boolean;
  relationshipManager: string;
}

interface LoginForm {
  userType: UserType;
  email: string;
}

interface RedeemForm {
  email: string;
  invitationCode: string;
}

interface CaseSummary {
  caseId: string;
  clientEmail: string;
  clientName: string;
  status: string;
  hasExistingAccount: boolean;
  relationshipManager: string;
}

interface InviteRecord {
  inviteId: string;
  caseId: string;
  clientEmail: string;
  clientName: string;
  invitationCode: string;
  loginHint: string;
  deliveryChannel: string;
  identityProvider: string;
  status: string;
  activationUrl?: string | null;
  oktaUserId?: string | null;
}

interface LoginResult {
  userType: string;
  identityProvider: string;
  status: string;
  nextStep: string;
  invitationCode?: string | null;
  activationUrl?: string | null;
}

interface ClientSessionSummary {
  subject?: string | null;
  email?: string | null;
  scopes?: string | null;
  authenticationType?: string | null;
}

interface RmSessionSummary {
  subject?: string | null;
  objectId?: string | null;
  tenantId?: string | null;
  name?: string | null;
  email?: string | null;
  scopes?: string | null;
  authenticationType?: string | null;
}

interface OktaAuthState {
  isAuthenticated: boolean;
  isPending: boolean;
  userInfo: Record<string, unknown> | null;
  apiSummary: ClientSessionSummary | null;
  accessToken: string;
}

interface RmAuthState {
  isAuthenticated: boolean;
  isPending: boolean;
  account: AccountInfo | null;
  apiSummary: RmSessionSummary | null;
  accessToken: string;
}

interface AccessTokenLike {
  accessToken?: string;
}

type OktaModule = {
  hasOktaConfig: typeof hasOktaConfig;
  oktaAuth: typeof oktaAuth;
};

type EntraModule = {
  getEntraApiScopes: typeof getEntraApiScopes;
  hasEntraConfig: typeof hasEntraConfig;
  msalInstance: typeof msalInstance;
};

const emptyInviteForm: InviteForm = {
  clientEmail: "",
  clientName: "",
  hasExistingAccount: false,
  relationshipManager: "Jamie RM",
};

const emptyLoginForm: LoginForm = {
  userType: "client",
  email: "",
};

const emptyRedeemForm: RedeemForm = {
  email: "",
  invitationCode: "",
};

function formatClaimValue(value: unknown): string {
  if (Array.isArray(value)) {
    return value.join(", ");
  }

  if (value === null || value === undefined || value === "") {
    return "Not available";
  }

  return String(value);
}

function toUserType(value: string): UserType {
  return value === "rm" ? "rm" : "client";
}

async function loadOktaModule(): Promise<OktaModule> {
  return import("./okta");
}

async function loadEntraModule(): Promise<EntraModule> {
  return import("./entra");
}

export default function App() {
  const [cases, setCases] = useState<CaseSummary[]>([]);
  const [inviteForm, setInviteForm] = useState(emptyInviteForm);
  const [loginForm, setLoginForm] = useState(emptyLoginForm);
  const [redeemForm, setRedeemForm] = useState(emptyRedeemForm);
  const [inviteResult, setInviteResult] = useState<InviteRecord | null>(null);
  const [loginResult, setLoginResult] = useState<LoginResult | null>(null);
  const [redeemResult, setRedeemResult] = useState<LoginResult | null>(null);
  const [error, setError] = useState("");
  const [authState, setAuthState] = useState<OktaAuthState>({
    isAuthenticated: false,
    isPending: true,
    userInfo: null,
    apiSummary: null,
    accessToken: "",
  });
  const [rmAuthState, setRmAuthState] = useState<RmAuthState>({
    isAuthenticated: false,
    isPending: true,
    account: null,
    apiSummary: null,
    accessToken: "",
  });

  useEffect(() => {
    loadCases();
  }, []);

  useEffect(() => {
    async function bootstrapAuth() {
      const { hasOktaConfig, oktaAuth } = await loadOktaModule();

      if (!hasOktaConfig()) {
        setAuthState((current) => ({ ...current, isPending: false }));
        return;
      }

      try {
        if (window.location.pathname === "/login/callback") {
          await oktaAuth.handleRedirect();
          window.history.replaceState({}, document.title, "/");
        }

        const isAuthenticated = await oktaAuth.isAuthenticated();

        if (!isAuthenticated) {
          setAuthState({
            isAuthenticated: false,
            isPending: false,
            userInfo: null,
            apiSummary: null,
            accessToken: "",
          });
          return;
        }

        const userInfo = await oktaAuth.getUser();
        const accessToken = (await oktaAuth.tokenManager.get(
          "accessToken",
        )) as AccessTokenLike | undefined;
        const apiSummary = await loadProtectedSession(accessToken?.accessToken);

        setAuthState({
          isAuthenticated: true,
          isPending: false,
          userInfo,
          apiSummary,
          accessToken: accessToken?.accessToken ?? "",
        });
      } catch {
        setAuthState({
          isAuthenticated: false,
          isPending: false,
          userInfo: null,
          apiSummary: null,
          accessToken: "",
        });
        setError("Okta sign-in could not be completed. Check your Okta config.");
      }
    }

    bootstrapAuth();
  }, []);

  useEffect(() => {
    async function bootstrapRmAuth() {
      const { getEntraApiScopes, hasEntraConfig, msalInstance } =
        await loadEntraModule();

      if (!hasEntraConfig()) {
        setRmAuthState((current) => ({ ...current, isPending: false }));
        return;
      }

      try {
        await msalInstance.initialize();
        const redirectResult = await msalInstance.handleRedirectPromise();

        if (window.location.pathname === "/rm/callback") {
          window.history.replaceState({}, document.title, "/");
        }

        const account =
          redirectResult?.account ?? msalInstance.getAllAccounts()[0] ?? null;

        if (!account) {
          setRmAuthState({
            isAuthenticated: false,
            isPending: false,
            account: null,
            apiSummary: null,
            accessToken: "",
          });
          return;
        }

        msalInstance.setActiveAccount(account);

        const tokenResult = await msalInstance.acquireTokenSilent({
          account,
          scopes: getEntraApiScopes(),
        });
        const apiSummary = await loadRmSession(tokenResult.accessToken);

        setRmAuthState({
          isAuthenticated: true,
          isPending: false,
          account,
          apiSummary,
          accessToken: tokenResult.accessToken,
        });
      } catch {
        setRmAuthState({
          isAuthenticated: false,
          isPending: false,
          account: null,
          apiSummary: null,
          accessToken: "",
        });
        setError("Entra ID sign-in could not be completed. Check your Entra config.");
      }
    }

    bootstrapRmAuth();
  }, []);

  async function loadCases(): Promise<void> {
    try {
      const response = await fetch("/api/cases");
      const data = (await response.json()) as CaseSummary[];
      setCases(data);
    } catch {
      setError("Unable to load demo cases. Start the API first.");
    }
  }

  async function submitInvite(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setError("");

    if (!rmAuthState.accessToken) {
      setError("Sign in as an RM with Entra ID before creating a client invite.");
      return;
    }

    try {
      const response = await fetch("/api/cases/invite", {
        method: "POST",
        headers: {
          Authorization: `Bearer ${rmAuthState.accessToken}`,
          "Content-Type": "application/json",
        },
        body: JSON.stringify(inviteForm),
      });

      if (response.status === 401 || response.status === 403) {
        setError("Your RM session is not authorized to create invites.");
        return;
      }

      if (!response.ok) {
        setError("Unable to create the invite right now.");
        return;
      }

      const data = (await response.json()) as InviteRecord;
      setInviteResult(data);
      setRedeemForm((current) => ({
        ...current,
        email: data.clientEmail,
        invitationCode: data.invitationCode,
      }));
      setInviteForm(emptyInviteForm);
      await loadCases();
    } catch {
      setError("Unable to create the invite right now.");
    }
  }

  async function loadProtectedSession(
    accessToken?: string,
  ): Promise<ClientSessionSummary | null> {
    if (!accessToken) {
      return null;
    }

    const response = await fetch("/api/client/session", {
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
    });

    if (!response.ok) {
      throw new Error("Protected API call failed.");
    }

    return (await response.json()) as ClientSessionSummary;
  }

  async function loadRmSession(accessToken?: string): Promise<RmSessionSummary | null> {
    if (!accessToken) {
      return null;
    }

    const response = await fetch("/api/rm/session", {
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
    });

    if (!response.ok) {
      throw new Error("Protected RM API call failed.");
    }

    return (await response.json()) as RmSessionSummary;
  }

  async function startOktaLogin(): Promise<void> {
    setError("");
    const { hasOktaConfig, oktaAuth } = await loadOktaModule();

    if (!hasOktaConfig()) {
      setError("Add Okta values to web/.env before starting sign-in.");
      return;
    }

    await oktaAuth.signInWithRedirect();
  }

  async function signOutOfOkta(): Promise<void> {
    setError("");
    const { oktaAuth } = await loadOktaModule();

    await oktaAuth.signOut({
      postLogoutRedirectUri: window.location.origin,
    });
  }

  async function startEntraLogin(): Promise<void> {
    setError("");
    const { getEntraApiScopes, hasEntraConfig, msalInstance } =
      await loadEntraModule();

    if (!hasEntraConfig() || getEntraApiScopes().length === 0) {
      setError("Add Entra values to web/.env before starting RM sign-in.");
      return;
    }

    await msalInstance.initialize();
    await msalInstance.loginRedirect({
      scopes: getEntraApiScopes(),
      prompt: "select_account",
    });
  }

  async function signOutOfEntra(): Promise<void> {
    setError("");
    const { msalInstance } = await loadEntraModule();

    await msalInstance.initialize();
    await msalInstance.logoutRedirect({
      account: rmAuthState.account,
    });
  }

  async function submitLogin(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setError("");

    if (loginForm.userType === "client") {
      await startOktaLogin();
      return;
    }

    await startEntraLogin();
  }

  async function submitRedeem(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    setError("");

    try {
      const response = await fetch("/api/auth/redeem-invite", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(redeemForm),
      });

      const data = (await response.json()) as LoginResult;
      setRedeemResult(data);
    } catch {
      setError("Unable to redeem invitation.");
    }
  }

  return (
    <main className="page-shell">
      <section className="hero">
        <p className="eyebrow">Bank KYC Identity Demo</p>
        <h1>One demo, two identity providers, three clear moments.</h1>
        <p className="hero-copy">
          RMs sign in with Microsoft Entra ID. External clients continue with
          Okta, either through an existing account or an invitation code.
        </p>
        <div className="hero-actions">
          <button type="button" onClick={startOktaLogin}>
            Continue with Okta
          </button>
          <button type="button" onClick={startEntraLogin}>
            Continue with Entra ID
          </button>
          {authState.isAuthenticated && (
            <button type="button" className="secondary" onClick={signOutOfOkta}>
              Sign out of Okta
            </button>
          )}
          {rmAuthState.isAuthenticated && (
            <button type="button" className="secondary" onClick={signOutOfEntra}>
              Sign out of Entra
            </button>
          )}
        </div>
      </section>

      <section className="panel auth-panel">
        <div className="section-heading">
          <div>
            <p className="eyebrow">Real Okta session</p>
            <h2>Client authentication status</h2>
          </div>
          {!authState.isAuthenticated && (
            <span className="status-pill">Config needed</span>
          )}
        </div>

        {authState.isPending ? (
          <p>Checking for an active Okta session...</p>
        ) : authState.isAuthenticated ? (
          <div className="auth-grid">
            <div className="result-card">
              <strong>Authenticated with Okta</strong>
              <p>User: {formatClaimValue(authState.userInfo?.email)}</p>
              <p>Subject: {formatClaimValue(authState.userInfo?.sub)}</p>
              <p>
                Access token:
                {" "}
                {authState.accessToken ? "Present and ready for API calls" : "Missing"}
              </p>
            </div>
            <div className="result-card">
              <strong>Protected API response</strong>
              <p>Email: {formatClaimValue(authState.apiSummary?.email)}</p>
              <p>Subject: {formatClaimValue(authState.apiSummary?.subject)}</p>
              <p>Scopes: {formatClaimValue(authState.apiSummary?.scopes)}</p>
            </div>
          </div>
        ) : (
          <div className="result-card">
            <strong>No active Okta session</strong>
            <p>
              Configure `web/.env`, then sign in as a client to exercise the real
              hosted Okta flow.
            </p>
          </div>
        )}
      </section>

      <section className="panel auth-panel">
        <div className="section-heading">
          <div>
            <p className="eyebrow">Real Entra ID session</p>
            <h2>RM authentication status</h2>
          </div>
          {!rmAuthState.isAuthenticated && (
            <span className="status-pill">Config needed</span>
          )}
        </div>

        {rmAuthState.isPending ? (
          <p>Checking for an active Entra ID session...</p>
        ) : rmAuthState.isAuthenticated ? (
          <div className="auth-grid">
            <div className="result-card">
              <strong>Authenticated with Microsoft Entra ID</strong>
              <p>User: {formatClaimValue(rmAuthState.account?.username)}</p>
              <p>Name: {formatClaimValue(rmAuthState.account?.name)}</p>
              <p>
                Access token:{" "}
                {rmAuthState.accessToken ? "Present and ready for API calls" : "Missing"}
              </p>
            </div>
            <div className="result-card">
              <strong>Protected RM API response</strong>
              <p>Email: {formatClaimValue(rmAuthState.apiSummary?.email)}</p>
              <p>Object ID: {formatClaimValue(rmAuthState.apiSummary?.objectId)}</p>
              <p>Tenant ID: {formatClaimValue(rmAuthState.apiSummary?.tenantId)}</p>
              <p>Scopes: {formatClaimValue(rmAuthState.apiSummary?.scopes)}</p>
            </div>
          </div>
        ) : (
          <div className="result-card">
            <strong>No active Entra ID session</strong>
            <p>
              Configure `web/.env`, then sign in as an RM to exercise the real
              Microsoft Entra ID flow.
            </p>
          </div>
        )}
      </section>

      <section className="grid">
        <article className="panel">
          <h2>1. RM creates a client invite</h2>
          <form onSubmit={submitInvite} className="stack">
            <label>
              Client email
              <input
                type="email"
                value={inviteForm.clientEmail}
                onChange={(event) =>
                  setInviteForm({
                    ...inviteForm,
                    clientEmail: event.target.value,
                  })
                }
                required
              />
            </label>
            <label>
              Client name
              <input
                value={inviteForm.clientName}
                onChange={(event) =>
                  setInviteForm({
                    ...inviteForm,
                    clientName: event.target.value,
                  })
                }
                required
              />
            </label>
            <label>
              Relationship manager
              <input
                value={inviteForm.relationshipManager}
                onChange={(event) =>
                  setInviteForm({
                    ...inviteForm,
                    relationshipManager: event.target.value,
                  })
                }
                required
              />
            </label>
            <label className="checkbox">
              <input
                type="checkbox"
                checked={inviteForm.hasExistingAccount}
                onChange={(event) =>
                  setInviteForm({
                    ...inviteForm,
                    hasExistingAccount: event.target.checked,
                  })
                }
              />
              Client already has an Okta account
            </label>
            <button type="submit" disabled={!rmAuthState.accessToken}>
              Send Okta invite
            </button>
          </form>

          {inviteResult && (
            <div className="result-card">
              <strong>Invite created</strong>
              <p>Invitation code: {inviteResult.invitationCode}</p>
              <p>{inviteResult.loginHint}</p>
              {inviteResult.activationUrl && (
                <p>
                  Activation link:
                  {" "}
                  <a href={inviteResult.activationUrl} target="_blank" rel="noreferrer">
                    Open Okta activation
                  </a>
                </p>
              )}
            </div>
          )}
        </article>

        <article className="panel">
          <h2>2. User chooses a sign-in path</h2>
          <form onSubmit={submitLogin} className="stack">
            <label>
              User type
              <select
                value={loginForm.userType}
                onChange={(event) =>
                  setLoginForm({
                    ...loginForm,
                    userType: toUserType(event.target.value),
                  })
                }
              >
                <option value="client">Client</option>
                <option value="rm">Relationship Manager</option>
              </select>
            </label>
            <label>
              Email
              <input
                type="email"
                value={loginForm.email}
                onChange={(event) =>
                  setLoginForm({ ...loginForm, email: event.target.value })
                }
                required={loginForm.userType === "client"}
              />
            </label>
            <button type="submit">
              {loginForm.userType === "client"
                ? "Redirect to Okta"
                : "Redirect to Entra ID"}
            </button>
          </form>

          {loginResult && (
            <div className="result-card">
              <strong>{loginResult.identityProvider}</strong>
              <p>Status: {loginResult.status}</p>
              <p>{loginResult.nextStep}</p>
              {loginResult.invitationCode && (
                <p>Detected invite: {loginResult.invitationCode}</p>
              )}
              {loginResult.activationUrl && (
                <p>
                  Activation link:
                  {" "}
                  <a href={loginResult.activationUrl} target="_blank" rel="noreferrer">
                    Open Okta activation
                  </a>
                </p>
              )}
            </div>
          )}
        </article>

        <article className="panel">
          <h2>3. Client redeems an invitation</h2>
          <form onSubmit={submitRedeem} className="stack">
            <label>
              Client email
              <input
                type="email"
                value={redeemForm.email}
                onChange={(event) =>
                  setRedeemForm({ ...redeemForm, email: event.target.value })
                }
                required
              />
            </label>
            <label>
              Invitation code
              <input
                value={redeemForm.invitationCode}
                onChange={(event) =>
                  setRedeemForm({
                    ...redeemForm,
                    invitationCode: event.target.value,
                  })
                }
                required
              />
            </label>
            <button type="submit">Redeem invite</button>
          </form>

          {redeemResult && (
            <div className="result-card">
              <strong>Redemption status: {redeemResult.status}</strong>
              <p>{redeemResult.nextStep}</p>
              {redeemResult.activationUrl && (
                <p>
                  Activation link:
                  {" "}
                  <a href={redeemResult.activationUrl} target="_blank" rel="noreferrer">
                    Open Okta activation
                  </a>
                </p>
              )}
            </div>
          )}
        </article>
      </section>

      <section className="panel">
        <div className="section-heading">
          <div>
            <p className="eyebrow">Demo state</p>
            <h2>Cases currently waiting on clients</h2>
          </div>
          <button type="button" className="secondary" onClick={loadCases}>
            Refresh
          </button>
        </div>

        <div className="case-list">
          {cases.map((item) => (
            <article className="case-card" key={item.caseId}>
              <p className="case-name">{item.clientName}</p>
              <p>{item.clientEmail}</p>
              <p>Status: {item.status}</p>
              <p>RM: {item.relationshipManager}</p>
              <p>
                Path:{" "}
                {item.hasExistingAccount
                  ? "Existing Okta account"
                  : "New registration"}
              </p>
            </article>
          ))}
        </div>
      </section>

      {error && <p className="error-banner">{error}</p>}
    </main>
  );
}
