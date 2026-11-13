namespace Core.Application;

public sealed record PatientLedgerResult(
    Guid ClinicId,
    Guid PatientId,
    decimal CurrentBalance,
    string Currency,
    IReadOnlyList<ChargeResult> Charges,
    IReadOnlyList<PaymentResult> Payments,
    IReadOnlyList<RefundResult> Refunds,
    IReadOnlyList<LedgerEntryResult> Entries);

