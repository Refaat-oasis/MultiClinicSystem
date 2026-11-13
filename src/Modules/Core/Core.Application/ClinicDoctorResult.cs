namespace Core.Application;

/// <summary>
/// Read model for a clinic doctor profile with assigned specialties and rooms.
/// </summary>
public sealed record ClinicDoctorResult(
    Guid Id,
    Guid ClinicId,
    Guid? UserId,
    string FullName,
    string TitleAr,
    string TitleEn,
    IReadOnlyList<string> SpecialtyCodes,
    IReadOnlyList<Guid> AssignedRoomIds,
    int SlotDurationMinutes,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);
