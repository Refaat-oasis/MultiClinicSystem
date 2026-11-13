using Core.Domain;

namespace Platform.Api.DTOs;

public sealed record CreateTreatmentPlanDto(
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
    IReadOnlyList<CreatePlanSessionDto> Sessions,
    IReadOnlyList<CreatePlanInstallmentDto> Installments,
    Guid? OperationId);

