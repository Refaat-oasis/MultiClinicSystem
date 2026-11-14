namespace Platform.Api.DTOs;

public sealed record CheckPatientDuplicatesDto(
    string FullName,
    string Phone,
    Guid? ExcludePatientId);

