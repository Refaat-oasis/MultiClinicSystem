namespace Core.Application;

public sealed record PatientPortalSessionResult(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    Guid PatientAccountId,
    Guid ClinicId,
    Guid PatientId,
    string FullName,
    string ClinicSlug,
    string? Email);

