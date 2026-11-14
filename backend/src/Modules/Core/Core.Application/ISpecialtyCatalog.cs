namespace Core.Application;

/// <summary>
/// Provides the platform catalog of clinic specialties and their default clinical/appointment templates.
/// </summary>
public interface ISpecialtyCatalog
{
    IReadOnlyList<SpecialtyCatalogItemResult> GetAll();
    SpecialtyCatalogItemResult? FindByCode(string? catalogCode);
}

