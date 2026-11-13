namespace Core.Application;

/// <summary>
/// Printable PDF document bytes and filename for a clinical prescription.
/// </summary>
public sealed record PrescriptionPdfResult(
    string FileName,
    string ContentType,
    byte[] ContentBytes);

