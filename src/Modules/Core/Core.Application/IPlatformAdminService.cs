using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Super Admin operations for managing owners, clinics, step-up actions, inspect mode, audit logs, and platform dashboard.
/// </summary>
public interface IPlatformAdminService
{
    Task<Result<OwnerSummaryResult>> CreateOwnerAsync(
        Guid adminId,
        string fullName,
        string email,
        string? phone,
        int? maxClinics,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<OwnerSummaryResult>>> ListOwnersAsync(
        string? search,
        CancellationToken cancellationToken = default);

    Task<Result<OwnerDetailResult>> GetOwnerDetailAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default);

    Task<Result> ResendOwnerInvitationCodeAsync(
        Guid adminId,
        Guid ownerId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> DeactivateOwnerAsync(
        Guid adminId,
        Guid ownerId,
        string? stepUpPassword,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> ReactivateOwnerAsync(
        Guid adminId,
        Guid ownerId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> UpdateOwnerQuotaAsync(
        Guid adminId,
        Guid ownerId,
        int? maxClinics,
        string? stepUpPassword,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<OwnerSummaryResult>>> GetClinicDirectoryAsync(
        string? search,
        CancellationToken cancellationToken = default);

    Task<Result> UpdateClinicStatusAsync(
        Guid adminId,
        Guid clinicId,
        string status,
        DateTimeOffset? subscriptionExpiryUtc,
        int? graceDays,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> MoveClinicOwnerAsync(
        Guid adminId,
        Guid clinicId,
        Guid targetOwnerId,
        string? stepUpPassword,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<InspectClinicResult>> InspectClinicAsync(
        Guid adminId,
        Guid clinicId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PlatformDashboardResult>> GetDashboardAsync(
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AuditLogItemResult>>> GetAuditLogsAsync(
        Guid? clinicId,
        string? actionFilter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PatientAccessLogItemResult>>> GetClinicAccessLogsForOwnerAsync(
        Guid ownerUserId,
        Guid? clinicId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ClinicModuleStateResult>>> GetClinicModulesAsync(
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ClinicModuleStateResult>>> UpdateClinicModulesAsync(
        Guid adminId,
        Guid clinicId,
        IReadOnlyList<ModuleToggleInput> modules,
        string? stepUpPassword,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);
}

