namespace Platform.Api.ViewModels;

public sealed record PatientLedgerViewModel(
    Guid ClinicId,
    Guid PatientId,
    decimal CurrentBalance,
    string Currency,
    IReadOnlyList<ChargeViewModel> Charges,
    IReadOnlyList<PaymentViewModel> Payments,
    IReadOnlyList<RefundViewModel> Refunds,
    IReadOnlyList<LedgerEntryViewModel> Entries);

