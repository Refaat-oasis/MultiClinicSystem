using System.Globalization;
using System.Text;
using Core.Application;

namespace Core.Infrastructure;

/// <summary>
/// Deterministic license-free PDF 1.4 report exporter matching Phase 14 screen numbers to the cent.
/// </summary>
public static class ReportsPdfGenerator
{
    public static byte[] GenerateIncomeVsExpensesPdf(string clinicName, IncomeVsExpensesReportResult report)
    {
        var lines = new List<string>
        {
            $"Clinic: {SanitizeAscii(clinicName)}",
            $"REPORT: INCOME VS EXPENSES ({report.FromDate:yyyy-MM-dd} to {report.ToDate:yyyy-MM-dd})",
            $"GrossPayments: {F(report.GrossPayments)} {report.Currency}",
            $"Refunds: {F(report.Refunds)} {report.Currency}",
            $"NetIncome: {F(report.NetIncome)} {report.Currency}",
            $"OperatingExpenses: {F(report.OperatingExpenses)} {report.Currency}",
            $"SupplierPayments: {F(report.SupplierPayments)} {report.Currency}",
            $"SupplierAccruals: {F(report.SupplierAccruals)} {report.Currency}",
            $"FixedCostsTotal: {F(report.FixedCostsTotal)} {report.Currency}",
            $"PayrollTotal: {F(report.PayrollTotal)} {report.Currency}",
            $"TotalExpenses: {F(report.TotalExpenses)} {report.Currency}",
            $"NetProfit: {F(report.NetProfit)} {report.Currency}"
        };

        return BuildSimplePdfDocument(lines);
    }

    public static byte[] GenerateDoctorSummaryPdf(string clinicName, DateOnly fromDate, DateOnly toDate, IReadOnlyList<DoctorFinancialReportRowResult> rows)
    {
        var lines = new List<string>
        {
            $"Clinic: {SanitizeAscii(clinicName)}",
            $"REPORT: DOCTOR SUMMARY ({fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd})"
        };

        foreach (var r in rows)
        {
            lines.Add(
                $"{SanitizeAscii(r.DoctorName)} ({SanitizeAscii(r.SpecialtyCode)}) | Sessions:{r.CompletedSessionsCount}/{r.SessionsCount} | Collected:{F(r.CollectedTotal)} | Refunds:{F(r.RefundedTotal)} | Tools:{F(r.ToolsCost)} | Lab:{F(r.LabCost)} | Payroll:{F(r.PayrollCost)} | Net:{F(r.NetContribution)}");
        }

        return BuildSimplePdfDocument(lines);
    }

    public static byte[] GenerateSessionsAndCategoriesPdf(string clinicName, DateOnly fromDate, DateOnly toDate, IReadOnlyList<SessionCategoryReportRowResult> rows)
    {
        var lines = new List<string>
        {
            $"Clinic: {SanitizeAscii(clinicName)}",
            $"REPORT: SESSIONS & CATEGORIES ({fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd})"
        };

        foreach (var r in rows)
        {
            lines.Add($"{r.VisitDate:yyyy-MM-dd} | {SanitizeAscii(r.CategoryNameEn)} | Sessions:{r.CompletedSessionsCount}/{r.SessionsCount} | Billed:{F(r.BilledTotal)}");
        }

        return BuildSimplePdfDocument(lines);
    }

    public static byte[] GenerateMethodTotalsPdf(string clinicName, DateOnly fromDate, DateOnly toDate, IReadOnlyList<MethodTotalReportRowResult> rows)
    {
        var lines = new List<string>
        {
            $"Clinic: {SanitizeAscii(clinicName)}",
            $"REPORT: PAYMENT METHODS ({fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd})"
        };

        foreach (var r in rows)
        {
            lines.Add($"{r.Method} | In:{F(r.PaymentsIn)} | Refund:{F(r.RefundsOut)} | Exp:{F(r.ExpensesOut)} | SupPay:{F(r.SupplierPaymentsOut)} | Net:{F(r.NetFlow)}");
        }

        return BuildSimplePdfDocument(lines);
    }

    private static string F(decimal amount) => amount.ToString("F2", CultureInfo.InvariantCulture);

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

        return sb.Length == 0 ? "-" : sb.ToString();
    }

    private static byte[] BuildSimplePdfDocument(List<string> lines)
    {
        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 10 Tf");
        content.AppendLine("40 770 Td");
        for (int i = 0; i < lines.Count; i++)
        {
            if (i > 0)
            {
                content.AppendLine("0 -16 Td");
            }

            content.Append('(').Append(lines[i]).AppendLine(") Tj");
        }

        content.AppendLine("ET");

        string contentStream = content.ToString();
        byte[] contentBytes = Encoding.ASCII.GetBytes(contentStream);

        var pdf = new StringBuilder();
        pdf.AppendLine("%PDF-1.4");
        var offsets = new List<int>();

        void AddObj(int id, string body)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.AppendLine(CultureInfo.InvariantCulture, $"{id} 0 obj");
            pdf.AppendLine(body);
            pdf.AppendLine("endobj");
        }

        AddObj(1, "<< /Type /Catalog /Pages 2 0 R >>");
        AddObj(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        AddObj(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>");
        AddObj(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        AddObj(5, string.Create(CultureInfo.InvariantCulture, $"<< /Length {contentBytes.Length} >>\nstream\n{contentStream}endstream"));

        int xrefPos = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.AppendLine("xref");
        pdf.AppendLine(CultureInfo.InvariantCulture, $"0 {offsets.Count + 1}");
        pdf.AppendLine("0000000000 65535 f ");
        foreach (int off in offsets)
        {
            pdf.AppendLine(CultureInfo.InvariantCulture, $"{off:D10} 00000 n ");
        }

        pdf.AppendLine("trailer");
        pdf.AppendLine(CultureInfo.InvariantCulture, $"<< /Size {offsets.Count + 1} /Root 1 0 R >>");
        pdf.AppendLine("startxref");
        pdf.AppendLine(xrefPos.ToString(CultureInfo.InvariantCulture));
        pdf.AppendLine("%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}

