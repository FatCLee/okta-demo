import { OktaAuth, type OktaAuthOptions } from "@okta/okta-auth-js";

function readScopes(): string[] {
  const rawScopes = import.meta.env.VITE_OKTA_SCOPES ?? "openid,profile,email";

  return rawScopes
    .split(",")
    .map((scope) => scope.trim())
    .filter(Boolean);
}

export function getOktaConfig(): OktaAuthOptions {
  return {
    issuer: import.meta.env.VITE_OKTA_ISSUER ?? "",
    clientId: import.meta.env.VITE_OKTA_CLIENT_ID ?? "",
    redirectUri:
      import.meta.env.VITE_OKTA_REDIRECT_URI ??
      `${window.location.origin}/login/callback`,
    scopes: readScopes(),
    pkce: true,
  };
}

export function hasOktaConfig(): boolean {
  const config = getOktaConfig();
  return Boolean(config.issuer && config.clientId && config.redirectUri);
}

export const oktaAuth = new OktaAuth(getOktaConfig());
