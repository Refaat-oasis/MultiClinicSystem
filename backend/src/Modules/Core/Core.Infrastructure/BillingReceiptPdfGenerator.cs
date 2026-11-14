using System.Globalization;
using System.Text;
using Core.Application;

namespace Core.Infrastructure;

/// <summary>
/// Deterministic PDF 1.4 generator for clinic billing payment receipts containing receipt number, patient file, and method breakdown.
/// </summary>
public static class BillingReceiptPdfGenerator
{
    public static byte[] GenerateReceiptPdf(
        string clinicName,
        string patientFileNumber,
        string patientName,
        PaymentResult payment,
        decimal? remainingBalanceOnPatient)
    {
        ArgumentNullException.ThrowIfNull(payment);

        var lines = new List<string>
        {
            $"Clinic: {SanitizeAscii(clinicName)}",
            $"PAYMENT RECEIPT #{SanitizeAscii(payment.ReceiptNumber)}",
            $"Date (UTC): {payment.PaidAtUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC",
            $"Patient File: {SanitizeAscii(patientFileNumber)} | Patient: {SanitizeAscii(patientName)}",
            $"Payment Method: {payment.Method}",
            $"Amount Paid: {payment.Amount.ToString("F2", CultureInfo.InvariantCulture)} {SanitizeAscii(payment.Currency)}",
            $"Type: {(payment.IsDeposit ? "Advance Deposit" : "Charge Settlement")}",
            payment.ChargeId.HasValue ? $"Charge Ref: {payment.ChargeId.Value}" : "Charge Ref: Account Credit",
            $"Cashier Ref: {SanitizeAscii(payment.CollectedByUserId)}"
        };

        if (remainingBalanceOnPatient.HasValue)
        {
            lines.Add(
                $"Patient Account Balance After Payment: {remainingBalanceOnPatient.Value.ToString("F2", CultureInfo.InvariantCulture)} {SanitizeAscii(payment.Currency)}");
        }

        if (!string.IsNullOrWhiteSpace(payment.Notes))
        {
            lines.Add($"Notes: {SanitizeAscii(payment.Notes)}");
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

        var pdf = new StringBuilder();
        var offsets = new List<int> { 0 };

        pdf.Append("%PDF-1.4\n");

        void AddObject(int id, string body)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(CultureInfo.InvariantCulture, $"{id} 0 obj\n{body}\nendobj\n");
        }

        AddObject(1, "<< /Type /Catalog /Pages 2 0 R >>");
        AddObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        AddObject(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>");
        AddObject(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        AddObject(5, $"<< /Length {contentBytes.Length} >>\nstream\n{contentStream}endstream");

        int xrefStart = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {offsets.Count}\n");
        pdf.Append("0000000000 65535 f \n");
        for (int i = 1; i < offsets.Count; i++)
        {
            pdf.Append(CultureInfo.InvariantCulture, $"{offsets[i]:D10} 00000 n \n");
        }

        pdf.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xrefStart}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}

