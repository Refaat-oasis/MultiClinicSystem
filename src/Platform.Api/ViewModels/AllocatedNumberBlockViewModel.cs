namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation ViewModel for a pre-allocated device number block.
/// </summary>
public sealed record AllocatedNumberBlockViewModel(
    Guid Id,
    Guid ClinicId,
    string DeviceId,
    string Kind,
    long FromNumber,
    long ToNumber,
    long NextNumber,
    long RemainingCount,
    string Status,
    DateTimeOffset IssuedAtUtc);

