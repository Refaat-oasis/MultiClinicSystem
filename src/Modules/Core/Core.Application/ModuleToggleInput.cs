namespace Core.Application;

/// <summary>
/// Input command to enable or disable a single module for a clinic.
/// </summary>
public sealed record ModuleToggleInput(
    string ModuleKey,
    bool Enabled,
    string? Note);

