using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class ClinicalDomainTests
{
    [Fact]
    public void Given_VisitDurationExceeded_When_Completed_Then_ComputesOvertimeAndExcusedReasonCorrectly()
    {
        DateTimeOffset started = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
        var visit = new Visit(
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            "dental", null, 30, started, true, null, started);

        // Finished at 10:45 (45 min vs 30 planned -> 15 min overtime)
        visit.Complete(started.AddMinutes(45), isOvertimeExcused: true, overtimeExcuseReason: "Complex extraction", updatedAtUtc: started.AddMinutes(45));

        visit.Status.Should().Be(VisitStatus.Completed);
        visit.OvertimeMinutes.Should().Be(15);
        visit.IsOvertimeExcused.Should().BeTrue();
        visit.OvertimeExcuseReason.Should().Be("Complex extraction");
    }

    [Fact]
    public void Given_DentalToothCodes_When_MappingFdiAndUniversal_Then_ConvertsBothPermanentAndPrimaryAccurately()
    {
        DentalNotationConverter.TryResolveTooth("16", DentalNumberingSystem.Fdi, out string fdi1, out string uni1, out bool p1).Should().BeTrue();
        fdi1.Should().Be("16");
        uni1.Should().Be("3");
        p1.Should().BeFalse();

        DentalNotationConverter.TryResolveTooth("19", DentalNumberingSystem.Universal, out string fdi2, out string uni2, out bool p2).Should().BeTrue();
        fdi2.Should().Be("36");
        uni2.Should().Be("19");
        p2.Should().BeFalse();

        DentalNotationConverter.TryResolveTooth("55", DentalNumberingSystem.Fdi, out string fdi3, out string uni3, out bool p3).Should().BeTrue();
        fdi3.Should().Be("55");
        uni3.Should().Be("A");
        p3.Should().BeTrue();

        DentalNotationConverter.TryResolveTooth("99", null, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Given_HistoricalToothRecords_When_DerivingChartState_Then_ReturnsLatestConditionPerTooth()
    {
        Guid clinicId = Guid.NewGuid();
        Guid patId = Guid.NewGuid();
        Guid v1 = Guid.NewGuid();
        Guid v2 = Guid.NewGuid();
        DateTimeOffset t1 = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
        DateTimeOffset t2 = new(2026, 6, 8, 9, 0, 0, TimeSpan.Zero);

        var rec1 = new ToothRecord(Guid.NewGuid(), clinicId, patId, v1, "16", "3", false, ToothCondition.Caries, "[\"M\",\"O\"]", "Deep decay", t1);
        var rec2 = new ToothRecord(Guid.NewGuid(), clinicId, patId, v2, "16", "3", false, ToothCondition.Filled, "[\"M\",\"O\"]", "Composite restoration", t2);
        var rec3 = new ToothRecord(Guid.NewGuid(), clinicId, patId, v1, "21", "9", false, ToothCondition.Crown, "[]", "Porcelain crown", t1);

        var state = DentalNotationConverter.DeriveCurrentChartState([rec2, rec1, rec3]);

        state.Should().HaveCount(2);
        var tooth16 = state.Single(x => x.FdiToothNumber == "16");
        tooth16.Condition.Should().Be(ToothCondition.Filled);
        tooth16.LastVisitId.Should().Be(v2);
        tooth16.Surfaces.Should().Equal(["M", "O"]);
    }

    [Fact]
    public void Given_ClinicalFormSchema_When_ValidatingAnswers_Then_EnforcesRequiredFieldsAndNumericBounds()
    {
        const string schema = """
        {
          "type": "object",
          "required": ["rightEyeAcuity", "leftEyeAcuity"],
          "properties": {
            "rightEyeAcuity": { "type": "string" },
            "leftEyeAcuity": { "type": "string" },
            "iopMmHg": { "type": "number", "minimum": 5, "maximum": 60 },
            "dilated": { "type": "boolean" }
          }
        }
        """;

        var invalidMissing = ClinicalFormSchemaValidator.Validate(schema, """{"rightEyeAcuity":"6/6","iopMmHg":120}""");
        invalidMissing.IsValid.Should().BeFalse();
        invalidMissing.Errors.Should().Contain(e => e.Contains("leftEyeAcuity"));
        invalidMissing.Errors.Should().Contain(e => e.Contains("iopMmHg"));

        var valid = ClinicalFormSchemaValidator.Validate(schema, """{"rightEyeAcuity":"6/6","leftEyeAcuity":"6/9","iopMmHg":16,"dilated":true}""");
        valid.IsValid.Should().BeTrue();
        valid.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Given_WeightAndHeight_When_RecordingVitals_Then_CalculatesBmi()
    {
        var vitals = new VisitVital(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 120, 80, 72, 36.8m, 80m, 180m, 98, 95m, DateTimeOffset.UtcNow);
        vitals.Bmi.Should().Be(24.69m);
    }
}

