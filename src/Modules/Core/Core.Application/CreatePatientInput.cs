namespace Core.Application;

/// <summary>
/// Input model for creating or updating a patient profile.
/// </summary>
public sealed record CreatePatientInput(
    Guid? ClientPatientId,
    string FullName,
    string Phone,
    string? SecondaryPhone,
    DateOnly? DateOfBirth,
    int? ApproximateAgeYears,
    string Gender,
    string? NationalId,
    string? Address,
    Guid? AssignedDoctorId,
    string? PrimarySpecialtyCode,
    bool ConfirmDuplicateCreation,
    Guid? OperationId);

