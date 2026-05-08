using System.ComponentModel.DataAnnotations;

namespace api.Models;

public sealed class LoginRequest
{
    [Required]
    public string UserType { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;
}
