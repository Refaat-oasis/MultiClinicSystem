using Core.Application;

namespace Platform.Api.ViewModels;

/// <summary>
/// Maps Patient Application results into API presentation ViewModels.
/// </summary>
public static class PatientViewModelMapper
{
    public static PatientDuplicateWarningViewModel MapWarning(PatientDuplicateWarningResult w) => new(
        w.PatientId,
        w.FileNumber,
        w.FullName,
        w.Phone,
        w.ReasonCode,
        w.ConfidencePercent);

    public static PatientAllergyItemViewModel MapAllergy(PatientAllergyItemResult a) => new(
        a.Id,
        a.PatientId,
        a.Allergen,
        a.Severity,
        a.Reaction,
        a.RecordedByUserId,
        a.CreatedAtUtc);

    public static PatientNoteItemViewModel MapNote(PatientNoteItemResult n) => new(
        n.Id,
        n.PatientId,
        n.DoctorUserId,
        n.NoteText,
        n.IsPrivate,
        n.CreatedByUserId,
        n.CreatedAtUtc);

    public static PatientAttachmentItemViewModel MapAttachment(PatientAttachmentItemResult a) => new(
        a.Id,
        a.PatientId,
        a.Category,
        a.FileName,
        a.ContentType,
        a.SizeBytes,
        a.Sha256Hash,
        a.HasThumbnail,
        a.Notes,
        a.UploadedByUserId,
        a.CreatedAtUtc);

    public static PatientSummaryItemViewModel MapSummary(PatientSummaryItemResult s) => new(
        s.Id,
        s.FileNumber,
        s.FullName,
        s.Phone,
        s.Gender,
        s.DateOfBirth,
        s.AgeYears,
        s.AssignedDoctorId,
        s.PrimarySpecialtyCode,
        s.AllergyCount,
        s.HasSevereAllergy,
        s.CreatedAtUtc);

    public static PatientDetailViewModel MapDetail(PatientDetailResult d) => new(
        d.Id,
        d.ClinicId,
        d.FileNumber,
        d.FullName,
        d.Phone,
        d.SecondaryPhone,
        d.DateOfBirth,
        d.ApproximateAgeYears,
        d.AgeYears,
        d.Gender,
        d.NationalId,
        d.Address,
        d.AssignedDoctorId,
        d.PrimarySpecialtyCode,
        d.IsMedicalHistoryEnabled,
        d.IsMedicalHistoryReviewed,
        d.MedicalHistorySummary,
        d.ChronicConditions,
        d.CurrentMedications,
        d.PastSurgeries,
        d.Allergies.Select(MapAllergy).ToList(),
        d.Notes.Select(MapNote).ToList(),
        d.IsAttachmentsEnabled,
        d.Attachments.Select(MapAttachment).ToList(),
        d.DuplicateWarnings.Select(MapWarning).ToList(),
        d.CreatedAtUtc,
        d.UpdatedAtUtc);
}

