using api.Data;
using api.Data.Entities;
using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Infrastructure;

public sealed class DemoDataStore(KycDbContext dbContext, OktaManagementClient oktaManagementClient)
{
    private const string ClientRoleCode = "client";
    private const string RelationshipManagerRoleCode = "rm";

    private readonly KycDbContext _dbContext = dbContext;
    private readonly OktaManagementClient _oktaManagementClient = oktaManagementClient;

    public async Task<IReadOnlyList<CaseSummary>> GetCasesAsync(CancellationToken cancellationToken) =>
        await _dbContext.Cases
            .AsNoTracking()
            .Include(kycCase => kycCase.ClientUser)
            .Include(kycCase => kycCase.RelationshipManagerUser)
            .OrderBy(kycCase => kycCase.ClientUser.DisplayName)
            .Select(kycCase => new CaseSummary(
                kycCase.Id,
                kycCase.ClientUser.Email,
                kycCase.ClientUser.DisplayName,
                kycCase.Status,
                kycCase.ClientHadExistingAccount,
                kycCase.RelationshipManagerUser.DisplayName))
            .ToListAsync(cancellationToken);

    public async Task<InviteRecord> CreateInviteAsync(InviteRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var normalizedClientEmail = request.ClientEmail.Trim().ToLowerInvariant();
        var clientName = request.ClientName.Trim();
        var relationshipManagerName = request.RelationshipManager.Trim();
        var invitationCode = $"OKTA-{Random.Shared.Next(100000, 999999)}";
        OktaActivationResult? activation = null;

        if (!request.HasExistingAccount && _oktaManagementClient.IsConfigured())
        {
            activation = await _oktaManagementClient.CreateUserAndActivationAsync(
                normalizedClientEmail,
                clientName,
                cancellationToken);
        }

        var client = await FindOrCreateUserAsync(
            normalizedClientEmail,
            clientName,
            ClientRoleCode,
            "Okta",
            activation?.OktaUserId ?? normalizedClientEmail,
            tenantId: null,
            now,
            cancellationToken);

        var relationshipManager = await FindOrCreateUserAsync(
            $"{relationshipManagerName.Replace(' ', '.').ToLowerInvariant()}@demo-bank.test",
            relationshipManagerName,
            RelationshipManagerRoleCode,
            "Microsoft Entra ID",
            relationshipManagerName,
            tenantId: null,
            now,
            cancellationToken);

        var kycCase = new KycCase
        {
            Id = Guid.NewGuid(),
            ClientUser = client,
            RelationshipManagerUser = relationshipManager,
            Status = "Invitation sent",
            ClientHadExistingAccount = request.HasExistingAccount,
            CreatedAt = now,
            UpdatedAt = now
        };

        var invite = new ClientInvite
        {
            Id = Guid.NewGuid(),
            Case = kycCase,
            InvitationCode = invitationCode,
            LoginHint = request.HasExistingAccount
                ? "Use your existing Okta account, then verify with OTP."
                : activation is null
                    ? "Okta activation is not configured yet. Use the code as a local placeholder."
                    : "Open the Okta activation link to finish account setup.",
            DeliveryChannel = "email",
            IdentityProvider = "Okta",
            Status = activation is null && !request.HasExistingAccount ? "Pending Okta setup" : "Sent",
            ActivationUrl = activation?.ActivationUrl,
            IdentityProviderUserId = activation?.OktaUserId,
            CreatedAt = now
        };

        _dbContext.AddRange(kycCase, invite);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new InviteRecord(
            invite.Id,
            kycCase.Id,
            client.Email,
            client.DisplayName,
            invite.InvitationCode,
            invite.LoginHint,
            invite.DeliveryChannel,
            invite.IdentityProvider,
            invite.Status,
            invite.ActivationUrl,
            invite.IdentityProviderUserId);
    }

    public async Task<LoginResult> StartLoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedUserType = request.UserType.Trim().ToLowerInvariant();

        if (normalizedUserType == RelationshipManagerRoleCode)
        {
            return new LoginResult(
                "RM",
                "Microsoft Entra ID",
                "redirect",
                "Send the RM through the Entra ID sign-in flow.");
        }

        var pendingInvite = await _dbContext.ClientInvites
            .AsNoTracking()
            .Include(invite => invite.Case)
            .ThenInclude(kycCase => kycCase.ClientUser)
            .Where(invite => invite.Case.ClientUser.Email == normalizedEmail)
            .OrderBy(invite => invite.CreatedAt)
            .LastOrDefaultAsync(cancellationToken);

        return pendingInvite is null
            ? new LoginResult(
                "Client",
                "Okta",
                "not-found",
                "No invite found. RM should create a placeholder account first.")
            : new LoginResult(
                "Client",
                "Okta",
                "invite-found",
                "Redirect client to Okta sign-in or invite redemption.",
                pendingInvite.InvitationCode,
                pendingInvite.ActivationUrl);
    }

    public async Task<LoginResult> RedeemInviteAsync(RedeemInviteRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedCode = request.InvitationCode.Trim().ToUpperInvariant();

        var invite = await _dbContext.ClientInvites
            .Include(item => item.Case)
            .ThenInclude(kycCase => kycCase.ClientUser)
            .LastOrDefaultAsync(item =>
                item.Case.ClientUser.Email == normalizedEmail &&
                item.InvitationCode == normalizedCode,
                cancellationToken);

        if (invite is null)
        {
            return new LoginResult(
                "Client",
                "Okta",
                "invalid-code",
                "The invitation code or email does not match our records.");
        }

        invite.RedeemedAt = DateTimeOffset.UtcNow;
        invite.Status = invite.ActivationUrl is null ? "Redeemed" : "Activation ready";
        invite.Case.Status = "Client onboarding";
        invite.Case.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LoginResult(
            "Client",
            "Okta",
            invite.ActivationUrl is null ? "redeemed" : "activation-ready",
            invite.ActivationUrl is null
                ? "Client can finish registration and continue the KYC case."
                : "Open the Okta activation link to complete the real activation flow.",
            invite.InvitationCode,
            invite.ActivationUrl);
    }

    private async Task<ApplicationUser> FindOrCreateUserAsync(
        string email,
        string displayName,
        string roleCode,
        string identityProvider,
        string identityProviderSubject,
        string? tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .Include(item => item.RoleAssignments)
            .Include(item => item.IdentityProviderAccounts)
            .FirstOrDefaultAsync(item => item.Email == email, cancellationToken);

        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                DisplayName = displayName,
                CreatedAt = now
            };

            _dbContext.Users.Add(user);
        }

        var role = await _dbContext.Roles.SingleAsync(item => item.Code == roleCode, cancellationToken);

        if (user.RoleAssignments.All(item => item.RoleId != role.Id))
        {
            user.RoleAssignments.Add(new UserRoleAssignment
            {
                Id = Guid.NewGuid(),
                User = user,
                Role = role,
                AssignedAt = now
            });
        }

        if (user.IdentityProviderAccounts.All(item =>
                item.Provider != identityProvider ||
                item.Subject != identityProviderSubject))
        {
            user.IdentityProviderAccounts.Add(new IdentityProviderAccount
            {
                Id = Guid.NewGuid(),
                User = user,
                Provider = identityProvider,
                Subject = identityProviderSubject,
                TenantId = tenantId,
                LinkedAt = now
            });
        }

        return user;
    }
}
