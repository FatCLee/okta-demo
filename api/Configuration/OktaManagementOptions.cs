namespace api.Configuration;

public sealed class OktaManagementOptions
{
    public const string SectionName = "OktaManagement";

    public string BaseUrl { get; init; } = string.Empty;

    public string ApiToken { get; init; } = string.Empty;
}
