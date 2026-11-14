using Core.Domain;

namespace Core.Application;

public sealed record CreateChargeInput(
    Guid? Id,
    Guid PatientId,
    Guid? VisitId,
    Guid? AppointmentId,
    Guid? DoctorId,
    decimal? StandardSessionPriceAmount,
    bool IsFreeSession,
    string? FreeSessionReason,
    decimal? CustomSessionPriceAmount,
    string? CustomPriceReason,
    decimal? DiscountPercent,
    decimal? DiscountFixedAmount,
    string? DiscountReason,
    decimal? ExtraChargeAmount,
    string? ExtraChargeReason,
    IReadOnlyList<ChargeLineInput>? Lines,
    string? Notes,
    bool PostImmediately,
    Guid? OperationId);

