namespace Platform.Api.ViewModels;

public sealed record ClinicDoctorViewModel(
    Guid Id,
    Guid ClinicId,
    Guid? UserId,
    string FullName,
    string TitleAr,
    string TitleEn,
    IReadOnlyList<string> SpecialtyCodes,
    IReadOnlyList<Guid> AssignedRoomIds,
    int SlotDurationMinutes,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

