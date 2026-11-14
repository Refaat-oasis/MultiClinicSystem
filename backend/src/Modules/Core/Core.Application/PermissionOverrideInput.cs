namespace Core.Application;

/// <summary>
/// Per-user grant or deny override input/view item.
/// </summary>
public sealed record PermissionOverrideInput(
    string PermissionKey,
    string Effect);

