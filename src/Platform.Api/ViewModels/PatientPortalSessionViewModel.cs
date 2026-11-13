namespace Platform.Api.ViewModels;

public sealed record PatientPortalSessionViewModel(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    Guid PatientAccountId,
    Guid ClinicId,
    Guid PatientId,
    string FullName,
    string ClinicSlug,
    string? Email);

