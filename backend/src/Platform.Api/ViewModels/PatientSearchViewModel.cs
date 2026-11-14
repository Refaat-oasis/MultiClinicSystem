namespace Platform.Api.ViewModels;

public sealed record PatientSearchViewModel(
    int TotalCount,
    IReadOnlyList<PatientSummaryItemViewModel> Items);

