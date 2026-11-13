using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class PatientsDomainTests
{
    [Theory]
    [InlineData("أحمد محمود إبراهيم", "احمد محمود ابراهيم")]
    [InlineData("آلاء علي حسن", "الاء علي حسن")]
    [InlineData("فاطمة مصطفى على", "فاطمه مصطفي علي")]
    [InlineData("مُحَمَّـدٌ عَبْدُاللَّهِ", "محمد عبدالله")]
    [InlineData("  يحيى   السيد  ", "يحيي السيد")]
    public void Given_ArabicNameWithVariants_When_Normalized_Then_ProducesCanonicalSearchForm(string raw, string expected)
    {
        string normalized = ArabicNormalizer.NormalizeText(raw);
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData("+20 100 123 4567", "01001234567")]
    [InlineData("0020-100-123-4567", "01001234567")]
    [InlineData("٠١٠٠١٢٣٤٥٦٧", "01001234567")]
    [InlineData("(0100) 123-4567", "01001234567")]
    public void Given_PhoneFormats_When_Normalized_Then_ProducesCanonicalDigits(string raw, string expected)
    {
        string normalized = ArabicNormalizer.NormalizePhone(raw);
        normalized.Should().Be(expected);
    }

    [Fact]
    public void Given_DateOfBirthOrApproximateAge_When_CalculatingAge_Then_ReturnsExpectedYears()
    {
        var today = new DateOnly(2026, 6, 15);

        PatientAgeCalculator.CalculateAgeYears(new DateOnly(1996, 6, 15), null, today).Should().Be(30);
        PatientAgeCalculator.CalculateAgeYears(new DateOnly(1996, 6, 16), null, today).Should().Be(29);
        PatientAgeCalculator.CalculateAgeYears(null, 42, today).Should().Be(42);
        PatientAgeCalculator.CalculateAgeYears(null, null, today).Should().BeNull();
    }

    [Fact]
    public void Given_ExistingPatients_When_DetectingDuplicates_Then_MatchesNormalizedPhoneAndArabicName()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var candidates = new[]
        {
            new PatientDuplicateCandidate(id1, "P-000001", "أحمد مصطفى", ArabicNormalizer.NormalizeText("أحمد مصطفى"), "01001112222", ArabicNormalizer.NormalizePhone("01001112222")),
            new PatientDuplicateCandidate(id2, "P-000002", "منى سالم", ArabicNormalizer.NormalizeText("منى سالم"), "01112223333", ArabicNormalizer.NormalizePhone("01112223333"))
        };

        var exactMatch = PatientDuplicateDetector.FindMatches("احمد مصطفي", "+20 100 111 2222", candidates);
        exactMatch.Should().ContainSingle(m => m.PatientId == id1 && m.ReasonCode == "PHONE_AND_NAME_EXACT" && m.ConfidencePercent == 100);

        var nameOnly = PatientDuplicateDetector.FindMatches("مني سالم", "01559998888", candidates);
        nameOnly.Should().ContainSingle(m => m.PatientId == id2 && m.ReasonCode == "NORMALIZED_NAME_MATCH" && m.ConfidencePercent == 75);

        var excluded = PatientDuplicateDetector.FindMatches("أحمد مصطفى", "01001112222", candidates, excludePatientId: id1);
        excluded.Should().BeEmpty();
    }

    [Fact]
    public void Given_PatientAllergyAndNote_When_Voided_Then_SoftDeleteFlagsAreSet()
    {
        var now = DateTimeOffset.UtcNow;
        var allergy = new PatientAllergy(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Penicillin", "Severe", "Rash", Guid.NewGuid(), now);
        allergy.Severity.Should().Be("Severe");

        allergy.Void(now.AddMinutes(5), "user-1", "Incorrect entry");
        allergy.IsVoided.Should().BeTrue();
        allergy.VoidReason.Should().Be("Incorrect entry");

        var note = new PatientNote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Private doctor note", true, Guid.NewGuid(), now);
        note.IsPrivate.Should().BeTrue();
        note.Void(now.AddMinutes(10), "user-1", "Edited");
        note.IsVoided.Should().BeTrue();
    }
}

