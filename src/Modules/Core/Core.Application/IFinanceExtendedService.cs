using BuildingBlocks.Domain;

namespace Core.Application;

public interface IFinanceExtendedService
{
    Task<Result<IReadOnlyList<FixedCostResult>>> ListFixedCostsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<FixedCostResult>> UpsertFixedCostAsync(
        Guid userId,
        Guid clinicId,
        UpsertFixedCostInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> VoidFixedCostAsync(
        Guid userId,
        Guid clinicId,
        Guid fixedCostId,
        string reason,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<FixedCostOccurrenceResult>>> ListFixedCostOccurrencesAsync(
        Guid userId,
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<EmployeeResult>>> ListEmployeesAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeResult>> UpsertEmployeeAsync(
        Guid userId,
        Guid clinicId,
        UpsertEmployeeInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PayrollAdjustmentResult>>> ListPayrollAdjustmentsAsync(
        Guid userId,
        Guid clinicId,
        int? periodYear,
        int? periodMonth,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollAdjustmentResult>> RecordPayrollAdjustmentAsync(
        Guid userId,
        Guid clinicId,
        RecordPayrollAdjustmentInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PayrollRunResult>>> ListPayrollRunsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollRunResult>> CreatePayrollRunAsync(
        Guid userId,
        Guid clinicId,
        CreatePayrollRunInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollRunResult>> FinalizePayrollRunAsync(
        Guid userId,
        Guid clinicId,
        Guid payrollRunId,
        Guid? operationId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IncomeVsExpensesReportResult>> GetIncomeVsExpensesAsync(
        Guid userId,
        Guid clinicId,
        DateOnly fromDate,
        DateOnly toDate,
        string? deviceId,
        CancellationToken cancellationToken = default);
}

