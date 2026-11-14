namespace Platform.Api.ViewModels;

public sealed record PatientSummaryItemViewModel(
    Guid Id,
    string FileNumber,
    string FullName,
    string Phone,
    string Gender,
    DateOnly? DateOfBirth,
    int? AgeYears,
    Guid? AssignedDoctorId,
    string? PrimarySpecialtyCode,
    int AllergyCount,
    bool HasSevereAllergy,
    DateTimeOffset CreatedAtUtc);

