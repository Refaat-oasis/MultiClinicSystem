using Core.Domain;

namespace Core.Application;

public sealed record CreateTreatmentPlanInput(
    Guid? Id,
    Guid PatientId,
    Guid DoctorId,
    string Name,
    TreatmentPlanMode Mode,
    decimal FixedTotalAmount,
    decimal IncludedMaterialsCostAmount,
    decimal UpfrontAmount,
    DateOnly StartDate,
    bool? ShowCostToPatient,
    string? Notes,
    IReadOnlyList<CreatePlanSessionInput> Sessions,
    IReadOnlyList<CreatePlanInstallmentInput> Installments,
    Guid? OperationId);

