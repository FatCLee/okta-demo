namespace api.Configuration;

public sealed class EntraApiOptions
{
    public const string SectionName = "Entra";

    public string TenantId { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string Authority => string.IsNullOrWhiteSpace(TenantId)
        ? string.Empty
        : $"https://login.microsoftonline.com/{TenantId}/v2.0";
}
