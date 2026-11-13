using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class OnboardingStateMachineTests
{
    [Fact]
    public void Given_NewClinic_When_SteppingThroughOnboarding_Then_TransitionsFromNotStartedToInProgressToCompleted()
    {
        var clinic = new Clinic(Guid.NewGuid(), "عيادة الأمل", "amal-temp", DateTimeOffset.UtcNow, ownerId: Guid.NewGuid());
        clinic.OnboardingStatus.Should().Be("NotStarted");
        clinic.Status.Should().Be("PendingOnboarding");

        clinic.StartOnboarding();
        clinic.OnboardingStatus.Should().Be("InProgress");

        var incomplete = clinic.CompleteOnboarding();
        incomplete.IsFailure.Should().BeTrue();
        incomplete.Error.Code.Should().Be("ONBOARDING_INCOMPLETE");

        clinic.UpdateOnboardingDetails(
            name: "عيادة الأمل التخصصية",
            street: "شارع التحرير",
            city: "الدقي",
            governorate: "الجيزة",
            mainPhone: "+201012345678",
            extraPhonesJson: "[]",
            contactEmail: "info@amal.local",
            logoAssetId: null);
        clinic.UpdateSlug("amal-clinic");

        var completed = clinic.CompleteOnboarding();
        completed.IsSuccess.Should().BeTrue();
        clinic.OnboardingStatus.Should().Be("Completed");
        clinic.Status.Should().Be("Active");
    }

    [Fact]
    public void Given_ClinicWithFinancialEntries_When_ChangingCurrencyOrTimeZone_Then_FailsWithFinancialSettingsLocked()
    {
        var clinic = new Clinic(Guid.NewGuid(), "عيادة النور", "noor-clinic", DateTimeOffset.UtcNow, ownerId: Guid.NewGuid(), status: "Active", onboardingStatus: "Completed");
        clinic.MarkHasFinancialEntries();

        var result = clinic.UpdateGeneralSettings(
            name: "عيادة النور",
            street: "شارع النيل",
            city: "المعادي",
            governorate: "القاهرة",
            mainPhone: "+201012345678",
            extraPhonesJson: "[]",
            contactEmail: null,
            timeZone: "UTC",
            currency: "USD",
            roundingRule: "NearestPound",
            logoAssetId: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("FINANCIAL_SETTINGS_LOCKED");
    }
}

