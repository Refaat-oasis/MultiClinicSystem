namespace Core.Application;

public sealed record PortalPatientSummaryResult(
    Guid PatientId,
    string FileNumber,
    string FullName,
    int? AgeYears,
    DateOnly? DateOfBirth,
    string Gender,
    string? BloodType,
    string? Email,
    string? GuardianName);

