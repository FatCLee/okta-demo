using api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace api.Data;

public sealed class KycDbContext(DbContextOptions<KycDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    public DbSet<UserRole> Roles => Set<UserRole>();

    public DbSet<UserRoleAssignment> RoleAssignments => Set<UserRoleAssignment>();

    public DbSet<IdentityProviderAccount> IdentityProviderAccounts => Set<IdentityProviderAccount>();

    public DbSet<KycCase> Cases => Set<KycCase>();

    public DbSet<ClientInvite> ClientInvites => Set<ClientInvite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.Property(user => user.DisplayName).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(role => role.Id);
            entity.HasIndex(role => role.Code).IsUnique();
            entity.Property(role => role.Code).HasMaxLength(40).IsRequired();
            entity.Property(role => role.Name).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<UserRoleAssignment>(entity =>
        {
            entity.HasKey(assignment => assignment.Id);
            entity.HasIndex(assignment => new { assignment.UserId, assignment.RoleId }).IsUnique();
            entity.HasOne(assignment => assignment.User)
                .WithMany(user => user.RoleAssignments)
                .HasForeignKey(assignment => assignment.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(assignment => assignment.Role)
                .WithMany(role => role.Assignments)
                .HasForeignKey(assignment => assignment.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IdentityProviderAccount>(entity =>
        {
            entity.HasKey(account => account.Id);
            entity.HasIndex(account => new { account.Provider, account.Subject }).IsUnique();
            entity.Property(account => account.Provider).HasMaxLength(80).IsRequired();
            entity.Property(account => account.Subject).HasMaxLength(200).IsRequired();
            entity.Property(account => account.TenantId).HasMaxLength(120);
            entity.HasOne(account => account.User)
                .WithMany(user => user.IdentityProviderAccounts)
                .HasForeignKey(account => account.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KycCase>(entity =>
        {
            entity.HasKey(kycCase => kycCase.Id);
            entity.Property(kycCase => kycCase.Status).HasMaxLength(80).IsRequired();
            entity.HasOne(kycCase => kycCase.ClientUser)
                .WithMany(user => user.ClientCases)
                .HasForeignKey(kycCase => kycCase.ClientUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(kycCase => kycCase.RelationshipManagerUser)
                .WithMany(user => user.RelationshipManagerCases)
                .HasForeignKey(kycCase => kycCase.RelationshipManagerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClientInvite>(entity =>
        {
            entity.HasKey(invite => invite.Id);
            entity.HasIndex(invite => invite.InvitationCode).IsUnique();
            entity.Property(invite => invite.InvitationCode).HasMaxLength(40).IsRequired();
            entity.Property(invite => invite.LoginHint).HasMaxLength(400).IsRequired();
            entity.Property(invite => invite.DeliveryChannel).HasMaxLength(40).IsRequired();
            entity.Property(invite => invite.IdentityProvider).HasMaxLength(80).IsRequired();
            entity.Property(invite => invite.Status).HasMaxLength(80).IsRequired();
            entity.Property(invite => invite.ActivationUrl).HasMaxLength(1000);
            entity.Property(invite => invite.IdentityProviderUserId).HasMaxLength(200);
            entity.HasOne(invite => invite.Case)
                .WithMany(kycCase => kycCase.Invites)
                .HasForeignKey(invite => invite.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
