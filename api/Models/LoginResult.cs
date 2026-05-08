namespace api.Models;

public sealed record LoginResult(
    string UserType,
    string IdentityProvider,
    string Status,
    string NextStep,
    string? InvitationCode = null,
    string? ActivationUrl = null);
