using BuildingBlocks.Domain;
using Core.Domain;

namespace Core.Application;

public interface IReportsService
{
    Task<Result<IncomeVsExpensesReportResult>> GetIncomeVsExpensesReportAsync(
        Guid userId,
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DoctorFinancialReportRowResult>>> GetDoctorSummaryReportAsync(
        Guid userId,
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        Guid? doctorId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SessionCategoryReportRowResult>>> GetSessionsAndCategoriesReportAsync(
        Guid userId,
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        Guid? doctorId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<MethodTotalReportRowResult>>> GetPaymentMethodTotalsReportAsync(
        Guid userId,
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ExportedReportFileResult>> ExportReportAsync(
        Guid userId,
        Guid clinicId,
        ReportExportKind reportKind,
        ReportExportFormat format,
        DateOnly fromDate,
        DateOnly toDate,
        Guid? doctorId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ReportExportLogResult>>> ListExportLogsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ScheduledReportConfigResult>>> ListScheduledReportsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ScheduledReportConfigResult>> UpsertScheduledReportAsync(
        Guid userId,
        Guid clinicId,
        UpsertScheduledReportInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PlatformCrossClinicReportResult>> GetPlatformCrossClinicReportAsync(
        Guid platformAdminId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicFinanceReportsInspectResult>> InspectClinicFinanceReportsAsync(
        Guid platformAdminId,
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);
}

