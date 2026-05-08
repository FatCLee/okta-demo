const oktaAccountSettingsPath = "/account-settings/home";
const entraPasswordChangeUrl =
  "https://mysignins.microsoft.com/security-info/password/change?tileType=ChangePassword";

export function getOktaAccountSettingsUrl(): string {
  const issuer = import.meta.env.VITE_OKTA_ISSUER ?? "";

  if (!issuer) {
    return "";
  }

  const orgUrl = issuer.split("/oauth2/")[0]?.replace(/\/$/, "");
  return orgUrl ? `${orgUrl}${oktaAccountSettingsPath}` : "";
}

export function getEntraPasswordChangeUrl(): string {
  return entraPasswordChangeUrl;
}
