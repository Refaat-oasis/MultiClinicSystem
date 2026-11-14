namespace Platform.Api.DTOs;

public sealed record RecordSkippedReceiptDto(
    string ReceiptNumber,
    string Reason,
    Guid? OperationId);

