namespace Platform.Api.DTOs;

public sealed class PortalBookAppointmentDto
{
    public Guid DoctorId { get; set; }
    public Guid CategoryId { get; set; }
    public DateTimeOffset StartAtUtc { get; set; }
    public string? Notes { get; set; }
    public Guid? OperationId { get; set; }
}
