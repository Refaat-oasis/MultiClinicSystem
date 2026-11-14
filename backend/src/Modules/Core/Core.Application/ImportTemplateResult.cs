using Core.Domain;

namespace Core.Application;

public sealed record ImportTemplateResult(
    ImportTargetKind TargetKind,
    string FileName,
    string ContentType,
    byte[] Content);

