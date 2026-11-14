namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel for an appointment category.
/// </summary>
public sealed record AppointmentCategoryViewModel(
    Guid Id,
    Guid ClinicId,
    string SpecialtyCode,
    string NameAr,
    string NameEn,
    int DefaultDurationMinutes,
    decimal DefaultPriceAmount,
    string DefaultPriceCurrency,
    bool BookableOnline,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

