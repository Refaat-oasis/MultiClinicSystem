namespace Platform.Api.ViewModels;

public sealed record SpecialtyCatalogItemViewModel(
    string CatalogCode,
    string NameAr,
    string NameEn,
    bool HasDentalPlugin,
    string DefaultFormSchemaJson,
    string DefaultAppointmentCategoriesJson);

