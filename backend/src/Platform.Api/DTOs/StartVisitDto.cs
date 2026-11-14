namespace Platform.Api.DTOs;

/// <summary>
/// Request payload to start or resume a clinical visit session (with optional client-generated GUID v7 Id and OperationId).
/// </summary>
public sealed class StartVisitDto
{
    public Guid? Id { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid? PatientId { get; set; }
    public Guid? DoctorId { get; set; }
    public string? SpecialtyCode { get; set; }
    public Guid? RoomId { get; set; }
    public int? PlannedDurationMinutes { get; set; }
    public bool IsVisibleToPatient { get; set; } = true;
    public Guid? OperationId { get; set; }
    public Guid? CategoryId { get; set; }
}

