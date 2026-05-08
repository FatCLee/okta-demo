namespace api.Data.Entities;

public sealed class KycCase
{
    public Guid Id { get; set; }

    public Guid ClientUserId { get; set; }

    public ApplicationUser ClientUser { get; set; } = null!;

    public Guid RelationshipManagerUserId { get; set; }

    public ApplicationUser RelationshipManagerUser { get; set; } = null!;

    public string Status { get; set; } = string.Empty;

    public bool ClientHadExistingAccount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ClientInvite> Invites { get; set; } = [];
}
