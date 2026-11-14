namespace Platform.Api.ViewModels;

public sealed record ImportRowErrorViewModel(
    int RowNumber,
    string Column,
    string ErrorKey,
    string Message);

