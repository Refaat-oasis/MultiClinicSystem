namespace Platform.Api.ViewModels;

public sealed record SkippedReceiptNumberViewModel(
    Guid Id,
    Guid ClinicId,
    Guid? DeviceId,
    string ReceiptNumber,
    string Reason,
    string RecordedByUserId,
    DateTimeOffset RecordedAtUtc);

