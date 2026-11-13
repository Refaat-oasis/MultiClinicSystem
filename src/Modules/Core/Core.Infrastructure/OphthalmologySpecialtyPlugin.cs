using Core.Application;

namespace Core.Infrastructure;

/// <summary>
/// Specialty plugin for ophthalmology clinics registering an eye-exam JSON schema without dental charting tables.
/// </summary>
public sealed class OphthalmologySpecialtyPlugin : ISpecialtyPlugin
{
    public string SpecialtyCode => "ophthalmology";
    public bool SupportsDentalChart => false;
    public string DefaultFormTitleAr => "نموذج فحص العيون";
    public string DefaultFormTitleEn => "Ophthalmology Exam Form";
    public string DefaultFormJsonSchema => """
    {
      "type": "object",
      "required": ["rightEyeAcuity", "leftEyeAcuity"],
      "properties": {
        "rightEyeAcuity": { "type": "string" },
        "leftEyeAcuity": { "type": "string" },
        "iopMmHg": { "type": "number", "minimum": 5, "maximum": 60 },
        "dilated": { "type": "boolean" }
      }
    }
    """;
}

