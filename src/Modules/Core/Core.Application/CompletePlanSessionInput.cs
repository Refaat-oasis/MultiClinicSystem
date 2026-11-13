namespace Core.Application;

public sealed record CompletePlanSessionInput(
    DateOnly CompletedDate,
    Guid? VisitId,
    decimal? AdditionalMaterialsCostAmount,
    Guid? OperationId);

