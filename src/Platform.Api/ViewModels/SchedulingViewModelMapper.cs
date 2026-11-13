using Core.Application;

namespace Platform.Api.ViewModels;

/// <summary>
/// Maps scheduling and queue application read models into API ViewModels.
/// </summary>
public static class SchedulingViewModelMapper
{
    public static ClinicSchedulingSettingViewModel ToViewModel(ClinicSchedulingSettingResult s) => new(
        s.VipJumpLimit, s.LateGraceMinutes, s.MaxQueuePushes, s.DefaultSlotStepMinutes, s.MinBookingLeadMinutes, s.RescheduleOptionsJson);

    public static AppointmentCategoryViewModel ToViewModel(AppointmentCategoryResult c) => new(
        c.Id, c.ClinicId, c.SpecialtyCode, c.NameAr, c.NameEn, c.DefaultDurationMinutes,
        c.DefaultPriceAmount, c.DefaultPriceCurrency, c.BookableOnline, c.IsActive, c.CreatedAtUtc);

    public static DoctorBookingSettingViewModel ToViewModel(DoctorBookingSettingResult s) => new(
        s.DoctorId, s.AcceptsOnlineBooking, s.MaxDailyOnlineBookings);

    public static FreeSlotItemViewModel ToViewModel(FreeSlotItemResult s) => new(
        s.DoctorId, s.DoctorNameAr, s.DoctorNameEn, s.RoomId, s.RoomCode, s.StartUtc, s.EndUtc, s.DurationMinutes);

    public static AppointmentItemViewModel ToViewModel(AppointmentItemResult a) => new(
        a.Id, a.ClinicId, a.PatientId, a.PatientName, a.PatientFileNumber, a.DoctorId,
        a.DoctorNameAr, a.DoctorNameEn, a.SpecialtyCode, a.RoomId, a.RoomCode, a.CategoryId,
        a.CategoryNameAr, a.CategoryNameEn, a.StartUtc, a.EndUtc, a.Status, a.Priority,
        a.Source, a.PushCount, a.PlanSessionId, a.Notes, a.CancelReason, a.AllowedNextStatuses, a.CreatedAtUtc);

    public static QueueEntryItemViewModel ToViewModel(QueueEntryItemResult q) => new(
        q.Id, q.ClinicId, q.AppointmentId, q.PatientId, q.PatientName, q.PatientFileNumber,
        q.DoctorId, q.DoctorNameAr, q.QueueDate, q.Priority, q.OrderIndex, q.OriginalPosition,
        q.VipJumpCount, q.PushCount, q.Status, q.ArrivedAtUtc, q.CalledAtUtc, q.ServedAtUtc);

    public static WaitingListItemViewModel ToViewModel(WaitingListItemResult w) => new(
        w.Id, w.ClinicId, w.PatientId, w.PatientName, w.PatientFileNumber, w.DoctorId,
        w.CategoryId, w.PreferredDate, w.Notes, w.IsFulfilled, w.CreatedAtUtc);

    public static SlotOfferItemViewModel ToViewModel(SlotOfferItemResult o) => new(
        o.Id, o.ClinicId, o.WaitingListEntryId, o.PatientId, o.PatientName, o.DoctorId,
        o.RoomId, o.CategoryId, o.SlotStartUtc, o.SlotEndUtc, o.Status, o.OfferedAtUtc);

    public static CloseDayViewModel ToViewModel(CloseDayResult r) => new(
        r.DayOffId, r.StartDate, r.EndDate, r.AffectedAppointments.Select(ToViewModel).ToList());

    public static DoctorTimetableViewModel ToViewModel(DoctorTimetableResult t) => new(
        t.DoctorId, t.DoctorNameAr, t.DoctorNameEn, t.Date, t.TotalCount,
        t.CountByCategory, t.CountByStatus, t.Appointments.Select(ToViewModel).ToList());

    public static ClinicScheduleInspectViewModel ToViewModel(ClinicScheduleInspectResult r) => new(
        r.ClinicId,
        r.Date,
        r.Categories.Select(ToViewModel).ToList(),
        r.Appointments.Select(ToViewModel).ToList(),
        r.QueueEntries.Select(ToViewModel).ToList(),
        r.WaitingList.Select(ToViewModel).ToList());
}

