using Core.Domain;

namespace Core.Application;

public sealed record CreateLabWorkBillingInput(
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

