namespace Core.Application;

public sealed record CreatePlanInstallmentInput(
    int InstallmentNumber,
    bool IsUpfront,
    DateOnly DueDate,
    decimal Amount);

