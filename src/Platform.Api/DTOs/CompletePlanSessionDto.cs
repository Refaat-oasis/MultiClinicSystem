namespace Platform.Api.DTOs;

public sealed record CompletePlanSessionDto(
    DateOnly CompletedDate,
    Guid? VisitId,
    decimal? AdditionalMaterialsCostAmount,
    Guid? OperationId);

