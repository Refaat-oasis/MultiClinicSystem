using BuildingBlocks.Domain;

namespace Core.Application;

public interface ITreatmentPlanService
{
    Task<Result<TreatmentPlanResult>> CreatePlanAsync(
        Guid userId,
        Guid clinicId,
        CreateTreatmentPlanInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<TreatmentPlanResult>> GetPlanAsync(
        Guid userId,
        Guid clinicId,
        Guid planId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<TreatmentPlanResult>>> ListPlansAsync(
        Guid userId,
        Guid clinicId,
        Guid? patientId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<PlanSuggestedSlotResult>> SuggestOrBookSessionAppointmentAsync(
        Guid userId,
        Guid clinicId,
        Guid planId,
        Guid sessionId,
        SchedulePlanSessionInput? bookingInput,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<TreatmentPlanResult>> RecordSessionSlipAsync(
        Guid userId,
        Guid clinicId,
        Guid planId,
        Guid sessionId,
        RecordPlanSessionSlipInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<TreatmentPlanResult>> CompletePlanSessionAsync(
        Guid userId,
        Guid clinicId,
        Guid planId,
        Guid sessionId,
        CompletePlanSessionInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<TreatmentPlanResult>> PayInstallmentAsync(
        Guid userId,
        Guid clinicId,
        Guid planId,
        Guid installmentId,
        PayPlanInstallmentInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<TreatmentPlanResult>> RefundInstallmentAsync(
        Guid userId,
        Guid clinicId,
        Guid planId,
        Guid installmentId,
        RefundPlanDepositInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<TreatmentPlanResult>> SetPortalCostVisibilityAsync(
        Guid userId,
        Guid clinicId,
        Guid planId,
        bool showCostToPatient,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicTreatmentPlansInspectResult>> InspectClinicTreatmentPlansAsSuperAdminAsync(
        Guid superAdminId,
        Guid clinicId,
        CancellationToken cancellationToken = default);
}

