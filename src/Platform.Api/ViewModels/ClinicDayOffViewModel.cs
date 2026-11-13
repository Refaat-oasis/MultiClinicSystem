namespace Platform.Api.ViewModels;

public sealed record ClinicDayOffViewModel(
    Guid Id,
    Guid ClinicId,
    Guid? DoctorId,
    string StartDate,
    string EndDate,
    string ReasonAr,
    string ReasonEn,
    DateTimeOffset CreatedAtUtc);
