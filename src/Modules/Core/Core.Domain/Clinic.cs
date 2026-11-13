using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Represents a medical clinic tenant within the multi-clinic platform.
/// </summary>
public sealed class Clinic
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public Guid? OwnerId { get; private set; }
    public string Status { get; private set; }
    public string OnboardingStatus { get; private set; }
    public string TimeZone { get; private set; }
    public string Currency { get; private set; }
    public string RoundingRule { get; private set; }
    public string? Street { get; private set; }
    public string? City { get; private set; }
    public string? Governorate { get; private set; }
    public string? MainPhone { get; private set; }
    public string ExtraPhonesJson { get; private set; }
    public string? ContactEmail { get; private set; }
    public Guid? LogoAssetId { get; private set; }
    public bool HasFinancialEntries { get; private set; }
    public DateTimeOffset? SubscriptionExpiryUtc { get; private set; }
    public int GraceDays { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private Clinic()
    {
        Name = string.Empty;
        Slug = string.Empty;
        Status = "PendingOnboarding";
        OnboardingStatus = "NotStarted";
        TimeZone = "Africa/Cairo";
        Currency = "EGP";
        RoundingRule = "NearestPound";
        ExtraPhonesJson = "[]";
        GraceDays = 14;
    }

    public Clinic(
        Guid id,
        string name,
        string slug,
        DateTimeOffset createdAtUtc,
        Guid? ownerId = null,
        string status = "PendingOnboarding",
        DateTimeOffset? subscriptionExpiryUtc = null,
        int graceDays = 14,
        string onboardingStatus = "NotStarted",
        string timeZone = "Africa/Cairo",
        string currency = "EGP",
        string roundingRule = "NearestPound")
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Clinic name cannot be empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Clinic slug cannot be empty.", nameof(slug));
        }

        Id = id;
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        OwnerId = ownerId;
        Status = status;
        OnboardingStatus = string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase) && onboardingStatus == "NotStarted"
            ? "Completed"
            : onboardingStatus;
        TimeZone = string.IsNullOrWhiteSpace(timeZone) ? "Africa/Cairo" : timeZone.Trim();
        Currency = string.IsNullOrWhiteSpace(currency) ? "EGP" : currency.Trim().ToUpperInvariant();
        RoundingRule = string.IsNullOrWhiteSpace(roundingRule) ? "NearestPound" : roundingRule.Trim();
        ExtraPhonesJson = "[]";
        SubscriptionExpiryUtc = subscriptionExpiryUtc;
        GraceDays = graceDays;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public void Deactivate()
    {
        IsActive = false;
        Status = "Suspended";
    }

    public void Activate()
    {
        IsActive = true;
        Status = "Active";
    }

    public void SetStatus(string status)
    {
        Status = status;
    }

    public void UpdateSubscription(DateTimeOffset? expiryUtc, int graceDays)
    {
        SubscriptionExpiryUtc = expiryUtc;
        GraceDays = graceDays;
    }

    public void MoveToOwner(Guid newOwnerId)
    {
        if (newOwnerId == Guid.Empty)
        {
            throw new ArgumentException("Target owner ID cannot be empty.", nameof(newOwnerId));
        }

        OwnerId = newOwnerId;
    }

    public void ConfigureLocaleCurrency(string? timeZone, string? currency, string? roundingRule)
    {
        if (!string.IsNullOrWhiteSpace(timeZone))
        {
            TimeZone = timeZone.Trim();
        }

        if (!string.IsNullOrWhiteSpace(currency))
        {
            Currency = currency.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(roundingRule))
        {
            RoundingRule = roundingRule.Trim();
        }
    }

    public void StartOnboarding()
    {
        if (string.Equals(OnboardingStatus, "NotStarted", StringComparison.OrdinalIgnoreCase))
        {
            OnboardingStatus = "InProgress";
        }
    }

    public void UpdateOnboardingDetails(
        string name,
        string street,
        string city,
        string governorate,
        string mainPhone,
        string extraPhonesJson,
        string? contactEmail,
        Guid? logoAssetId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Clinic name cannot be empty.", nameof(name));
        }

        Name = name.Trim();
        Street = street.Trim();
        City = city.Trim();
        Governorate = governorate.Trim();
        MainPhone = mainPhone.Trim();
        ExtraPhonesJson = string.IsNullOrWhiteSpace(extraPhonesJson) ? "[]" : extraPhonesJson;
        ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim().ToLowerInvariant();
        if (logoAssetId.HasValue)
        {
            LogoAssetId = logoAssetId.Value;
        }

        StartOnboarding();
    }

    public void SetLogoAsset(Guid assetId)
    {
        LogoAssetId = assetId;
    }

    public void UpdateSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("Slug cannot be empty.", nameof(slug));
        }

        Slug = slug.Trim().ToLowerInvariant();
    }

    public Result CompleteOnboarding()
    {
        if (string.IsNullOrWhiteSpace(Name) ||
            string.IsNullOrWhiteSpace(Street) ||
            string.IsNullOrWhiteSpace(City) ||
            string.IsNullOrWhiteSpace(Governorate) ||
            string.IsNullOrWhiteSpace(MainPhone) ||
            string.IsNullOrWhiteSpace(Slug))
        {
            return Result.Failure(Error.Validation(
                "ONBOARDING_INCOMPLETE",
                "Clinic details and public slug must be completed before finishing onboarding."));
        }

        OnboardingStatus = "Completed";
        if (string.Equals(Status, "PendingOnboarding", StringComparison.OrdinalIgnoreCase))
        {
            Status = "Active";
        }

        return Result.Success();
    }

    public Result UpdateGeneralSettings(
        string name,
        string street,
        string city,
        string governorate,
        string mainPhone,
        string extraPhonesJson,
        string? contactEmail,
        string? timeZone,
        string? currency,
        string? roundingRule,
        Guid? logoAssetId)
    {
        bool changingLockedFields =
            (!string.IsNullOrWhiteSpace(timeZone) && !string.Equals(timeZone.Trim(), TimeZone, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(currency) && !string.Equals(currency.Trim(), Currency, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(roundingRule) && !string.Equals(roundingRule.Trim(), RoundingRule, StringComparison.OrdinalIgnoreCase));

        if (HasFinancialEntries && changingLockedFields)
        {
            return Result.Failure(Error.Conflict(
                "FINANCIAL_SETTINGS_LOCKED",
                "Time zone, currency, and rounding rule are locked after the first financial entry."));
        }

        Name = name.Trim();
        Street = street.Trim();
        City = city.Trim();
        Governorate = governorate.Trim();
        MainPhone = mainPhone.Trim();
        ExtraPhonesJson = string.IsNullOrWhiteSpace(extraPhonesJson) ? "[]" : extraPhonesJson;
        ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim().ToLowerInvariant();

        if (!HasFinancialEntries)
        {
            ConfigureLocaleCurrency(timeZone, currency, roundingRule);
        }

        if (logoAssetId.HasValue)
        {
            LogoAssetId = logoAssetId.Value;
        }

        return Result.Success();
    }

    public void MarkHasFinancialEntries()
    {
        HasFinancialEntries = true;
    }
}
