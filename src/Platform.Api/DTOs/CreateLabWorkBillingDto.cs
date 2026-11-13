using Core.Domain;

namespace Platform.Api.DTOs;

public sealed record CreateLabWorkBillingDto(
    Guid? Id,
    Guid PatientId,
    Guid? VisitId,
    Guid? DoctorId,
    Guid? SupplierId,
    string LabName,
    string WorkDescription,
    string? ToothReference,
    decimal LabCostAmount,
    decimal PatientPriceAmount,
    PaymentMethod? ImmediateExpenseMethod,
    Guid? OperationId);

