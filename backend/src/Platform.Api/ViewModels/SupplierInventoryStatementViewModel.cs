namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation view model for a supplier's reconciled inventory purchase statement.
/// </summary>
public sealed record SupplierInventoryStatementViewModel(
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
    IReadOnlyList<PurchaseInvoiceViewModel> Invoices);

