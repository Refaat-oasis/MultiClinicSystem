namespace Core.Domain;

/// <summary>
/// Represents a medical clinic tenant within the multi-clinic platform.
/// </summary>
public sealed class Clinic
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public Guid? OwnerId { get; private set; }
    public string Status { get; private set; }
    public DateTimeOffset? SubscriptionExpiryUtc { get; private set; }
    public int GraceDays { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Clinic()
    {
        Name = string.Empty;
        Slug = string.Empty;
        Status = "PendingOnboarding";
        GraceDays = 14;
    }

    public Clinic(
        Guid id,
        string name,
        string slug,
        DateTimeOffset createdAtUtc,
        Guid? ownerId = null,
        string status = "PendingOnboarding",
        DateTimeOffset? subscriptionExpiryUtc = null,
        int graceDays = 14)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Clinic name cannot be empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Clinic slug cannot be empty.", nameof(slug));
        }

        Id = id;
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        OwnerId = ownerId;
        Status = status;
        SubscriptionExpiryUtc = subscriptionExpiryUtc;
        GraceDays = graceDays;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public void Deactivate()
    {
        IsActive = false;
        Status = "Suspended";
    }

    public void Activate()
    {
        IsActive = true;
        Status = "Active";
    }

    public void SetStatus(string status)
    {
        Status = status;
    }

    public void UpdateSubscription(DateTimeOffset? expiryUtc, int graceDays)
    {
        SubscriptionExpiryUtc = expiryUtc;
        GraceDays = graceDays;
    }

    public void MoveToOwner(Guid newOwnerId)
    {
        if (newOwnerId == Guid.Empty)
        {
            throw new ArgumentException("Target owner ID cannot be empty.", nameof(newOwnerId));
        }

        OwnerId = newOwnerId;
    }
}
