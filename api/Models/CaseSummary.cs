namespace api.Models;

public sealed record CaseSummary(
    Guid CaseId,
    string ClientEmail,
    string ClientName,
    string Status,
    bool HasExistingAccount,
    string RelationshipManager);
