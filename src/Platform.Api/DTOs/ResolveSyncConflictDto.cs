using Core.Domain;

namespace Platform.Api.DTOs;

/// <summary>
/// HTTP request payload to resolve or dismiss an open sync conflict.
/// </summary>
public sealed record ResolveSyncConflictDto(
    SyncConflictStatus Resolution,
    string? ResolutionNote);

