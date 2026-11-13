namespace Platform.Api.DTOs;

public sealed record CreatePlanInstallmentDto(
    int InstallmentNumber,
    bool IsUpfront,
    DateOnly DueDate,
    decimal Amount);

