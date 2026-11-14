namespace Core.Application;

public sealed record ReceiptPdfResult(
    string FileName,
    string ContentType,
    byte[] Bytes);

