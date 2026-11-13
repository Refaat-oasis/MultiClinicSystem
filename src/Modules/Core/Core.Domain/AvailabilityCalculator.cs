using System.Globalization;

namespace Core.Domain;

/// <summary>
/// Pure domain engine computing bookable time slots over working hours, doctor overrides,
/// closed days, doctor/room occupancy, category duration, step, and lead time.
/// </summary>
public static class AvailabilityCalculator
{
    public static bool IntervalsOverlap(DateTimeOffset aStart, DateTimeOffset aEnd, DateTimeOffset bStart, DateTimeOffset bEnd)
    {
        return aStart < bEnd && bStart < aEnd;
    }

    public static bool IsInsideWorkingWindow(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        Guid doctorId,
        TimeZoneInfo clinicTimeZone,
        IReadOnlyList<AvailabilityWorkingHourWindow> workingHours,
        IReadOnlyList<AvailabilityDayOffRange> daysOff)
    {
        if (endUtc <= startUtc)
        {
            return false;
        }

        DateTimeOffset localStart = TimeZoneInfo.ConvertTime(startUtc, clinicTimeZone);
        DateTimeOffset localEnd = TimeZoneInfo.ConvertTime(endUtc, clinicTimeZone);

        DateOnly localStartDate = DateOnly.FromDateTime(localStart.DateTime);
        DateOnly localEndDate = DateOnly.FromDateTime(localEnd.DateTime);

        if (localEnd.TimeOfDay == TimeSpan.Zero && localEndDate == localStartDate.AddDays(1))
        {
            localEndDate = localStartDate;
        }

        if (localEndDate != localStartDate)
        {
            return false;
        }

        if (IsDateClosed(localStartDate, doctorId, daysOff))
        {
            return false;
        }

        AvailabilityWorkingHourWindow? window = ResolveDayWindow(localStartDate, doctorId, workingHours);
        if (window == null || window.IsClosed)
        {
            return false;
        }

        if (!TryParseTime(window.StartTime, out TimeSpan winStart) || !TryParseTime(window.EndTime, out TimeSpan winEnd))
        {
            return false;
        }

        TimeSpan reqStart = localStart.TimeOfDay;
        TimeSpan reqEnd = localEnd.TimeOfDay == TimeSpan.Zero && localEnd > localStart
            ? TimeSpan.FromHours(24)
            : localEnd.TimeOfDay;

        return reqStart >= winStart && reqEnd <= winEnd;
    }

