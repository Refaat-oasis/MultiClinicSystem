namespace Core.Application;

public sealed record RecordPlanSessionSlipInput(
    DateOnly ActualOrSlippedDate,
    string? Reason);

