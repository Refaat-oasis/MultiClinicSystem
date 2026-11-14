namespace Core.Application;

public sealed record RecordSkippedReceiptInput(
    string ReceiptNumber,
    string Reason,
    Guid? OperationId);

