using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Single medication line item belonging to a visit prescription.
/// </summary>
public sealed class PrescriptionItem : ITenantEntity
{
    public Guid Id { get; private set; }
    public Guid ClinicId { get; set; }
    public Guid PrescriptionId { get; private set; }
    public string DrugName { get; private set; } = string.Empty;
    public string? ActiveIngredient { get; private set; }
    public string Dosage { get; private set; } = string.Empty;
    public string Frequency { get; private set; } = string.Empty;
    public string Duration { get; private set; } = string.Empty;
    public string? Instructions { get; private set; }
    public bool TriggeredAllergyWarning { get; private set; }
    public bool IsVoided { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private PrescriptionItem()
    {
    }

    public PrescriptionItem(
        Guid id,
        Guid clinicId,
        Guid prescriptionId,
        string drugName,
        string? activeIngredient,
        string dosage,
        string frequency,
        string duration,
        string? instructions,
        bool triggeredAllergyWarning)
    {
        Id = id == Guid.Empty ? Guid.CreateVersion7() : id;
        ClinicId = clinicId;
        PrescriptionId = prescriptionId;
        DrugName = drugName.Trim();
        ActiveIngredient = activeIngredient?.Trim();
        Dosage = dosage.Trim();
        Frequency = frequency.Trim();
        Duration = duration.Trim();
        Instructions = instructions?.Trim();
        TriggeredAllergyWarning = triggeredAllergyWarning;
    }
}

