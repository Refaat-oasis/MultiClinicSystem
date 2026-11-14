namespace Core.Application;

public sealed record SkippedReceiptNumberResult(
    Guid Id,
    Guid ClinicId,
    Guid? DeviceId,
    string ReceiptNumber,
    string Reason,
    string RecordedByUserId,
    DateTimeOffset RecordedAtUtc);

