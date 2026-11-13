namespace Platform.Api.DTOs;

public sealed class ReserveNumberBlockDto
{
    public string? DeviceId { get; set; }
    public string Kind { get; set; } = "Receipt";
    public int? BlockSize { get; set; }
}
