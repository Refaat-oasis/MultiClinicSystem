namespace Core.Application;

public sealed record PortalBillingSummaryResult(
    Guid ClinicId,
    Guid PatientId,
    decimal TotalChargedAmount,
    decimal TotalPaidAmount,
    decimal TotalRefundedAmount,
    decimal CurrentBalanceDue,
    string Currency,
    IReadOnlyList<PortalBillingChargeItemResult> Charges,
    IReadOnlyList<string> ReceiptNumbers);

