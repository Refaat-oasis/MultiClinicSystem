namespace Core.Application;

/// <summary>
/// Aggregate summary card for a single clinic owned by the current owner.
/// </summary>
public sealed record OwnerPortfolioClinicItemResult(
    Guid ClinicId,
    string Name,
    string Slug,
    string Status,
    string OnboardingStatus,
    int StaffCount,
    int RoomsCount,
    int DoctorsCount,
    int EnabledModulesCount);

