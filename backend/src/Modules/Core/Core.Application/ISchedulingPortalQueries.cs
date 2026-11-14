using BuildingBlocks.Domain;

namespace Core.Application;

/// <summary>
/// Anti-corruption query interface implemented by the Scheduling module for upcoming appointments in the Patient Portal.
/// </summary>
public interface ISchedulingPortalQueries
{
    Task<Result<IReadOnlyList<PortalUpcomingAppointmentItemResult>>> ListUpcomingAppointmentsForPortalAsync(
        Guid clinicId,
        Guid patientId,
        bool showDoctorNames,
        CancellationToken cancellationToken = default);
}

