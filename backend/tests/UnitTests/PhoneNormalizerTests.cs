using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class PhoneNormalizerTests
{
    [Theory]
    [InlineData("01012345678", "+201012345678")]
    [InlineData("011-2233-4455", "+201122334455")]
    [InlineData("012 3456 7890", "+201234567890")]
    [InlineData("01555551234", "+201555551234")]
    [InlineData("0223456789", "+20223456789")]
    [InlineData("03-4201234", "+2034201234")]
    [InlineData("00971501234567", "+971501234567")]
    [InlineData("+44 20 7946 0958", "+442079460958")]
    public void Given_ValidPhoneNumber_When_Normalizing_Then_ReturnsNormalizedE164(string input, string expected)
    {
        var result = PhoneNormalizer.Normalize(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("01912345678")]
    [InlineData("abc1234567")]
    public void Given_InvalidPhoneNumber_When_Normalizing_Then_ReturnsValidationFailure(string input)
    {
        var result = PhoneNormalizer.Normalize(input);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_PHONE");
    }
}

