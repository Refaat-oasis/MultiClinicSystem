using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Provides bounded modular dashboard widget queries and per-user dashboard layouts.
/// </summary>
public interface IDashboardService
{
    Task<Result<DashboardWidgetCatalogResult>> GetAvailableWidgetsAsync(
        Guid userId,
        Guid clinicId,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<DashboardWidgetDataResult>> QueryWidgetAsync(
        Guid userId,
        Guid clinicId,
        string widgetKey,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? scope,
        string? deviceId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<string>>> SaveLayoutAsync(
        Guid userId,
        Guid clinicId,
        IReadOnlyList<string> pinnedWidgetKeys,
        string? deviceId,
        CancellationToken cancellationToken = default);
}

