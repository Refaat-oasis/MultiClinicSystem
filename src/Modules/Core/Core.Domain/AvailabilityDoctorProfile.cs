namespace Core.Domain;

/// <summary>
/// Active doctor profile used by the pure availability calculator.
/// </summary>
public sealed record AvailabilityDoctorProfile(
    Guid DoctorId,
    Guid? RoomId,
    string SpecialtyCode);

