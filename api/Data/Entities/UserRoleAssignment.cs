namespace api.Data.Entities;

public sealed class UserRoleAssignment
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public Guid RoleId { get; set; }

    public UserRole Role { get; set; } = null!;

    public DateTimeOffset AssignedAt { get; set; }
}
