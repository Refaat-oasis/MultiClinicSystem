namespace Core.Application;

public sealed record RegisterPrintedPinInput(
    string FileNumber,
    DateOnly DateOfBirth,
    string PrintedPin,
    string Email);

