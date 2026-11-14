using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Application service orchestrating Phase 10 public clinic portal resolution, patient account activation/auth,
/// isolated read-only record views, signed downloads, and staff portal administration.
/// </summary>
public interface IPatientPortalService
{
    Task<Result<PublicClinicLandingResult>> ResolvePublicLandingAsync(
        string slug,
        CancellationToken cancellationToken = default);

    Task<Result<PatientPortalSessionResult>> ActivateWithEmailCodeAsync(
        string slug,
        ActivatePatientPortalInput input,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> RegisterPrintedPinAndSendEmailCodeAsync(
        string slug,
        RegisterPrintedPinInput input,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PatientPortalSessionResult>> LoginAsync(
        string slug,
        PatientPortalLoginInput input,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> ForgotPasswordAsync(
        string slug,
        string email,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result> ResetPasswordAsync(
        string slug,
        PatientPortalResetPasswordInput input,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PortalHomeOverviewResult>> GetHomeOverviewAsync(
        Guid accountId,
        Guid clinicId,
        Guid patientId,
        string slug,
        CancellationToken cancellationToken = default);

    Task<Result<PortalMedicalBackgroundResult>> GetMedicalRecordAsync(
        Guid accountId,
        Guid clinicId,
        Guid patientId,
        string slug,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PortalVisitSummaryResult>>> GetVisitsSectionAsync(
        Guid accountId,
        Guid clinicId,
        Guid patientId,
        string slug,
        CancellationToken cancellationToken = default);

    Task<Result<PortalVisitDetailResult>> GetVisitDetailAsync(
        Guid accountId,
        Guid clinicId,
        Guid patientId,
        string slug,
        Guid visitId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PortalTreatmentPlanItemResult>>> GetTreatmentPlansSectionAsync(
        Guid accountId,
        Guid clinicId,
        Guid patientId,
        string slug,
        CancellationToken cancellationToken = default);

    Task<Result<PortalSignedUrlResult>> CreateSignedPrescriptionUrlAsync(
        Guid accountId,
        Guid clinicId,
        Guid patientId,
        string slug,
        Guid prescriptionId,
        CancellationToken cancellationToken = default);

    Task<Result<PortalSignedUrlResult>> CreateSignedAttachmentUrlAsync(
        Guid accountId,
        Guid clinicId,
        Guid patientId,
        string slug,
        Guid attachmentId,
        CancellationToken cancellationToken = default);

    Task<Result<PatientAttachmentDownloadResult>> ExecuteSignedDownloadAsync(
        string slug,
        string resourceKind,
        Guid resourceId,
        Guid patientId,
        string token,
        CancellationToken cancellationToken = default);

    Task<Result<PatientPortalAdminStatusResult>> GetPatientPortalAdminStatusAsync(
        Guid staffUserId,
        Guid clinicId,
        Guid patientId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<PatientPortalAdminStatusResult>> InvitePatientAsync(
        Guid staffUserId,
        Guid clinicId,
        Guid patientId,
        InvitePatientToPortalInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PatientPortalAdminStatusResult>> ResendInvitationAsync(
        Guid staffUserId,
        Guid clinicId,
        Guid patientId,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PatientPortalAdminStatusResult>> SetAccountEnabledAsync(
        Guid staffUserId,
        Guid clinicId,
        Guid patientId,
        bool enabled,
        string? reason,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicPortalSettingResult>> GetClinicPortalSettingsAsync(
        Guid staffUserId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicPortalSettingResult>> UpdateClinicPortalSettingsAsync(
        Guid staffUserId,
        Guid clinicId,
        UpdateClinicPortalSettingInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PortalAccessLogItemResult>>> ListPatientPortalAccessLogsAsync(
        Guid staffUserId,
        Guid clinicId,
        Guid? patientId,
        int page,
        int pageSize,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicPortalInspectResult>> InspectClinicPortalAsync(
        string adminId,
        Guid clinicId,
        CancellationToken cancellationToken = default);

    Task<Result<PortalAvailabilityResult>> GetPortalAvailabilityAsync(
        Guid accountId,
        Guid clinicId,
        Guid patientId,
        string slug,
        Guid? categoryId,
        Guid? doctorId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken cancellationToken = default);

    Task<Result<PortalUpcomingAppointmentItemResult>> BookAppointmentFromPortalAsync(
        Guid accountId,
        Guid clinicId,
        Guid patientId,
        string slug,
        PortalBookAppointmentInput input,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PortalUpcomingAppointmentItemResult>> CancelAppointmentFromPortalAsync(
        Guid accountId,
        Guid clinicId,
        Guid patientId,
        string slug,
        Guid appointmentId,
        string? reason,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicPortalBookingSettingResult>> GetClinicPortalBookingSettingsAsync(
        Guid staffUserId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicPortalBookingSettingResult>> UpdateClinicPortalBookingSettingsAsync(
        Guid staffUserId,
        Guid clinicId,
        UpdateClinicPortalBookingSettingInput input,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PortalBookingRequestItemResult>>> ListPortalBookingRequestsAsync(
        Guid staffUserId,
        Guid clinicId,
        string? statusFilter,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<PortalBookingRequestItemResult>> ApprovePortalBookingRequestAsync(
        Guid staffUserId,
        Guid clinicId,
        Guid appointmentId,
        string? reviewNotes,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<PortalBookingRequestItemResult>> DeclinePortalBookingRequestAsync(
        Guid staffUserId,
        Guid clinicId,
        Guid appointmentId,
        string? reviewNotes,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<int>> ExpireStalePortalBookingRequestsAsync(
        Guid staffUserId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<PatientPortalAdminStatusResult>> SetPatientOnlineBookingBlockAsync(
        Guid staffUserId,
        Guid clinicId,
        Guid patientId,
        bool blocked,
        string? reason,
        string? deviceId,
        string? ipAddress,
        string? requestId,
        CancellationToken cancellationToken = default);

    Task<Result<ClinicPortalBookingInspectResult>> InspectClinicPortalBookingAsync(
        string adminId,
        Guid clinicId,
        CancellationToken cancellationToken = default);
}

