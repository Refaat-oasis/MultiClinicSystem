namespace Platform.Api.DTOs;

public sealed record RegisterPrintedPinDto(
    string FileNumber,
    DateOnly DateOfBirth,
    string PrintedPin,
    string Email);

