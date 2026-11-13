namespace Core.Domain;

/// <summary>
/// Tracks client devices registered per user, supporting approval and revocation.
/// </summary>
public sealed class AppDevice
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string DeviceId { get; private set; }
    public string DeviceName { get; private set; }
    public bool IsApproved { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private AppDevice()
    {
        DeviceId = string.Empty;
        DeviceName = string.Empty;
    }

    public AppDevice(
        Guid id,
        Guid userId,
        string deviceId,
        string deviceName,
        DateTimeOffset createdAtUtc,
        bool isApproved = true)
    {
        Id = id;
        UserId = userId;
        DeviceId = deviceId ?? string.Empty;
        DeviceName = deviceName ?? string.Empty;
        CreatedAtUtc = createdAtUtc;
        IsApproved = isApproved;
        ApprovedAtUtc = isApproved ? createdAtUtc : null;
        IsRevoked = false;
    }

    public void Revoke(DateTimeOffset revokedAtUtc)
    {
        IsRevoked = true;
        RevokedAtUtc = revokedAtUtc;
    }

    public void Approve(DateTimeOffset approvedAtUtc)
    {
        IsApproved = true;
        IsRevoked = false;
        ApprovedAtUtc = approvedAtUtc;
        RevokedAtUtc = null;
    }

    public void RequireApproval()
    {
        IsApproved = false;
        ApprovedAtUtc = null;
    }
}
