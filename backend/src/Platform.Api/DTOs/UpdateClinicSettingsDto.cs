namespace Platform.Api.DTOs;

public sealed record UpdateClinicSettingsDto(
    string Name,
    string Street,
    string City,
    string Governorate,
    string MainPhone,
    IReadOnlyList<string>? ExtraPhones,
    string? ContactEmail,
    string? Slug,
    string? TimeZone,
    string? Currency,
    string? RoundingRule,
    Guid? LogoAssetId);

