namespace Platform.Api.ViewModels;

public sealed record ClinicBillingInspectViewModel(
    Guid ClinicId,
    int PriceListCount,
    int ChargesCount,
    decimal TotalNetBilledAmount,
    decimal TotalCollectedAmount,
    decimal TotalRefundedAmount,
    decimal TotalExpensesAmount,
    int ClosingsCount,
    int SuppliersCount,
    IReadOnlyList<ChargeViewModel> RecentCharges,
    IReadOnlyList<DailyClosingViewModel> RecentClosings);

