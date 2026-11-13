namespace Platform.Api.ViewModels;

public sealed record ClinicSpecialtyItemViewModel(
    Guid Id,
    string CatalogCode,
    string NameAr,
    string NameEn,
    bool HasDentalPlugin,
    string DefaultFormSchemaJson,
    string DefaultAppointmentCategoriesJson);

