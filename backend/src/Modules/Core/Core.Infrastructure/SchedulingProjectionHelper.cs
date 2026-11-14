using System.Globalization;
using System.Text.Json;
using BuildingBlocks.Domain;
using Core.Application;
using Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Core.Infrastructure;

/// <summary>
/// Internal query projection and settings helpers shared across scheduling operations.
/// </summary>
internal static class SchedulingProjectionHelper
{
    public static Guid? GetDefaultRoomId(ClinicDoctor doctor)
    {
        try
        {
            List<Guid>? ids = JsonSerializer.Deserialize<List<Guid>>(doctor.AssignedRoomIdsJson);
            return ids is { Count: > 0 } ? ids[0] : null;
        }
        catch
        {
            return null;
        }
    }

    public static string GetPrimarySpecialty(ClinicDoctor doctor)
    {
        try
        {
            List<string>? codes = JsonSerializer.Deserialize<List<string>>(doctor.SpecialtyCodesJson);
            return codes is { Count: > 0 } ? codes[0] : "general";
        }
        catch
        {
            return "general";
        }
    }
    public static async Task<ClinicSchedulingSetting> GetOrCreateDefaultSettingsAsync(
        CoreDbContext dbContext,
        Guid clinicId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ClinicSchedulingSetting? existing = await dbContext.ClinicSchedulingSettings
            .FirstOrDefaultAsync(s => s.ClinicId == clinicId && !s.IsVoided, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var created = new ClinicSchedulingSetting(GuidV7.NewGuid(), clinicId, now);
        dbContext.ClinicSchedulingSettings.Add(created);
        await dbContext.SaveChangesAsync(cancellationToken);
        return created;
    }

    public static AppointmentCategoryResult ToCategoryResult(AppointmentCategory c) => new(
        c.Id,
        c.ClinicId,
        c.SpecialtyCode,
        c.NameAr,
        c.NameEn,
        c.DefaultDurationMinutes,
        c.DefaultPriceAmount,
        c.DefaultPriceCurrency,
        c.BookableOnline,
        c.IsActive,
        c.CreatedAtUtc);

    public static async Task<List<AppointmentItemResult>> HydrateAppointmentsAsync(
        CoreDbContext dbContext,
        Guid clinicId,
        IReadOnlyList<Appointment> appointments,
        CancellationToken cancellationToken)
    {
        if (appointments.Count == 0)
        {
            return [];
        }

        List<Guid> patientIds = appointments.Select(a => a.PatientId).Distinct().ToList();
        List<Guid> doctorIds = appointments.Select(a => a.DoctorId).Distinct().ToList();
        List<Guid> categoryIds = appointments.Select(a => a.CategoryId).Distinct().ToList();
        List<Guid> roomIds = appointments.Where(a => a.RoomId.HasValue).Select(a => a.RoomId!.Value).Distinct().ToList();

        Dictionary<Guid, (string FullName, string FileNumber)> patients = await dbContext.Patients
            .AsNoTracking()
            .Where(p => p.ClinicId == clinicId && patientIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => ValueTuple.Create(p.FullName, p.FileNumber), cancellationToken);

        Dictionary<Guid, ClinicDoctor> doctors = await dbContext.ClinicDoctors
            .AsNoTracking()
            .Where(d => d.ClinicId == clinicId && doctorIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        Dictionary<Guid, AppointmentCategory> categories = await dbContext.AppointmentCategories
            .AsNoTracking()
            .Where(c => c.ClinicId == clinicId && categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        Dictionary<Guid, string> rooms = roomIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.ClinicRooms
                .AsNoTracking()
                .Where(r => r.ClinicId == clinicId && roomIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => r.Code, cancellationToken);

        var result = new List<AppointmentItemResult>(appointments.Count);
        foreach (Appointment a in appointments)
        {
            patients.TryGetValue(a.PatientId, out (string FullName, string FileNumber) pat);
            doctors.TryGetValue(a.DoctorId, out ClinicDoctor? doc);
            categories.TryGetValue(a.CategoryId, out AppointmentCategory? cat);
            string? roomCode = a.RoomId.HasValue && rooms.TryGetValue(a.RoomId.Value, out string? rc) ? rc : null;
            List<string> allowedNext = AppointmentStateMachine.GetAllowedTransitions(a.Status)
                .Select(s => s.ToString())
                .ToList();

            result.Add(new AppointmentItemResult(
                a.Id,
                a.ClinicId,
                a.PatientId,
                pat.FullName ?? string.Empty,
                pat.FileNumber ?? string.Empty,
                a.DoctorId,
                doc?.FullName ?? string.Empty,
                doc?.FullName ?? string.Empty,
                doc != null ? GetPrimarySpecialty(doc) : string.Empty,
                a.RoomId,
                roomCode,
                a.CategoryId,
                cat?.NameAr ?? string.Empty,
                cat?.NameEn ?? string.Empty,
                a.StartUtc,
                a.EndUtc,
                a.Status.ToString(),
                a.Priority.ToString(),
                a.Source.ToString(),
                a.PushCount,
                a.PlanSessionId,
                a.Notes,
                a.CancelReason,
                allowedNext,
                a.CreatedAtUtc));
        }

        return result;
    }

    public static async Task<List<QueueEntryItemResult>> HydrateQueueAsync(
        CoreDbContext dbContext,
        Guid clinicId,
        IReadOnlyList<QueueEntry> entries,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        List<Guid> patientIds = entries.Select(q => q.PatientId).Distinct().ToList();
        List<Guid> doctorIds = entries.Select(q => q.DoctorId).Distinct().ToList();

        Dictionary<Guid, (string FullName, string FileNumber)> patients = await dbContext.Patients
            .AsNoTracking()
            .Where(p => p.ClinicId == clinicId && patientIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => ValueTuple.Create(p.FullName, p.FileNumber), cancellationToken);

        Dictionary<Guid, string> doctors = await dbContext.ClinicDoctors
            .AsNoTracking()
            .Where(d => d.ClinicId == clinicId && doctorIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.FullName, cancellationToken);

        return entries.Select(q =>
        {
            patients.TryGetValue(q.PatientId, out (string FullName, string FileNumber) pat);
            doctors.TryGetValue(q.DoctorId, out string? docName);
            return new QueueEntryItemResult(
                q.Id,
                q.ClinicId,
                q.AppointmentId,
                q.PatientId,
                pat.FullName ?? string.Empty,
                pat.FileNumber ?? string.Empty,
                q.DoctorId,
                docName ?? string.Empty,
                q.QueueDate,
                q.Priority.ToString(),
                q.OrderIndex,
                q.OriginalPosition,
                q.VipJumpCount,
                q.PushCount,
                q.Status.ToString(),
                q.ArrivedAtUtc,
                q.CalledAtUtc,
                q.ServedAtUtc);
        }).ToList();
    }

    public static string ToLocalDateString(DateTimeOffset utc, string? timeZoneId)
    {
        TimeZoneInfo tz = ClinicTimeZoneResolver.Resolve(timeZoneId);
        DateTimeOffset local = TimeZoneInfo.ConvertTime(utc, tz);
        return local.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
