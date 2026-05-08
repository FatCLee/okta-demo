namespace api.Data.Entities;

public sealed class UserRole
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ICollection<UserRoleAssignment> Assignments { get; set; } = [];
}
