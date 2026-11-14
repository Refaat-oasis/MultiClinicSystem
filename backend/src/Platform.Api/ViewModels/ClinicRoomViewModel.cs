namespace Platform.Api.ViewModels;

public sealed record ClinicRoomViewModel(
    Guid Id,
    Guid ClinicId,
    string RoomCode,
    string NameAr,
    string NameEn,
    string? SpecialtyCode,
    string? FloorOrLocation,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);
