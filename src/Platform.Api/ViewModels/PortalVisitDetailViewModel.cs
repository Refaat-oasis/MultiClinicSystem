namespace Platform.Api.ViewModels;

public sealed record PortalVisitDetailViewModel(
    PortalVisitSummaryViewModel Visit,
    IReadOnlyList<PortalSharedAttachmentItemViewModel> SharedAttachments);

