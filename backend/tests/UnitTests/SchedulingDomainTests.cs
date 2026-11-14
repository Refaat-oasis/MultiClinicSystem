using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class SchedulingDomainTests
{
    [Theory]
    [InlineData(AppointmentStatus.Booked, AppointmentStatus.Arrived, true)]
    [InlineData(AppointmentStatus.Booked, AppointmentStatus.Cancelled, true)]
    [InlineData(AppointmentStatus.Booked, AppointmentStatus.NoShow, true)]
    [InlineData(AppointmentStatus.Booked, AppointmentStatus.Late, true)]
    [InlineData(AppointmentStatus.Booked, AppointmentStatus.Completed, false)]
    [InlineData(AppointmentStatus.Booked, AppointmentStatus.InSession, false)]
    [InlineData(AppointmentStatus.Late, AppointmentStatus.Arrived, true)]
    [InlineData(AppointmentStatus.Late, AppointmentStatus.NoShow, true)]
    [InlineData(AppointmentStatus.Arrived, AppointmentStatus.InSession, true)]
    [InlineData(AppointmentStatus.Arrived, AppointmentStatus.Booked, false)]
    [InlineData(AppointmentStatus.InSession, AppointmentStatus.Completed, true)]
    [InlineData(AppointmentStatus.InSession, AppointmentStatus.NoShow, false)]
    [InlineData(AppointmentStatus.Completed, AppointmentStatus.Cancelled, false)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Booked, false)]
    [InlineData(AppointmentStatus.NoShow, AppointmentStatus.Arrived, false)]
    public void Given_AppointmentStateMachine_When_CheckingTransition_Then_MatchesSpecification(
        AppointmentStatus from,
        AppointmentStatus to,
        bool expected)
    {
        AppointmentStateMachine.CanTransition(from, to).Should().Be(expected);
    }

    [Fact]
    public void Given_AdjacentIntervalsAtHourBoundary_When_CheckedForOverlap_Then_DoNotOverlap()
    {
        DateTimeOffset aStart = new(2026, 5, 10, 9, 0, 0, TimeSpan.Zero);
        DateTimeOffset aEnd = new(2026, 5, 10, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset bStart = new(2026, 5, 10, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset bEnd = new(2026, 5, 10, 11, 0, 0, TimeSpan.Zero);

        AvailabilityCalculator.IntervalsOverlap(aStart, aEnd, bStart, bEnd).Should().BeFalse();
        AvailabilityCalculator.IntervalsOverlap(aStart, aEnd.AddMinutes(1), bStart, bEnd).Should().BeTrue();
    }

    [Fact]
    public void Given_WorkingHoursOverridesDaysOffAndRoomClash_When_ComputingFreeSlots_Then_RespectsAllConstraints()
    {
        Guid doc1 = Guid.NewGuid();
        Guid doc2 = Guid.NewGuid();
        Guid sharedRoom = Guid.NewGuid();

        // 2026-05-11 is Monday (DayOfWeek = 1)
        List<AvailabilityDoctorProfile> doctors =
        [
            new(doc1, sharedRoom, "dental"),
            new(doc2, sharedRoom, "dermatology")
        ];

        List<AvailabilityWorkingHourWindow> workingHours =
        [
            new(null, (int)DayOfWeek.Monday, "09:00", "11:00", false),
            new(doc1, (int)DayOfWeek.Monday, "10:00", "11:00", false)
        ];

        List<AvailabilityDayOffRange> daysOff = [];
        List<AvailabilityBusyInterval> busy =
        [
            // doc2 has 10:00-10:30 in sharedRoom -> blocks BOTH doc2 and doc1 at 10:00 because they share sharedRoom!
            new(doc2, sharedRoom, new DateTimeOffset(2026, 5, 11, 10, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 5, 11, 10, 30, 0, TimeSpan.Zero))
        ];

        DateTimeOffset fromUtc = new(2026, 5, 11, 8, 0, 0, TimeSpan.Zero);
        DateTimeOffset toUtc = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset nowUtc = new(2026, 5, 11, 8, 30, 0, TimeSpan.Zero);

        // With 60-minute lead time -> earliest slot is 09:30!
        IReadOnlyList<AvailableSlotCandidate> slots = AvailabilityCalculator.ComputeFreeSlots(
            fromUtc, toUtc, nowUtc,
            durationMinutes: 30, slotStepMinutes: 30, leadTimeMinutes: 60,
            clinicTimeZone: TimeZoneInfo.Utc,
            doctors: doctors, workingHours: workingHours, daysOff: daysOff, busyIntervals: busy);

        // doc1 works 10:00-11:00 (override); 10:00 blocked by sharedRoom clash -> only 10:30 left!
        // doc2 works 09:00-11:00; 09:00 blocked by lead time (09:30 earliest), 10:00 busy -> 09:30 and 10:30 left!
        slots.Where(s => s.DoctorId == doc1).Select(s => s.StartUtc.Hour * 100 + s.StartUtc.Minute)
            .Should().Equal([1030]);
        slots.Where(s => s.DoctorId == doc2).Select(s => s.StartUtc.Hour * 100 + s.StartUtc.Minute)
            .Should().Equal([930, 1030]);

        // Closed day excludes everyone on that date
        List<AvailabilityDayOffRange> closedMonday = [new(null, "2026-05-11", "2026-05-11")];
        AvailabilityCalculator.ComputeFreeSlots(
            fromUtc, toUtc, nowUtc, 30, 30, 0, TimeZoneInfo.Utc,
            doctors, workingHours, closedMonday, []).Should().BeEmpty();
    }

    [Fact]
    public void Given_WaitingQueue_When_VipAndRushArrive_Then_RespectsJumpLimitAndPushBackRules()
    {
        DateTimeOffset t0 = new(2026, 5, 11, 9, 0, 0, TimeSpan.Zero);
        Guid n1 = Guid.NewGuid();
        Guid n2 = Guid.NewGuid();
        Guid n3 = Guid.NewGuid();

        List<QueueReorderEntryInput> waiting =
        [
            new(n1, AppointmentPriority.Normal, 1, t0),
            new(n2, AppointmentPriority.Normal, 2, t0.AddMinutes(1)),
            new(n3, AppointmentPriority.Normal, 3, t0.AddMinutes(2))
        ];

        // VIP with limit 2 jumps over n3 & n2 -> lands at position 2 (n1 stays #1)
        var vipPlan = QueueReorderCalculator.PlanInsertion(waiting, AppointmentPriority.Vip, vipJumpLimit: 2);
        vipPlan.InsertedOrderIndex.Should().Be(2);
        vipPlan.ActualJumps.Should().Be(2);
        vipPlan.ReorderedExisting.Single(x => x.Id == n1).NewOrderIndex.Should().Be(1);
        vipPlan.ReorderedExisting.Single(x => x.Id == n2).NewOrderIndex.Should().Be(3);
        vipPlan.ReorderedExisting.Single(x => x.Id == n3).NewOrderIndex.Should().Be(4);

        // Push n1 back one place -> swaps with n2
        IReadOnlyList<QueueReorderOutputItem> pushed = QueueReorderCalculator.PlanPushBackOnePlace(waiting, n1);
        pushed.Single(x => x.Id == n2).NewOrderIndex.Should().Be(1);
        pushed.Single(x => x.Id == n1).NewOrderIndex.Should().Be(2);
        pushed.Single(x => x.Id == n3).NewOrderIndex.Should().Be(3);
    }
}

