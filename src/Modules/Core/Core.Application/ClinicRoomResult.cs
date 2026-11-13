namespace Core.Application;

/// <summary>
/// Read model for a clinic consultation/procedure room.
/// </summary>
public sealed record ClinicRoomResult(
    Guid Id,
    Guid ClinicId,
    string Code,
    string NameAr,
    string NameEn,
    string? SpecialtyCode,
    string? FloorOrLocation,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

