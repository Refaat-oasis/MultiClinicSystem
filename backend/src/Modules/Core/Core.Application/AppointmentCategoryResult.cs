namespace Core.Application;

/// <summary>
/// Read model for an appointment category.
/// </summary>
public sealed record AppointmentCategoryResult(
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

