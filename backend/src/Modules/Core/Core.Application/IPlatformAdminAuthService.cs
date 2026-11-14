using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Authentication, forced enrollment, step-up verification, and break-glass reset service for Platform Super Admins.
/// </summary>
public interface IPlatformAdminAuthService
{
    Task<Result<PlatformAdminLoginResult>> LoginAsync(
        string email,
        string password,
        string? hourlyCode,
        string? recoveryKey,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PlatformAdminEnrollmentResult>> CompleteEnrollmentAsync(
        Guid adminId,
        string newPassword,
        string hourlyCode,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> VerifyStepUpAsync(
        Guid adminId,
        string password,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> ResetTwoFactorBreakGlassAsync(
        string email,
        string breakGlassSecret,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task EnsureSeededAsync(CancellationToken cancellationToken = default);
}

