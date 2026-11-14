using BuildingBlocks.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class MoneyTests
{
    [Fact]
    public void Given_AmountWithMoreThanTwoDecimals_When_Constructed_Then_RoundsToTwoDecimals()
    {
        // Arrange & Act
        var money = new Money(12.3456m, "EGP");

        // Assert
        money.Amount.Should().Be(12.35m);
        money.Currency.Should().Be("EGP");
    }

    [Fact]
    public void Given_EmptyCurrency_When_Constructed_Then_ThrowsArgumentException()
    {
        // Arrange, Act & Assert
        var act = () => new Money(100m, "   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Given_TwoMoneyInstancesWithSameCurrency_When_Added_Then_SumsAmounts()
    {
        // Arrange
        var m1 = new Money(100.25m, "EGP");
        var m2 = new Money(50.50m, "EGP");

        // Act
        var result = m1 + m2;

        // Assert
        result.Amount.Should().Be(150.75m);
        result.Currency.Should().Be("EGP");
    }

    [Fact]
    public void Given_TwoMoneyInstancesWithDifferentCurrencies_When_Added_Then_ThrowsInvalidOperationException()
    {
        // Arrange
        var m1 = new Money(100m, "EGP");
        var m2 = new Money(100m, "USD");

        // Act & Assert
        var act = () => _ = m1 + m2;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*different currencies*");
    }

    [Fact]
    public void Given_TwoMoneyInstancesWithSameCurrency_When_Subtracted_Then_SubtractsAmounts()
    {
        // Arrange
        var m1 = new Money(100.50m, "EGP");
        var m2 = new Money(40.20m, "EGP");

        // Act
        var result = m1 - m2;

        // Assert
        result.Amount.Should().Be(60.30m);
        result.Currency.Should().Be("EGP");
    }

    [Fact]
    public void Given_MoneyInstance_When_MultipliedByScalar_Then_MultipliesCorrectly()
    {
        // Arrange
        var money = new Money(25.50m, "EGP");

        // Act
        var result = money * 3m;

        // Assert
        result.Amount.Should().Be(76.50m);
        result.Currency.Should().Be("EGP");
    }

    [Fact]
    public void Given_MoneyInstance_When_DividedByZero_Then_ThrowsDivideByZeroException()
    {
        // Arrange
        var money = new Money(100m, "EGP");

        // Act & Assert
        var act = () => _ = money / 0m;
        act.Should().Throw<DivideByZeroException>();
    }

    [Fact]
    public void Given_TwoMoneyInstances_When_Comparing_Then_RelationalOperatorsWorkCorrectly()
    {
        // Arrange
        var m1 = new Money(100m, "EGP");
        var m2 = new Money(200m, "EGP");

        // Assert
        (m1 < m2).Should().BeTrue();
        (m1 <= m2).Should().BeTrue();
        (m2 > m1).Should().BeTrue();
        (m2 >= m1).Should().BeTrue();
        (m1 == new Money(100m, "EGP")).Should().BeTrue();
    }

    [Fact]
    public void Given_MoneyInstance_When_FormattedToString_Then_ShowsTwoDecimalPlacesAndCurrency()
    {
        // Arrange
        var money = new Money(42.5m, "EGP");

        // Act
        string formatted = money.ToString();

        // Assert
        formatted.Should().Be("42.50 EGP");
    }
}

