using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Manages clinic physical structure (rooms, doctors, working hours, days off), atomic number blocks, and owner audit viewer.
/// </summary>
public interface IClinicStructureService
{
    Task<Result<IReadOnlyList<ClinicRoomResult>>> ListRoomsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicRoomResult>> CreateRoomAsync(
        Guid userId,
        Guid clinicId,
        string code,
        string nameAr,
        string nameEn,
        string? specialtyCode,
        string? floorOrLocation,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicRoomResult>> UpdateRoomAsync(
        Guid userId,
        Guid clinicId,
        Guid roomId,
        string code,
        string nameAr,
        string nameEn,
        string? specialtyCode,
        string? floorOrLocation,
        bool isActive,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> VoidRoomAsync(
        Guid userId,
        Guid clinicId,
        Guid roomId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ClinicDoctorResult>>> ListDoctorsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicDoctorResult>> CreateDoctorAsync(
        Guid userId,
        Guid clinicId,
        Guid? staffUserId,
        string fullName,
        string titleAr,
        string titleEn,
        IReadOnlyList<string> specialtyCodes,
        IReadOnlyList<Guid> assignedRoomIds,
        int slotDurationMinutes,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicDoctorResult>> UpdateDoctorAsync(
        Guid userId,
        Guid clinicId,
        Guid doctorId,
        Guid? staffUserId,
        string fullName,
        string titleAr,
        string titleEn,
        IReadOnlyList<string> specialtyCodes,
        IReadOnlyList<Guid> assignedRoomIds,
        int slotDurationMinutes,
        bool isActive,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> VoidDoctorAsync(
        Guid userId,
        Guid clinicId,
        Guid doctorId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ClinicWorkingHourResult>>> GetWorkingHoursAsync(
        Guid userId,
        Guid clinicId,
        Guid? doctorId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ClinicWorkingHourResult>>> SaveWorkingHoursAsync(
        Guid userId,
        Guid clinicId,
        Guid? doctorId,
        IReadOnlyList<WorkingHourInput> items,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ClinicDayOffResult>>> ListDaysOffAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicDayOffResult>> CreateDayOffAsync(
        Guid userId,
        Guid clinicId,
        Guid? doctorId,
        string startDate,
        string endDate,
        string reasonAr,
        string reasonEn,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> VoidDayOffAsync(
        Guid userId,
        Guid clinicId,
        Guid dayOffId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<NumberBlockResult>>> ListNumberBlocksAsync(
        Guid userId,
        Guid clinicId,
        string? kind,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<NumberBlockResult>> ReserveNumberBlockAsync(
        Guid userId,
        Guid clinicId,
        string targetDeviceId,
        string kind,
        int? blockSize,
        string? callerDeviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AuditLogItemResult>>> GetClinicAuditLogsAsync(
        Guid userId,
        Guid clinicId,
        string? actionFilter,
        int page,
        int pageSize,
        string? deviceId,
        CancellationToken cancellationToken = default);
}

