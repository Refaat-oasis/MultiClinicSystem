using BuildingBlocks.Domain;

namespace Core.Application;

public interface IBillingService
{
    Task<Result<IReadOnlyList<PriceListItemResult>>> ListPriceListAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<PriceListItemResult>> UpsertPriceListItemAsync(
        Guid userId,
        Guid clinicId,
        UpsertPriceListItemInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ChargeResult>>> ListChargesAsync(
        Guid userId,
        Guid clinicId,
        Guid? patientId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ChargeResult>> CreateChargeAsync(
        Guid userId,
        Guid clinicId,
        CreateChargeInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task CreateDraftChargeForCompletedVisitAsync(
        Guid clinicId,
        Guid visitId,
        Guid patientId,
        Guid? appointmentId,
        Guid doctorId,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<Result<ChargeResult>> PostDraftChargeAsync(
        Guid userId,
        Guid clinicId,
        Guid chargeId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PaymentResult>>> RecordPaymentAsync(
        Guid userId,
        Guid clinicId,
        RecordPaymentInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<RefundResult>> CreateRefundAsync(
        Guid userId,
        Guid clinicId,
        CreateRefundInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<SkippedReceiptNumberResult>> RecordSkippedReceiptAsync(
        Guid userId,
        Guid clinicId,
        RecordSkippedReceiptInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SkippedReceiptNumberResult>>> ListSkippedReceiptsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<PatientLedgerResult>> GetPatientLedgerAsync(
        Guid userId,
        Guid clinicId,
        Guid patientId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ReceiptPdfResult>> GenerateReceiptPdfAsync(
        Guid userId,
        Guid clinicId,
        Guid paymentId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<LabWorkBillingResult>> CreateLabWorkAsync(
        Guid userId,
        Guid clinicId,
        CreateLabWorkBillingInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LabWorkBillingResult>>> ListLabWorkAsync(
        Guid userId,
        Guid clinicId,
        Guid? patientId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SupplierResult>>> ListSuppliersAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<SupplierResult>> CreateSupplierAsync(
        Guid userId,
        Guid clinicId,
        CreateSupplierInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<SupplierPaymentResult>> RecordSupplierPaymentAsync(
        Guid userId,
        Guid clinicId,
        RecordSupplierPaymentInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ExpenseResult>>> ListExpensesAsync(
        Guid userId,
        Guid clinicId,
        DateOnly? date,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ExpenseResult>> CreateExpenseAsync(
        Guid userId,
        Guid clinicId,
        CreateExpenseInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<DailyClosingResult>> GetDailyClosingPreviewAsync(
        Guid userId,
        Guid clinicId,
        DateOnly date,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<DailyClosingResult>> CloseDayAsync(
        Guid userId,
        Guid clinicId,
        CloseDayInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<DailyClosingResult>> ReopenDayAsync(
        Guid userId,
        Guid clinicId,
        ReopenDayInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicBillingInspectResult>> InspectClinicBillingAsync(
        Guid adminId,
        Guid clinicId,
        CancellationToken cancellationToken = default);
}

