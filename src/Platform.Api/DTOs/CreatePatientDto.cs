namespace Platform.Api.DTOs;

public sealed record CreatePatientDto(
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

