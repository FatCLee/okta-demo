using System.ComponentModel.DataAnnotations;

namespace api.Models;

public sealed class InviteRequest
{
    [Required]
    [EmailAddress]
    public string ClientEmail { get; init; } = string.Empty;

    [Required]
    public string ClientName { get; init; } = string.Empty;

    public bool HasExistingAccount { get; init; }

    [Required]
    public string RelationshipManager { get; init; } = string.Empty;
}
