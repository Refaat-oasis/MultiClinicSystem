namespace Platform.Api.DTOs;

public sealed record OnboardingDetailsDto(
    string Name,
    string Street,
    string City,
    string Governorate,
    string MainPhone,
    IReadOnlyList<string>? ExtraPhones,
    string? ContactEmail,
    Guid? LogoAssetId);

