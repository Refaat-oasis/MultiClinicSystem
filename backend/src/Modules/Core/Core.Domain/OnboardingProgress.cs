using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Tracks completed onboarding wizard steps for resumable clinic setup.
/// </summary>
public sealed class OnboardingProgress : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public string StepKey { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public Guid CompletedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private OnboardingProgress()
    {
        StepKey = string.Empty;
    }

    public OnboardingProgress(
        Guid id,
        Guid clinicId,
        string stepKey,
        DateTimeOffset completedAtUtc,
        Guid completedByUserId)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Onboarding progress ID cannot be empty.", nameof(id));
        }

        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        if (string.IsNullOrWhiteSpace(stepKey))
        {
            throw new ArgumentException("Step key cannot be empty.", nameof(stepKey));
        }

        Id = id;
        ClinicId = clinicId;
        StepKey = stepKey.Trim().ToLowerInvariant();
        CompletedAtUtc = completedAtUtc;
        CompletedByUserId = completedByUserId;
    }

    public void MarkCompletedAgain(DateTimeOffset completedAtUtc, Guid userId)
    {
        CompletedAtUtc = completedAtUtc;
        CompletedByUserId = userId;
    }
}

