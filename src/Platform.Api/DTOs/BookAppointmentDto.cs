namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to book an appointment (supports client-generated GUID v7 Id and OperationId).
/// </summary>
public sealed class BookAppointmentDto
{
    public Guid? Id { get; set; }
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid? RoomId { get; set; }
    public Guid CategoryId { get; set; }
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset? EndUtc { get; set; }
    public string Priority { get; set; } = "Normal";
    public Guid? PlanSessionId { get; set; }
    public string? Notes { get; set; }
    public Guid? OperationId { get; set; }
}

