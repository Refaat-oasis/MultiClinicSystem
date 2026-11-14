namespace Platform.Api.ViewModels;

public sealed record PortalBookableDoctorItemViewModel(
    Guid DoctorId,
    string? DisplayName,
    string SpecialtyCode);
