namespace api.Infrastructure;

public sealed record OktaActivationResult(
    string OktaUserId,
    string ActivationToken,
    string ActivationUrl);
