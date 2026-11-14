using Core.Application;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class PasswordPolicyTests
{
    private readonly PasswordPolicy _policy = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Given_EmptyPassword_When_Validated_Then_ReturnsFailure(string? password)
    {
        var result = _policy.Validate(password!, "test@example.com");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PASSWORD_EMPTY");
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("abc")]
    [InlineData("short")]
    public void Given_ShortPassword_When_Validated_Then_ReturnsFailure(string password)
    {
        var result = _policy.Validate(password, "test@example.com");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PASSWORD_TOO_SHORT");
    }

    [Theory]
    [InlineData("password")]
    [InlineData("12345678")]
    [InlineData("admin123")]
    [InlineData("qwerty")]
    [InlineData("clinic123")]
    [InlineData("welcome1")]
    public void Given_CommonPassword_When_Validated_Then_ReturnsFailure(string password)
    {
        var result = _policy.Validate(password, "test@example.com");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PASSWORD_TOO_COMMON");
    }

    [Fact]
    public void Given_PasswordMatchingEmail_When_Validated_Then_ReturnsFailure()
    {
        var result = _policy.Validate("doctor@clinic.com", "doctor@clinic.com");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PASSWORD_EQUALS_EMAIL");
    }

    [Fact]
    public void Given_PasswordMatchingEmailUsername_When_Validated_Then_ReturnsFailure()
    {
        var result = _policy.Validate("doctor123", "doctor123@clinic.com");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PASSWORD_EQUALS_USERNAME");
    }

    [Fact]
    public void Given_StrongUniquePassword_When_Validated_Then_ReturnsSuccess()
    {
        var result = _policy.Validate("K$9xP#2mQ!7vL&4z", "doctor@clinic.com");

        result.IsSuccess.Should().BeTrue();
    }
}
