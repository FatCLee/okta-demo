using System.ComponentModel.DataAnnotations;

namespace api.Models;

public sealed class RedeemInviteRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string InvitationCode { get; init; } = string.Empty;
}
