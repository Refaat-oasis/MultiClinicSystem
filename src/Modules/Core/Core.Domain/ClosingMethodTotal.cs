using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Per-payment-method closing breakdown row under a DailyClosing.
/// </summary>
public sealed class ClosingMethodTotal : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public Guid DailyClosingId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public decimal PaymentsTotal { get; private set; }
    public decimal RefundsTotal { get; private set; }
    public decimal ExpensesTotal { get; private set; }
    public decimal ExpectedTotal { get; private set; }
    public decimal CountedTotal { get; private set; }
    public decimal VarianceAmount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ClosingMethodTotal()
    {
    }

    public ClosingMethodTotal(
        Guid id,
        Guid clinicId,
        Guid dailyClosingId,
        ClosingMethodSummary summary,
        string currency)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        if (dailyClosingId == Guid.Empty)
        {
            throw new ArgumentException("DailyClosingId is required.", nameof(dailyClosingId));
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        DailyClosingId = dailyClosingId;
        Method = summary.Method;
        PaymentsTotal = summary.PaymentsTotal;
        RefundsTotal = summary.RefundsTotal;
        ExpensesTotal = summary.ExpensesTotal;
        ExpectedTotal = summary.ExpectedAmount;
        CountedTotal = summary.CountedAmount;
        VarianceAmount = summary.Variance;
        Currency = string.IsNullOrWhiteSpace(currency) ? Money.DefaultCurrency : currency.Trim().ToUpperInvariant();
    }
}
