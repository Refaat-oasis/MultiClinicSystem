using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class SlugGeneratorTests
{
    [Fact]
    public void Given_ArabicClinicName_When_SuggestingSlug_Then_TransliteratesToValidAsciiSlug()
    {
        string suggestion = SlugGenerator.SuggestFromName("عيادة النور للأسنان");

        suggestion.Should().NotBeNullOrWhiteSpace();
        SlugGenerator.Validate(suggestion).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("admin", "SLUG_RESERVED")]
    [InlineData("api", "SLUG_RESERVED")]
    [InlineData("platform", "SLUG_RESERVED")]
    [InlineData("portal", "SLUG_RESERVED")]
    [InlineData("health", "SLUG_RESERVED")]
    public void Given_ReservedSlug_When_Validating_Then_ReturnsSlugReservedError(string slug, string expectedCode)
    {
        var result = SlugGenerator.Validate(slug);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("-clinic")]
    [InlineData("clinic-")]
    [InlineData("my--clinic")]
    [InlineData("clinic_name")]
    public void Given_InvalidSlugFormat_When_Validating_Then_Fails(string slug)
    {
        var result = SlugGenerator.Validate(slug);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Given_ValidLowercaseHyphenSlug_When_Validating_Then_SucceedsNormalized()
    {
        var result = SlugGenerator.Validate("  Noor-Dental-Cairo  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("noor-dental-cairo");
    }
}

