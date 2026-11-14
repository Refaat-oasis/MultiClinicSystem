namespace Core.Application;

/// <summary>
/// Specialty plugin contract registering default specialty JSON forms and whether dental charting is supported.
/// </summary>
public interface ISpecialtyPlugin
{
    string SpecialtyCode { get; }
    bool SupportsDentalChart { get; }
    string DefaultFormTitleAr { get; }
    string DefaultFormTitleEn { get; }
    string DefaultFormJsonSchema { get; }
}

