using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class PortalBookingDomainTests
{
    [Theory]
    [InlineData(AppointmentStatus.Requested, AppointmentStatus.Booked, true)]
    [InlineData(AppointmentStatus.Requested, AppointmentStatus.Declined, true)]
    [InlineData(AppointmentStatus.Requested, AppointmentStatus.Cancelled, true)]
    [InlineData(AppointmentStatus.Requested, AppointmentStatus.Arrived, false)]
    [InlineData(AppointmentStatus.Requested, AppointmentStatus.Completed, false)]
    [InlineData(AppointmentStatus.Declined, AppointmentStatus.Booked, false)]
    [InlineData(AppointmentStatus.Declined, AppointmentStatus.Cancelled, false)]
    public void Given_PortalRequestStateMachine_When_CheckingTransition_Then_MatchesSpecification(
        AppointmentStatus from,
        AppointmentStatus to,
        bool expected)
    {
        AppointmentStateMachine.CanTransition(from, to).Should().Be(expected);
    }

    [Fact]
    public void Given_RequestedAppointment_When_PendingOrExpiredOrApprovedOrDeclined_Then_SlotOccupancyUpdatesAccordingly()
    {
        DateTimeOffset now = new(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
        DateTimeOffset holdUntil = now.AddHours(12);
        var appt = new Appointment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            now.AddDays(1),
            now.AddDays(1).AddMinutes(30),
            AppointmentPriority.Normal,
            AppointmentSource.Portal,
            null,
            "Portal request",
            null,
            now);

        appt.MarkAsPendingPortalRequest(holdUntil, now);
        appt.Status.Should().Be(AppointmentStatus.Requested);
        AppointmentStateMachine.IsOccupyingStatus(appt.Status).Should().BeTrue();
        AppointmentStateMachine.IsOccupying(appt, now.AddHours(1)).Should().BeTrue();
        AppointmentStateMachine.IsOccupying(appt, holdUntil).Should().BeFalse("expired requests must release slots");

        Guid reviewer = Guid.NewGuid();
        appt.ApprovePortalRequest(reviewer, "Confirmed", now.AddHours(13)).Should().BeFalse("expired request cannot be approved");
        appt.ApprovePortalRequest(reviewer, "Confirmed", now.AddHours(2)).Should().BeTrue();
        appt.Status.Should().Be(AppointmentStatus.Booked);
        appt.ApprovalExpiresAtUtc.Should().BeNull();
        AppointmentStateMachine.IsOccupying(appt, now.AddHours(24)).Should().BeTrue();

        var declinedAppt = new Appointment(
            Guid.NewGuid(),
            appt.ClinicId,
            appt.PatientId,
            appt.DoctorId,
            null,
            appt.CategoryId,
            now.AddDays(2),
            now.AddDays(2).AddMinutes(30),
            AppointmentPriority.Normal,
            AppointmentSource.Portal,
            null,
            null,
            null,
            now);
        declinedAppt.MarkAsPendingPortalRequest(holdUntil, now);
        declinedAppt.DeclinePortalRequest(reviewer, "Doctor unavailable", now.AddHours(1)).Should().BeTrue();
        declinedAppt.Status.Should().Be(AppointmentStatus.Declined);
        AppointmentStateMachine.IsOccupying(declinedAppt, now.AddHours(1)).Should().BeFalse();
    }

    [Fact]
    public void Given_PatientAccount_When_ThreeNoShowsOccur_Then_AutoBlocksOnlineBookingUntilManualUnblock()
    {
        DateTimeOffset now = new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);
        var account = new PatientAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "p@clinic.local", false, null, now);

        account.IsOnlineBookingBlocked.Should().BeFalse();
        account.RecordNoShow(3, now.AddDays(1));
        account.RecordNoShow(3, now.AddDays(2));
        account.NoShowCount.Should().Be(2);
        account.IsOnlineBookingBlocked.Should().BeFalse();

        account.RecordNoShow(3, now.AddDays(3));
        account.NoShowCount.Should().Be(3);
        account.IsOnlineBookingBlocked.Should().BeTrue();
        account.OnlineBookingBlockReason.Should().Be("AutoBlockedAfterNoShows");

        account.UnblockOnlineBooking(now.AddDays(4));
        account.IsOnlineBookingBlocked.Should().BeFalse();
        account.NoShowCount.Should().Be(3, "historical counter remains accurate while streak resets on unblock");
        account.NoShowsSinceUnblock.Should().Be(0);

        account.RecordNoShow(3, now.AddDays(5));
        account.IsOnlineBookingBlocked.Should().BeFalse("patient gets fresh threshold after manual unblock");
    }

    [Fact]
    public void Given_ClinicPortalBookingSetting_When_UpdatedWithValidOrInvalidBounds_Then_EnforcesDefaultsAndValidation()
    {
        DateTimeOffset now = new(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);
        var setting = new ClinicPortalBookingSetting(Guid.NewGuid(), Guid.NewGuid(), now);

        setting.Mode.Should().Be(PortalBookingMode.AutoConfirm);
        setting.MaxActiveFutureBookingsPerPatient.Should().Be(2);
        setting.CancellationCutoffHours.Should().Be(12);
        setting.AutoBlockAfterNoShows.Should().Be(3);
        setting.ApprovalHoldHours.Should().Be(12);

        setting.Update(PortalBookingMode.RequiresApproval, 60, 45, 30, 3, 25, 24, 2, 6, false, now.AddMinutes(5));
        setting.Mode.Should().Be(PortalBookingMode.RequiresApproval);
        setting.MinLeadTimeMinutes.Should().Be(60);
        setting.ShowPricesWhenAvailable.Should().BeFalse();

        Action invalidAction = () => setting.Update(PortalBookingMode.AutoConfirm, -1, 30, 15, 2, 20, 12, 3, 12, true, now);
        invalidAction.Should().Throw<ArgumentOutOfRangeException>();
    }
}
