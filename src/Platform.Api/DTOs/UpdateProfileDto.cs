namespace Platform.Api.DTOs;

public sealed record UpdateProfileDto(
    string FullName,
    string? PhoneNumber,
    string? Locale,
    string? Theme,
    Guid? AvatarAssetId);

