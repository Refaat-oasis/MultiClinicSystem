using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class VerificationCodesTests
{
    private const string ServerSecret = "ultra-secure-test-server-secret-key-for-hmac-sha256-clinic";
    private readonly VerificationCodes _service = new(ServerSecret);

    [Fact]
    public void Given_VerificationCodesService_When_GenerateCodeCalled_Then_ReturnsSixDigitNumericString()
    {
        // Act
        string code = _service.GenerateCode();

        // Assert
        code.Should().HaveLength(6);
        code.Should().MatchRegex(@"^\d{6}$");
    }

    [Fact]
    public void Given_ValidCodeAndHmac_When_VerifiedWithSameUserAndPurpose_Then_ReturnsTrue()
    {
        // Arrange
        const string userId = "usr_0194ff72000070008000000000000001";
        const string purpose = "activation";
        string rawCode = "123456";
        string hmac = _service.ComputeHmac(rawCode, userId, purpose);

        // Act
        bool isValid = _service.Verify(rawCode, hmac, userId, purpose);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void Given_WrongCode_When_Verified_Then_ReturnsFalse()
    {
        // Arrange
        const string userId = "usr_0194ff72000070008000000000000001";
        const string purpose = "password-reset";
        string hmac = _service.ComputeHmac("123456", userId, purpose);

        // Act
        bool isValid = _service.Verify("654321", hmac, userId, purpose);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void Given_DifferentUserOrPurpose_When_Verified_Then_ReturnsFalse()
    {
        // Arrange
        string hmac = _service.ComputeHmac("789123", "userA", "activation");

        // Act & Assert
        _service.Verify("789123", hmac, "userB", "activation").Should().BeFalse();
        _service.Verify("789123", hmac, "userA", "password-reset").Should().BeFalse();
    }
}

