using System.Globalization;
using System.Text;
using Core.Application;

namespace Core.Infrastructure;

/// <summary>
/// Deterministic PDF 1.4 generator for printable clinical prescriptions with clinic branding and medication tables.
/// </summary>
public static class ClinicalPdfGenerator
{
    public static byte[] GeneratePrescriptionPdf(
        string clinicName,
        string patientFileNumber,
        string patientName,
        string doctorName,
        string specialtyCode,
        PrescriptionResult prescription)
    {
        ArgumentNullException.ThrowIfNull(prescription);

        var lines = new List<string>
        {
            $"Clinic: {SanitizeAscii(clinicName)}",
            $"Doctor: {SanitizeAscii(doctorName)} ({SanitizeAscii(specialtyCode)})",
            $"Patient File: {SanitizeAscii(patientFileNumber)} | Patient: {SanitizeAscii(patientName)}",
            $"Prescription ID: {prescription.Id}",
            $"Date (UTC): {prescription.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC"
        };

        if (prescription.HasAllergyWarning && !string.IsNullOrWhiteSpace(prescription.AllergyWarningDetails))
        {
            lines.Add($"ALLERGY WARNING ACKNOWLEDGED: {SanitizeAscii(prescription.AllergyWarningDetails)}");
        }

        if (!string.IsNullOrWhiteSpace(prescription.Notes))
        {
            lines.Add($"Notes: {SanitizeAscii(prescription.Notes)}");
        }

        lines.Add("Medications:");
        int index = 1;
        foreach (PrescriptionItemResult item in prescription.Items)
        {
            lines.Add(
                $"{index}. {SanitizeAscii(item.DrugName)}"
                + (!string.IsNullOrWhiteSpace(item.ActiveIngredient) ? $" [{SanitizeAscii(item.ActiveIngredient)}]" : string.Empty)
                + $" - {SanitizeAscii(item.Dosage)} | {SanitizeAscii(item.Frequency)} | {SanitizeAscii(item.Duration)}");
            if (!string.IsNullOrWhiteSpace(item.Instructions))
            {
                lines.Add($"   Instructions: {SanitizeAscii(item.Instructions)}");
            }

            index++;
        }

        return BuildSimplePdfDocument(lines);
    }

    private static string SanitizeAscii(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var sb = new StringBuilder(value.Length);
        foreach (char ch in value.Trim())
        {
            if (ch is '\\' or '(' or ')')
            {
                sb.Append('\\').Append(ch);
            }
            else if (ch >= 32 && ch <= 126)
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append('?');
            }
        }

        return sb.ToString();
    }

    private static byte[] BuildSimplePdfDocument(List<string> lines)
    {
        var streamBuilder = new StringBuilder();
        streamBuilder.AppendLine("BT");
        streamBuilder.AppendLine("/F1 11 Tf");
        int y = 760;
        foreach (string line in lines)
        {
            if (y < 50)
            {
                break;
            }

            streamBuilder.AppendLine(CultureInfo.InvariantCulture, $"1 0 0 1 48 {y} Tm ({line}) Tj");
            y -= 18;
        }

        streamBuilder.AppendLine("ET");
        string contentStream = streamBuilder.ToString();
        byte[] contentBytes = Encoding.ASCII.GetBytes(contentStream);

        string[] objects =
        [
            "1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n",
            "2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n",
            "3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >> endobj\n",
            $"4 0 obj << /Length {contentBytes.Length} >> stream\n{contentStream}endstream endobj\n",
            "5 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj\n"
        ];

        using var ms = new MemoryStream();
        byte[] header = Encoding.ASCII.GetBytes("%PDF-1.4\n");
        ms.Write(header, 0, header.Length);

        var offsets = new List<long> { 0 };
        foreach (string obj in objects)
        {
            offsets.Add(ms.Position);
            byte[] bytes = Encoding.ASCII.GetBytes(obj);
            ms.Write(bytes, 0, bytes.Length);
        }

        long xrefOffset = ms.Position;
        var xref = new StringBuilder();
        xref.AppendLine(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}");
        xref.AppendLine("0000000000 65535 f ");
        for (int i = 1; i < offsets.Count; i++)
        {
            xref.AppendLine(CultureInfo.InvariantCulture, $"{offsets[i]:0000000000} 00000 n ");
        }

        xref.AppendLine(CultureInfo.InvariantCulture, $"trailer << /Size {objects.Length + 1} /Root 1 0 R >>");
        xref.AppendLine(CultureInfo.InvariantCulture, $"startxref\n{xrefOffset}\n%%EOF");
        byte[] trailerBytes = Encoding.ASCII.GetBytes(xref.ToString());
        ms.Write(trailerBytes, 0, trailerBytes.Length);
        return ms.ToArray();
    }
}

