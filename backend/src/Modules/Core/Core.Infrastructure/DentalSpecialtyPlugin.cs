using Core.Application;

namespace Core.Infrastructure;

/// <summary>
/// Specialty plugin for dental clinics enabling tooth charting and providing the default dental examination schema.
/// </summary>
public sealed class DentalSpecialtyPlugin : ISpecialtyPlugin
{
    public string SpecialtyCode => "dental";
    public bool SupportsDentalChart => true;
    public string DefaultFormTitleAr => "نموذج فحص الأسنان";
    public string DefaultFormTitleEn => "Dental Examination Form";
    public string DefaultFormJsonSchema => """
    {
      "type": "object",
      "required": ["oralHygiene", "gingivalStatus"],
      "properties": {
        "oralHygiene": { "type": "string" },
        "gingivalStatus": { "type": "string" },
        "occlusionNotes": { "type": "string" },
        "painScale": { "type": "integer", "minimum": 0, "maximum": 10 }
      }
    }
    """;
}

