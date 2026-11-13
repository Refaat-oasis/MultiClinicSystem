namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to create or update an appointment category.
/// </summary>
public sealed class UpsertAppointmentCategoryDto
{
    public string SpecialtyCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int DefaultDurationMinutes { get; set; } = 30;
    public decimal DefaultPriceAmount { get; set; }
    public string? DefaultPriceCurrency { get; set; }
    public bool BookableOnline { get; set; }
    public bool IsActive { get; set; } = true;
}

