namespace api.Data.Entities;

public sealed class ClientInvite
{
    public Guid Id { get; set; }

    public Guid CaseId { get; set; }

    public KycCase Case { get; set; } = null!;

    public string InvitationCode { get; set; } = string.Empty;

    public string LoginHint { get; set; } = string.Empty;

    public string DeliveryChannel { get; set; } = string.Empty;

    public string IdentityProvider { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? ActivationUrl { get; set; }

    public string? IdentityProviderUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RedeemedAt { get; set; }
}
