namespace Core.Application;

/// <summary>
/// Summary row for clinic patient search and list screens.
/// </summary>
public sealed record PatientSummaryItemResult(
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

