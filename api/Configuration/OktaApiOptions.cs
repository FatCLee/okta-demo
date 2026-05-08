namespace api.Configuration;

public sealed class OktaApiOptions
{
    public const string SectionName = "Okta";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = "api://default";
}
