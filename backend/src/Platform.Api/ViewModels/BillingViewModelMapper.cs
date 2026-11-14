using Core.Application;

namespace Platform.Api.ViewModels;

public static class BillingViewModelMapper
{
    public static PriceListItemViewModel ToViewModel(PriceListItemResult r) => new(
        r.Id, r.ClinicId, r.PriceCode, r.NameAr, r.NameEn, r.Kind, r.UnitCostAmount, r.DefaultPriceAmount, r.Currency, r.IsActive, r.UpdatedAtUtc);

    public static ChargeLineViewModel ToViewModel(ChargeLineResult l) => new(
        l.Id, l.Kind, l.PriceListItemId, l.InventoryItemId, l.Description, l.Quantity,
        l.UnitCostAmount, l.UnitChargedPriceAmount, l.LineCostAmount, l.LineTotalAmount, l.MarkupAmount, l.Currency);

    public static ChargeViewModel ToViewModel(ChargeResult c) => new(
        c.Id, c.ClinicId, c.PatientId, c.VisitId, c.AppointmentId, c.DoctorId, c.Status,
        c.StandardSessionPriceAmount, c.IsFreeSession, c.FreeSessionReason, c.CustomSessionPriceAmount,
        c.CustomPriceReason, c.BaseSessionPriceAmount, c.ToolsTotalCostAmount, c.ToolsTotalChargedAmount,
        c.ToolsMarkupAmount, c.ExtraChargesAmount, c.ExtraChargeReason, c.GrossSubtotalAmount,
        c.DiscountAmount, c.DiscountReason, c.UnroundedNetTotalAmount, c.NetTotalAmount, c.PaidAmount,
        c.RefundedAmount, c.RemainingDueAmount, c.Currency, c.RoundingRuleApplied, c.Notes, c.CreatedAtUtc,
        c.Lines.Select(ToViewModel).ToList());

    public static PaymentViewModel ToViewModel(PaymentResult p) => new(
        p.Id, p.ClinicId, p.PatientId, p.ChargeId, p.DeviceId, p.ReceiptNumber, p.Method,
        p.Amount, p.Currency, p.IsDeposit, p.PaymentGroupId, p.Notes, p.CollectedByUserId, p.PaidAtUtc);

    public static RefundViewModel ToViewModel(RefundResult r) => new(
        r.Id, r.ClinicId, r.PatientId, r.OriginalPaymentId, r.ChargeId, r.Method,
        r.Amount, r.Currency, r.Reason, r.ReceiptNumber, r.IssuedByUserId, r.RefundedAtUtc);

    public static LedgerEntryViewModel ToViewModel(LedgerEntryResult e) => new(
        e.Id, e.EntryType, e.ReferenceId, e.ReferenceKind, e.SignedAmount, e.Currency, e.Method, e.Description, e.ActorUserId, e.OccurredAtUtc);

    public static PatientLedgerViewModel ToViewModel(PatientLedgerResult l) => new(
        l.ClinicId,
        l.PatientId,
        l.CurrentBalance,
        l.Currency,
        l.Charges.Select(ToViewModel).ToList(),
        l.Payments.Select(ToViewModel).ToList(),
        l.Refunds.Select(ToViewModel).ToList(),
        l.Entries.Select(ToViewModel).ToList());

    public static SkippedReceiptNumberViewModel ToViewModel(SkippedReceiptNumberResult s) => new(
        s.Id, s.ClinicId, s.DeviceId, s.ReceiptNumber, s.Reason, s.RecordedByUserId, s.RecordedAtUtc);

    public static LabWorkBillingViewModel ToViewModel(LabWorkBillingResult l) => new(
        l.Id, l.ClinicId, l.PatientId, l.VisitId, l.DoctorId, l.SupplierId, l.LabName,
        l.WorkDescription, l.ToothReference, l.LabCostAmount, l.PatientPriceAmount, l.Currency, l.ChargeId, l.ExpenseId, l.CreatedAtUtc);

    public static SupplierViewModel ToViewModel(SupplierResult s) => new(
        s.Id, s.ClinicId, s.Name, s.Type, s.Phone, s.Notes, s.TotalAccruedAmount, s.TotalPaidAmount, s.BalanceDueAmount, s.Currency, s.IsActive, s.CreatedAtUtc);

    public static SupplierPaymentViewModel ToViewModel(SupplierPaymentResult p) => new(
        p.Id, p.ClinicId, p.SupplierId, p.Method, p.Amount, p.Currency, p.ReferenceNote, p.PaidByUserId, p.PaidAtUtc);

    public static ExpenseViewModel ToViewModel(ExpenseResult e) => new(
        e.Id, e.ClinicId, e.Category, e.Description, e.Method, e.Amount, e.Currency, e.SupplierId, e.LabWorkOrderId, e.CountsAsCashOut, e.RecordedByUserId, e.OccurredAtUtc);

    public static ClosingMethodTotalViewModel ToViewModel(ClosingMethodTotalResult m) => new(
        m.Method, m.PaymentsTotal, m.RefundsTotal, m.ExpensesTotal, m.ExpectedTotal, m.CountedTotal, m.VarianceAmount, m.Currency);

    public static DailyClosingViewModel ToViewModel(DailyClosingResult dc) => new(
        dc.Id, dc.ClinicId, dc.ClosingDate, dc.Status, dc.FinanceExpensesExcluded,
        dc.TotalExpectedAmount, dc.TotalCountedAmount, dc.TotalVarianceAmount, dc.Currency,
        dc.ClosedByUserId, dc.ClosedAtUtc, dc.CloseNotes, dc.ReopenedByUserId, dc.ReopenedAtUtc, dc.ReopenReason,
        dc.Methods.Select(ToViewModel).ToList());

    public static ClinicBillingInspectViewModel ToViewModel(ClinicBillingInspectResult r) => new(
        r.ClinicId, r.PriceListCount, r.ChargesCount, r.TotalNetBilledAmount, r.TotalCollectedAmount,
        r.TotalRefundedAmount, r.TotalExpensesAmount, r.ClosingsCount, r.SuppliersCount,
        r.RecentCharges.Select(ToViewModel).ToList(),
        r.RecentClosings.Select(ToViewModel).ToList());
}

