namespace Core.Application;

/// <summary>
/// Represents a configured specialty belonging to a clinic.
/// </summary>
public sealed record ClinicSpecialtyItemResult(
    Guid Id,
    string CatalogCode,
    string NameAr,
    string NameEn,
    bool HasDentalPlugin,
    string DefaultFormSchemaJson,
    string DefaultAppointmentCategoriesJson);

