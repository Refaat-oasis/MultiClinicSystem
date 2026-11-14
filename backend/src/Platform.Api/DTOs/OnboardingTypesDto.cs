namespace Platform.Api.DTOs;

public sealed record OnboardingTypesDto(
    IReadOnlyList<SpecialtySelectionDto> Specialties);

