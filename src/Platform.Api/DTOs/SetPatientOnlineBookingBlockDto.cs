namespace Platform.Api.DTOs;

public sealed class SetPatientOnlineBookingBlockDto
{
    public bool Blocked { get; set; }
    public string? Reason { get; set; }
}
