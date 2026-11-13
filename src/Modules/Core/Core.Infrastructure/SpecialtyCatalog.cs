using Core.Application;

namespace Core.Infrastructure;

/// <summary>
/// Built-in medical specialty catalog providing default clinical schemas and appointment categories.
/// </summary>
public sealed class SpecialtyCatalog : ISpecialtyCatalog
{
    private static readonly IReadOnlyList<SpecialtyCatalogItemResult> Items =
    [
        new(
            CatalogCode: "dental",
            NameAr: "طب الأسنان",
            NameEn: "Dental",
            HasDentalPlugin: true,
            DefaultFormSchemaJson: """{"fields":["chiefComplaint","toothChart","hygieneScore","treatmentPlan"]}""",
            DefaultAppointmentCategoriesJson: """[{"nameAr":"كشف أسنان","nameEn":"Dental Consultation","durationMinutes":30,"defaultPrice":300},{"nameAr":"حشو وعلاج جذور","nameEn":"Filling & Root Canal","durationMinutes":45,"defaultPrice":800}]"""),
        new(
            CatalogCode: "cardiology",
            NameAr: "القلب والأوعية الدموية",
            NameEn: "Cardiology",
            HasDentalPlugin: false,
            DefaultFormSchemaJson: """{"fields":["bloodPressure","ecgSummary","echoNotes","medications"]}""",
            DefaultAppointmentCategoriesJson: """[{"nameAr":"كشف قلب","nameEn":"Cardiology Visit","durationMinutes":30,"defaultPrice":500},{"nameAr":"رسم قلب وإيكو","nameEn":"ECG & Echocardiogram","durationMinutes":45,"defaultPrice":950}]"""),
        new(
            CatalogCode: "ophthalmology",
            NameAr: "طب وجراحة العيون",
            NameEn: "Ophthalmology",
            HasDentalPlugin: false,
            DefaultFormSchemaJson: """{"fields":["visualAcuityRight","visualAcuityLeft","iop","fundusExam"]}""",
            DefaultAppointmentCategoriesJson: """[{"nameAr":"كشف رمد","nameEn":"Eye Examination","durationMinutes":25,"defaultPrice":400}]"""),
        new(
            CatalogCode: "dermatology",
            NameAr: "الأمراض الجلدية والتجميل",
            NameEn: "Dermatology",
            HasDentalPlugin: false,
            DefaultFormSchemaJson: """{"fields":["skinType","lesionLocation","dermatoscopyNotes"]}""",
            DefaultAppointmentCategoriesJson: """[{"nameAr":"كشف جلدية","nameEn":"Dermatology Consultation","durationMinutes":20,"defaultPrice":350}]"""),
        new(
            CatalogCode: "pediatrics",
            NameAr: "طب الأطفال وحديثي الولادة",
            NameEn: "Pediatrics",
            HasDentalPlugin: false,
            DefaultFormSchemaJson: """{"fields":["weightKg","heightCm","headCircumference","immunizations"]}""",
            DefaultAppointmentCategoriesJson: """[{"nameAr":"كشف أطفال","nameEn":"Pediatric Visit","durationMinutes":25,"defaultPrice":300}]"""),
        new(
            CatalogCode: "ent",
            NameAr: "الأنف والأذن والحنجرة",
            NameEn: "ENT",
            HasDentalPlugin: false,
            DefaultFormSchemaJson: """{"fields":["earExam","nasalExam","throatExam"]}""",
            DefaultAppointmentCategoriesJson: """[{"nameAr":"كشف أنف وأذن","nameEn":"ENT Examination","durationMinutes":25,"defaultPrice":350}]"""),
        new(
            CatalogCode: "orthopedics",
            NameAr: "جراحة العظام",
            NameEn: "Orthopedics",
            HasDentalPlugin: false,
            DefaultFormSchemaJson: """{"fields":["affectedJoint","rangeOfMotion","imagingSummary"]}""",
            DefaultAppointmentCategoriesJson: """[{"nameAr":"كشف عظام","nameEn":"Orthopedics Visit","durationMinutes":30,"defaultPrice":400}]"""),
        new(
            CatalogCode: "gynecology",
            NameAr: "النساء والتوليد",
            NameEn: "Gynecology",
            HasDentalPlugin: false,
            DefaultFormSchemaJson: """{"fields":["gravidaPara","ultrasoundNotes","gestationalAge"]}""",
            DefaultAppointmentCategoriesJson: """[{"nameAr":"كشف نساء وتوليد","nameEn":"Gynecology Visit","durationMinutes":30,"defaultPrice":400}]"""),
        new(
            CatalogCode: "internal_medicine",
            NameAr: "الباطنة العامة",
            NameEn: "Internal Medicine",
            HasDentalPlugin: false,
            DefaultFormSchemaJson: """{"fields":["systemReview","chronicConditions","labReview"]}""",
            DefaultAppointmentCategoriesJson: """[{"nameAr":"كشف باطنة","nameEn":"Internal Medicine Visit","durationMinutes":30,"defaultPrice":350}]"""),
        new(
            CatalogCode: "general_practice",
            NameAr: "الممارسة العامة",
            NameEn: "General Practice",
            HasDentalPlugin: false,
            DefaultFormSchemaJson: """{"fields":["chiefComplaint","vitals","generalAssessment"]}""",
            DefaultAppointmentCategoriesJson: """[{"nameAr":"كشف عام","nameEn":"General Consultation","durationMinutes":20,"defaultPrice":250}]"""),
        new(
            CatalogCode: "physiotherapy",
            NameAr: "العلاج الطبيعي",
            NameEn: "Physiotherapy",
            HasDentalPlugin: false,
            DefaultFormSchemaJson: """{"fields":["painScale","mobilityAssessment","therapyProtocol"]}""",
            DefaultAppointmentCategoriesJson: """[{"nameAr":"جلسة علاج طبيعي","nameEn":"Physiotherapy Session","durationMinutes":45,"defaultPrice":300}]"""),
        new(
            CatalogCode: "other",
            NameAr: "تخصص آخر",
            NameEn: "Other",
            HasDentalPlugin: false,
            DefaultFormSchemaJson: """{"fields":["chiefComplaint","clinicalNotes","followUpPlan"]}""",
            DefaultAppointmentCategoriesJson: """[{"nameAr":"كشف تخصصي","nameEn":"Specialty Consultation","durationMinutes":30,"defaultPrice":300}]""")
    ];

    public IReadOnlyList<SpecialtyCatalogItemResult> GetAll() => Items;

    public SpecialtyCatalogItemResult? FindByCode(string? catalogCode)
    {
        if (string.IsNullOrWhiteSpace(catalogCode))
        {
            return null;
        }

        string normalized = catalogCode.Trim().ToLowerInvariant();
        return Items.FirstOrDefault(i => string.Equals(i.CatalogCode, normalized, StringComparison.OrdinalIgnoreCase));
    }
}

