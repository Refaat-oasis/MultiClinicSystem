namespace Platform.Api.ViewModels;

public sealed record PatientDuplicateWarningViewModel(
    Guid PatientId,
    string FileNumber,
    string FullName,
    string Phone,
    string WarningReason,
    int ConfidencePercent);

