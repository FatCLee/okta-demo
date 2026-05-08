namespace api.Models;

public sealed record InviteRecord(
    Guid InviteId,
    Guid CaseId,
    string ClientEmail,
    string ClientName,
    string InvitationCode,
    string LoginHint,
    string DeliveryChannel,
    string IdentityProvider,
    string Status,
    string? ActivationUrl = null,
    string? OktaUserId = null);
