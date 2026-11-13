namespace Core.Application;

public sealed record LabWorkBillingResult(
    Guid Id,
    Guid ClinicId,
    Guid PatientId,
    Guid? VisitId,
    Guid? DoctorId,
    Guid? SupplierId,
    string LabName,
    string WorkDescription,
    string? ToothReference,
    decimal LabCostAmount,
    decimal PatientPriceAmount,
    string Currency,
    Guid? ChargeId,
    Guid? ExpenseId,
    DateTimeOffset CreatedAtUtc);

