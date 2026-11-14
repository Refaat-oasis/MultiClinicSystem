using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class TimeEngineDomainTests
{
    [Fact]
    public void Given_SamplesBelowMinimumThreshold_When_CalculatingTimeStats_Then_DoesNotChangeBaselineDuration()
    {
        var samples = new List<TimeEngineSample>
        {
            new(45, false),
            new(50, false),
            new(48, false)
        };

        TimeEngineCalculationResult result = TimeEngineCalculator.Calculate(
            new TimeEngineCalculationInput(
                DefaultDurationMinutes: 30,
                CurrentAppliedDurationMinutes: 30,
                MinSampleSize: 5,
                TrimFraction: 0.15m,
                MaxStepChangePercent: 20m,
                IsDoctorEngineEnabled: true,
                Samples: samples));

        result.SampleCount.Should().Be(3);
        result.ExcusedCount.Should().Be(0);
        result.SuggestedDurationMinutes.Should().Be(30);
        result.AppliedDurationMinutes.Should().Be(30);
        result.IsAdopted.Should().BeFalse();
    }

    [Fact]
    public void Given_SingleExtremeOutlierSession_When_CalculatingTimeStats_Then_OutlierIsTrimmedWithoutDistortingAverage()
    {
        var samples = new List<TimeEngineSample>
        {
            new(19, false),
            new(20, false),
            new(20, false),
            new(21, false),
            new(180, false)
        };

        TimeEngineCalculationResult result = TimeEngineCalculator.Calculate(
            new TimeEngineCalculationInput(
                DefaultDurationMinutes: 30,
                CurrentAppliedDurationMinutes: 30,
                MinSampleSize: 5,
                TrimFraction: 0.15m,
                MaxStepChangePercent: 50m,
                IsDoctorEngineEnabled: true,
                Samples: samples));

        result.SampleCount.Should().Be(5);
        // Trimmed out 19 (lowest) and 180 (highest) -> average of (20, 20, 21) = 20.33
        result.TrimmedAverageMinutes.Should().Be(20.33m);
        result.SuggestedDurationMinutes.Should().Be(20);
        result.AppliedDurationMinutes.Should().Be(20);
        result.IsAdopted.Should().BeTrue();
    }

    [Fact]
    public void Given_LargeShiftAndTwentyPercentStepLimit_When_CalculatingTimeStats_Then_MovesWithinAllowedStepBounds()
    {
        var upwardSamples = Enumerable.Repeat(new TimeEngineSample(60, false), 6).ToList();
        TimeEngineCalculationResult upward = TimeEngineCalculator.Calculate(
            new TimeEngineCalculationInput(
                DefaultDurationMinutes: 30,
                CurrentAppliedDurationMinutes: 30,
                MinSampleSize: 5,
                TrimFraction: 0.10m,
                MaxStepChangePercent: 20m,
                IsDoctorEngineEnabled: true,
                Samples: upwardSamples));

        upward.TrimmedAverageMinutes.Should().Be(60m);
        upward.SuggestedDurationMinutes.Should().Be(36); // +20% of 30
        upward.AppliedDurationMinutes.Should().Be(36);

        var downwardSamples = Enumerable.Repeat(new TimeEngineSample(10, false), 6).ToList();
        TimeEngineCalculationResult downward = TimeEngineCalculator.Calculate(
            new TimeEngineCalculationInput(
                DefaultDurationMinutes: 30,
                CurrentAppliedDurationMinutes: 30,
                MinSampleSize: 5,
                TrimFraction: 0.10m,
                MaxStepChangePercent: 20m,
                IsDoctorEngineEnabled: true,
                Samples: downwardSamples));

        downward.SuggestedDurationMinutes.Should().Be(24); // -20% of 30
        downward.AppliedDurationMinutes.Should().Be(24);
    }

    [Fact]
    public void Given_ExcusedSessionsAndDoctorEngineDisabled_When_CalculatingTimeStats_Then_ExcludesExcusedAndRevertsAppliedWhenOff()
    {
        var samples = new List<TimeEngineSample>
        {
            new(24, false),
            new(25, false),
            new(25, false),
            new(26, false),
            new(25, false),
            new(150, true) // Excused complicated surgery
        };

        TimeEngineCalculationResult whenDisabled = TimeEngineCalculator.Calculate(
            new TimeEngineCalculationInput(
                DefaultDurationMinutes: 35,
                CurrentAppliedDurationMinutes: 25,
                MinSampleSize: 5,
                TrimFraction: 0.10m,
                MaxStepChangePercent: 50m,
                IsDoctorEngineEnabled: false,
                Samples: samples));

        whenDisabled.SampleCount.Should().Be(5);
        whenDisabled.ExcusedCount.Should().Be(1);
        whenDisabled.TrimmedAverageMinutes.Should().Be(25m);
        whenDisabled.SuggestedDurationMinutes.Should().Be(25);
        whenDisabled.AppliedDurationMinutes.Should().Be(35);
        whenDisabled.IsAdopted.Should().BeFalse();
    }

    [Fact]
    public void Given_SessionDurationEntity_When_MarkedExcused_Then_TogglesExcuseAndMaintainsRawMinutes()
    {
        var now = new DateTimeOffset(2026, 9, 10, 10, 0, 0, TimeSpan.Zero);
        var row = new SessionDuration(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            rawMinutes: 75, plannedMinutes: 30, isExcused: false, excuseReason: null,
            occurredAtUtc: now, operationId: null, createdAtUtc: now);

        row.SetExcused(true, "Complex emergency extraction", now.AddMinutes(5));
        row.RawMinutes.Should().Be(75);
        row.IsExcused.Should().BeTrue();
        row.ExcuseReason.Should().Be("Complex emergency extraction");
    }
}

