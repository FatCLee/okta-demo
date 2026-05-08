/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_OKTA_ISSUER?: string;
  readonly VITE_OKTA_CLIENT_ID?: string;
  readonly VITE_OKTA_REDIRECT_URI?: string;
  readonly VITE_OKTA_SCOPES?: string;
  readonly VITE_ENTRA_TENANT_ID?: string;
  readonly VITE_ENTRA_CLIENT_ID?: string;
  readonly VITE_ENTRA_REDIRECT_URI?: string;
  readonly VITE_ENTRA_API_SCOPE?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
