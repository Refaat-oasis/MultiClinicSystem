namespace Core.Application;

/// <summary>
/// Input model for creating a clinical lab or prosthodontic work order during a visit.
/// </summary>
public sealed record CreateLabOrderInput(
    string LabName,
    string TestOrWorkName,
    string? ToothOrSite,
    string? Instructions);

