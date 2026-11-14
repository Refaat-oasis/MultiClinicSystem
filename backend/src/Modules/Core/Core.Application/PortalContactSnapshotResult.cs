namespace Core.Application;

public sealed record PortalContactSnapshotResult(
    Guid PatientId,
    string FileNumber,
    string FullName,
    DateOnly? DateOfBirth,
    string? Email,
    string? GuardianName,
    string? GuardianEmail);

