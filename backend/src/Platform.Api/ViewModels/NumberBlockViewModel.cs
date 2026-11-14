namespace Platform.Api.ViewModels;

public sealed record NumberBlockViewModel(
    Guid Id,
    Guid ClinicId,
    string DeviceId,
    string Kind,
    long FromNumber,
    long ToNumber,
    long NextNumber,
    string Status,
    DateTimeOffset CreatedAtUtc);

