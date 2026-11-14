namespace Core.Application;

/// <summary>
/// Binary download payload for an authorized patient attachment or thumbnail.
/// </summary>
public sealed record PatientAttachmentDownloadResult(
    string FileName,
    string ContentType,
    byte[] Content);