    public static IReadOnlyList<AvailableSlotCandidate> ComputeFreeSlots(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        DateTimeOffset nowUtc,
        int durationMinutes,
        int slotStepMinutes,
        int leadTimeMinutes,
        TimeZoneInfo clinicTimeZone,
        IReadOnlyList<AvailabilityDoctorProfile> doctors,
        IReadOnlyList<AvailabilityWorkingHourWindow> workingHours,
        IReadOnlyList<AvailabilityDayOffRange> daysOff,
        IReadOnlyList<AvailabilityBusyInterval> busyIntervals,
        IReadOnlyDictionary<Guid, int>? durationOverridesByDoctor = null)
    {
        if (toUtc <= fromUtc || durationMinutes <= 0 || slotStepMinutes <= 0 || doctors.Count == 0)
        {
            return Array.Empty<AvailableSlotCandidate>();
        }

        DateTimeOffset earliestAllowedUtc = nowUtc.AddMinutes(Math.Max(0, leadTimeMinutes));
        DateTimeOffset effectiveFromUtc = fromUtc > earliestAllowedUtc ? fromUtc : earliestAllowedUtc;
        if (effectiveFromUtc >= toUtc)
        {
            return Array.Empty<AvailableSlotCandidate>();
        }

        DateTimeOffset localFrom = TimeZoneInfo.ConvertTime(effectiveFromUtc, clinicTimeZone);
        DateTimeOffset localTo = TimeZoneInfo.ConvertTime(toUtc, clinicTimeZone);
        DateOnly firstDate = DateOnly.FromDateTime(localFrom.DateTime);
        DateOnly lastDate = DateOnly.FromDateTime(localTo.DateTime);

        var results = new List<AvailableSlotCandidate>();

        for (DateOnly day = firstDate; day <= lastDate; day = day.AddDays(1))
        {
            foreach (AvailabilityDoctorProfile doctor in doctors)
            {
                if (IsDateClosed(day, doctor.DoctorId, daysOff))
                {
                    continue;
                }

                AvailabilityWorkingHourWindow? window = ResolveDayWindow(day, doctor.DoctorId, workingHours);
                if (window == null || window.IsClosed)
                {
                    continue;
                }

                if (!TryParseTime(window.StartTime, out TimeSpan winStart) ||
                    !TryParseTime(window.EndTime, out TimeSpan winEnd) ||
                    winEnd <= winStart)
                {
                    continue;
                }

                DateTime localWindowStart = day.ToDateTime(TimeOnly.FromTimeSpan(winStart));
                DateTime localWindowEnd = winEnd == TimeSpan.FromHours(24)
                    ? day.AddDays(1).ToDateTime(TimeOnly.MinValue)
                    : day.ToDateTime(TimeOnly.FromTimeSpan(winEnd));

                DateTimeOffset utcWindowStart = new(localWindowStart, clinicTimeZone.GetUtcOffset(localWindowStart));
                DateTimeOffset utcWindowEnd = new(localWindowEnd, clinicTimeZone.GetUtcOffset(localWindowEnd));

                int doctorDuration = durationOverridesByDoctor != null &&
                                     durationOverridesByDoctor.TryGetValue(doctor.DoctorId, out int customDuration) &&
                                     customDuration >= 5
                    ? customDuration
                    : durationMinutes;

                for (DateTimeOffset candidateStart = utcWindowStart;
                     candidateStart.AddMinutes(doctorDuration) <= utcWindowEnd;
                     candidateStart = candidateStart.AddMinutes(slotStepMinutes))
                {
                    DateTimeOffset candidateEnd = candidateStart.AddMinutes(doctorDuration);
                    if (candidateStart < effectiveFromUtc || candidateEnd > toUtc)
                    {
                        continue;
                    }

                    bool hasConflict = false;
                    foreach (AvailabilityBusyInterval busy in busyIntervals)
                    {
                        if (!IntervalsOverlap(candidateStart, candidateEnd, busy.StartUtc, busy.EndUtc))
                        {
                            continue;
                        }

                        bool sameDoctor = busy.DoctorId == doctor.DoctorId;
                        bool sameRoom = doctor.RoomId.HasValue && busy.RoomId.HasValue && busy.RoomId.Value == doctor.RoomId.Value;
                        if (sameDoctor || sameRoom)
                        {
                            hasConflict = true;
                            break;
                        }
                    }

                    if (!hasConflict)
                    {
                        results.Add(new AvailableSlotCandidate(doctor.DoctorId, doctor.RoomId, candidateStart, candidateEnd));
                    }
                }
            }
        }

        return results
            .OrderBy(s => s.StartUtc)
            .ThenBy(s => s.DoctorId)
            .ToList();
    }

    private static AvailabilityWorkingHourWindow? ResolveDayWindow(
        DateOnly localDate,
        Guid doctorId,
        IReadOnlyList<AvailabilityWorkingHourWindow> workingHours)
    {
        int dow = (int)localDate.DayOfWeek;
        AvailabilityWorkingHourWindow? doctorOverride = workingHours.FirstOrDefault(w => w.DoctorId == doctorId && w.DayOfWeek == dow);
        if (doctorOverride != null)
        {
            return doctorOverride;
        }

        return workingHours.FirstOrDefault(w => w.DoctorId == null && w.DayOfWeek == dow);
    }

    private static bool IsDateClosed(
        DateOnly localDate,
        Guid doctorId,
        IReadOnlyList<AvailabilityDayOffRange> daysOff)
    {
        foreach (AvailabilityDayOffRange range in daysOff)
        {
            if (range.DoctorId.HasValue && range.DoctorId.Value != doctorId)
            {
                continue;
            }

            if (DateOnly.TryParseExact(range.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly start) &&
                DateOnly.TryParseExact(range.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly end) &&
                localDate >= start &&
                localDate <= end)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseTime(string value, out TimeSpan result)
    {
        if (value == "24:00" || value == "24:00:00")
        {
            result = TimeSpan.FromHours(24);
            return true;
        }

        return TimeSpan.TryParseExact(value.Trim(), ["hh\\:mm", "h\\:mm", "hh\\:mm\\:ss"], CultureInfo.InvariantCulture, out result);
    }
}

