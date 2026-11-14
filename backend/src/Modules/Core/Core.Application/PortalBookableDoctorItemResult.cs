namespace Core.Application;

public sealed record PortalBookableDoctorItemResult(
    Guid DoctorId,
    string? DisplayName,
    string SpecialtyCode);
