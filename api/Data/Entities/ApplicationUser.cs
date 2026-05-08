namespace api.Data.Entities;

public sealed class ApplicationUser
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<UserRoleAssignment> RoleAssignments { get; set; } = [];

    public ICollection<IdentityProviderAccount> IdentityProviderAccounts { get; set; } = [];

    public ICollection<KycCase> ClientCases { get; set; } = [];

    public ICollection<KycCase> RelationshipManagerCases { get; set; } = [];
}
