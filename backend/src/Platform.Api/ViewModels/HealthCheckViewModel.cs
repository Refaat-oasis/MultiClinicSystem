namespace Platform.Api.ViewModels;

/// <summary>
/// Presentation model representing service health status.
/// </summary>
public sealed record HealthCheckViewModel(string Status, DateTimeOffset TimestampUtc);

