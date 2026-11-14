using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Daily closing lock per clinic and calendar date (in clinic timezone) with expected vs counted reconciliation and audited reopen.
/// </summary>
public sealed class DailyClosing : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public DateOnly ClosingDate { get; private set; }
    public DailyClosingStatus Status { get; private set; }
    public bool FinanceExpensesExcluded { get; private set; }
    public decimal TotalExpectedAmount { get; private set; }
    public decimal TotalCountedAmount { get; private set; }
    public decimal TotalVarianceAmount { get; private set; }
    public string Currency { get; private set; } = Money.DefaultCurrency;
    public string ClosedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset ClosedAtUtc { get; private set; }
    public string? CloseNotes { get; private set; }
    public string? ReopenedByUserId { get; private set; }
    public DateTimeOffset? ReopenedAtUtc { get; private set; }
    public string? ReopenReason { get; private set; }
    public Guid? OperationId { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private DailyClosing()
    {
    }

    public DailyClosing(
        Guid id,
        Guid clinicId,
        DateOnly closingDate,
        bool financeExpensesExcluded,
        decimal totalExpectedAmount,
        decimal totalCountedAmount,
        string currency,
        string closedByUserId,
        DateTimeOffset closedAtUtc,
        string? closeNotes,
        Guid? operationId)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("ClinicId is required.", nameof(clinicId));
        }

        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        ClosingDate = closingDate;
        Status = DailyClosingStatus.Closed;
        FinanceExpensesExcluded = financeExpensesExcluded;
        TotalExpectedAmount = Math.Round(totalExpectedAmount, 2, MidpointRounding.AwayFromZero);
        TotalCountedAmount = Math.Round(totalCountedAmount, 2, MidpointRounding.AwayFromZero);
        TotalVarianceAmount = Math.Round(TotalCountedAmount - TotalExpectedAmount, 2, MidpointRounding.AwayFromZero);
        Currency = string.IsNullOrWhiteSpace(currency) ? Money.DefaultCurrency : currency.Trim().ToUpperInvariant();
        ClosedByUserId = string.IsNullOrWhiteSpace(closedByUserId) ? "system" : closedByUserId.Trim();
        ClosedAtUtc = closedAtUtc.ToUniversalTime();
        CloseNotes = string.IsNullOrWhiteSpace(closeNotes) ? null : closeNotes.Trim();
        OperationId = operationId;
    }

    public bool IsLocked => Status == DailyClosingStatus.Closed;

    public void Reopen(string actorUserId, string reason, DateTimeOffset nowUtc)
    {
        if (Status == DailyClosingStatus.Reopened)
        {
            throw new InvalidOperationException("Daily closing is already reopened.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Reopening a closed day requires a reason.");
        }

        Status = DailyClosingStatus.Reopened;
        ReopenedByUserId = string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId.Trim();
        ReopenedAtUtc = nowUtc.ToUniversalTime();
        ReopenReason = reason.Trim();
    }

    public void Reclose(
        bool financeExpensesExcluded,
        decimal totalExpectedAmount,
        decimal totalCountedAmount,
        string actorUserId,
        DateTimeOffset nowUtc,
        string? notes)
    {
        if (Status == DailyClosingStatus.Closed)
        {
            throw new InvalidOperationException("Day is already closed.");
        }

        Status = DailyClosingStatus.Closed;
        FinanceExpensesExcluded = financeExpensesExcluded;
        TotalExpectedAmount = Math.Round(totalExpectedAmount, 2, MidpointRounding.AwayFromZero);
        TotalCountedAmount = Math.Round(totalCountedAmount, 2, MidpointRounding.AwayFromZero);
        TotalVarianceAmount = Math.Round(TotalCountedAmount - TotalExpectedAmount, 2, MidpointRounding.AwayFromZero);
        ClosedByUserId = string.IsNullOrWhiteSpace(actorUserId) ? "system" : actorUserId.Trim();
        ClosedAtUtc = nowUtc.ToUniversalTime();
        CloseNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}

