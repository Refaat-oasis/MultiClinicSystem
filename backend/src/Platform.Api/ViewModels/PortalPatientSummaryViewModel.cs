namespace Platform.Api.ViewModels;

public sealed record PortalPatientSummaryViewModel(
    Guid PatientId,
    string FileNumber,
    string FullName,
    int? AgeYears,
    DateOnly? DateOfBirth,
    string Gender,
    string? BloodType,
    string? Email,
    string? GuardianName);

