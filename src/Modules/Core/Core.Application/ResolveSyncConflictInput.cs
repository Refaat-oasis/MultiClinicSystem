using Core.Domain;

namespace Core.Application;

/// <summary>
/// Input model to resolve or dismiss an open sync conflict.
/// </summary>
public sealed record ResolveSyncConflictInput(
    SyncConflictStatus Resolution,
    string? ResolutionNote);

