namespace api.Data.Entities;

public sealed class IdentityProviderAccount
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public string Provider { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string? TenantId { get; set; }

    public DateTimeOffset LinkedAt { get; set; }
}
