using api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace api.Data;

public static class DatabaseSeeder
{
    private static readonly Guid ClientRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RelationshipManagerRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ExistingClientId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid DemoRelationshipManagerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ExistingCaseId = Guid.Parse("fb04fdad-c269-4b84-9173-2d7cce9ef8fd");
    private static readonly Guid ExistingInviteId = Guid.Parse("0c3f5eb7-7d41-4a5d-9f96-cb9c9e69fc33");

    public static async Task EnsureSeededAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KycDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        if (await dbContext.Roles.AnyAsync())
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        var clientRole = new UserRole
        {
            Id = ClientRoleId,
            Code = "client",
            Name = "Client"
        };

        var relationshipManagerRole = new UserRole
        {
            Id = RelationshipManagerRoleId,
            Code = "rm",
            Name = "Relationship Manager"
        };

        var existingClient = new ApplicationUser
        {
            Id = ExistingClientId,
            Email = "existing.client@demo-bank.test",
            DisplayName = "Existing Client",
            CreatedAt = now
        };

        var demoRelationshipManager = new ApplicationUser
        {
            Id = DemoRelationshipManagerId,
            Email = "jamie.rm@demo-bank.test",
            DisplayName = "Jamie RM",
            CreatedAt = now
        };

        dbContext.AddRange(
            clientRole,
            relationshipManagerRole,
            existingClient,
            demoRelationshipManager,
            new UserRoleAssignment
            {
                Id = Guid.NewGuid(),
                User = existingClient,
                Role = clientRole,
                AssignedAt = now
            },
            new UserRoleAssignment
            {
                Id = Guid.NewGuid(),
                User = demoRelationshipManager,
                Role = relationshipManagerRole,
                AssignedAt = now
            },
            new IdentityProviderAccount
            {
                Id = Guid.NewGuid(),
                User = existingClient,
                Provider = "Okta",
                Subject = "existing.client@demo-bank.test",
                LinkedAt = now
            },
            new IdentityProviderAccount
            {
                Id = Guid.NewGuid(),
                User = demoRelationshipManager,
                Provider = "Microsoft Entra ID",
                Subject = "jamie.rm@demo-bank.test",
                LinkedAt = now
            });

        var existingCase = new KycCase
        {
            Id = ExistingCaseId,
            ClientUser = existingClient,
            RelationshipManagerUser = demoRelationshipManager,
            Status = "Waiting for client",
            ClientHadExistingAccount = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var existingInvite = new ClientInvite
        {
            Id = ExistingInviteId,
            Case = existingCase,
            InvitationCode = "OKTA-482193",
            LoginHint = "Use your existing Okta account, then verify with OTP.",
            DeliveryChannel = "email",
            IdentityProvider = "Okta",
            Status = "Sent",
            CreatedAt = now
        };

        dbContext.AddRange(existingCase, existingInvite);

        await dbContext.SaveChangesAsync();
    }
}
