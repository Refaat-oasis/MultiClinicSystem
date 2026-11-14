namespace Platform.Api.DTOs;

public sealed record CreateClinicDto(
    string Name,
    string? TimeZone,
    string? Currency,
    string? RoundingRule,
    string? DeviceId);

