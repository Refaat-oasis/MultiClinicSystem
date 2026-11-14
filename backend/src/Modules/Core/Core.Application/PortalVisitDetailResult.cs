namespace Core.Application;

public sealed record PortalVisitDetailResult(
    PortalVisitSummaryResult Visit,
    IReadOnlyList<PortalSharedAttachmentItemResult> SharedAttachments);

