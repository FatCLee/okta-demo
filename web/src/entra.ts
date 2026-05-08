import { PublicClientApplication, type Configuration } from "@azure/msal-browser";

export function getEntraConfig(): Configuration {
  const tenantId = import.meta.env.VITE_ENTRA_TENANT_ID ?? "";

  return {
    auth: {
      clientId: import.meta.env.VITE_ENTRA_CLIENT_ID ?? "",
      authority: tenantId
        ? `https://login.microsoftonline.com/${tenantId}`
        : "",
      redirectUri:
        import.meta.env.VITE_ENTRA_REDIRECT_URI ??
        `${window.location.origin}/rm/callback`,
      postLogoutRedirectUri: window.location.origin,
    },
    cache: {
      cacheLocation: "sessionStorage",
    },
  };
}

export function getEntraApiScopes(): string[] {
  const apiScope = import.meta.env.VITE_ENTRA_API_SCOPE ?? "";
  return apiScope ? [apiScope] : [];
}

export function hasEntraConfig(): boolean {
  const config = getEntraConfig();
  return Boolean(config.auth.clientId && config.auth.authority);
}

export const msalInstance = new PublicClientApplication(getEntraConfig());
