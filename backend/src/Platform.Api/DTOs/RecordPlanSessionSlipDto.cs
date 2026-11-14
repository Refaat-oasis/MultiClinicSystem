namespace Platform.Api.DTOs;

public sealed record RecordPlanSessionSlipDto(
    DateOnly ActualOrSlippedDate,
    string? Reason);

