using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Per-clinic patient portal presentation, default visibility, and onboarding copy settings.
/// </summary>
public sealed class ClinicPortalSetting : ITenantEntity, ISoftDeletable
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; private set; }
    public bool ShowPricesByDefault { get; private set; }
    public bool ShowDoctorNames { get; private set; }
    public bool DefaultVisitVisibility { get; private set; }
    public string TermsTextAr { get; private set; }
    public string TermsTextEn { get; private set; }
    public string WelcomeMessageAr { get; private set; }
    public string WelcomeMessageEn { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public bool IsVoided { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ClinicPortalSetting()
    {
        TermsTextAr = "أوافق على شروط الخصوصية والوصول إلى ملفي الطبي في بوابة المريض.";
        TermsTextEn = "I agree to the clinic portal terms and privacy policy.";
        WelcomeMessageAr = "مرحباً بكم في بوابة المريض الإلكترونية.";
        WelcomeMessageEn = "Welcome to your clinic patient portal.";
    }

    public ClinicPortalSetting(Guid id, Guid clinicId, DateTimeOffset updatedAtUtc)
    {
        if (clinicId == Guid.Empty)
        {
            throw new ArgumentException("Clinic ID cannot be empty.", nameof(clinicId));
        }

        Id = id == Guid.Empty ? GuidV7.NewGuid() : id;
        ClinicId = clinicId;
        ShowPricesByDefault = true;
        ShowDoctorNames = true;
        DefaultVisitVisibility = true;
        TermsTextAr = "أوافق على شروط الخصوصية والوصول إلى ملفي الطبي في بوابة المريض.";
        TermsTextEn = "I agree to the clinic portal terms and privacy policy.";
        WelcomeMessageAr = "مرحباً بكم في بوابة المريض الإلكترونية.";
        WelcomeMessageEn = "Welcome to your clinic patient portal.";
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
    }

    public void Update(
        bool showPricesByDefault,
        bool showDoctorNames,
        bool defaultVisitVisibility,
        string? termsTextAr,
        string? termsTextEn,
        string? welcomeMessageAr,
        string? welcomeMessageEn,
        DateTimeOffset updatedAtUtc)
    {
        ShowPricesByDefault = showPricesByDefault;
        ShowDoctorNames = showDoctorNames;
        DefaultVisitVisibility = defaultVisitVisibility;
        if (!string.IsNullOrWhiteSpace(termsTextAr))
        {
            TermsTextAr = termsTextAr.Trim();
        }
        if (!string.IsNullOrWhiteSpace(termsTextEn))
        {
            TermsTextEn = termsTextEn.Trim();
        }
        if (!string.IsNullOrWhiteSpace(welcomeMessageAr))
        {
            WelcomeMessageAr = welcomeMessageAr.Trim();
        }
        if (!string.IsNullOrWhiteSpace(welcomeMessageEn))
        {
            WelcomeMessageEn = welcomeMessageEn.Trim();
        }
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
    }
}

