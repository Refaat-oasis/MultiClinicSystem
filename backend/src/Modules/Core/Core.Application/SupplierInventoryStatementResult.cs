namespace Core.Application;

/// <summary>
/// Supplier statement combining purchase invoices with reconciled Finance payable and payment totals when active.
/// </summary>
public sealed record SupplierInventoryStatementResult(
    Guid SupplierId,
    string SupplierName,
    decimal TotalPurchasedAmount,
    decimal TotalPaidAmount,
    decimal BalanceDueAmount,
    decimal FinanceLedgerAccruedAmount,
    decimal FinanceLedgerPaidAmount,
    decimal FinanceLedgerBalanceDueAmount,
    bool FinanceModuleEnabled,
    string Currency,
    IReadOnlyList<PurchaseInvoiceResult> Invoices);

