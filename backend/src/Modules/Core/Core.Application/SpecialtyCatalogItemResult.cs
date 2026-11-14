namespace Core.Application;

/// <summary>
/// Built-in medical specialty definition with default clinical form schema and appointment categories.
/// </summary>
public sealed record SpecialtyCatalogItemResult(
    string CatalogCode,
    string NameAr,
    string NameEn,
    bool HasDentalPlugin,
    string DefaultFormSchemaJson,
    string DefaultAppointmentCategoriesJson);

