using System.Globalization;
using ClosedXML.Excel;
using Core.Application;

namespace Core.Infrastructure;

/// <summary>
/// Generates formatted ClosedXML (.xlsx) spreadsheets matching Phase 14 screen reports.
/// </summary>
public static class ReportsExcelGenerator
{
    public static byte[] GenerateIncomeVsExpensesExcel(IncomeVsExpensesReportResult report)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Income vs Expenses");
        ws.Cell(1, 1).Value = "Metric";
        ws.Cell(1, 2).Value = "Amount";
        ws.Cell(1, 3).Value = "Currency";
        ws.Row(1).Style.Font.Bold = true;

        var rows = new (string Label, decimal Value)[]
        {
            ("GrossPayments", report.GrossPayments),
            ("Refunds", report.Refunds),
            ("NetIncome", report.NetIncome),
            ("OperatingExpenses", report.OperatingExpenses),
            ("SupplierPayments", report.SupplierPayments),
            ("SupplierAccruals", report.SupplierAccruals),
            ("FixedCostsTotal", report.FixedCostsTotal),
            ("PayrollTotal", report.PayrollTotal),
            ("TotalExpenses", report.TotalExpenses),
            ("NetProfit", report.NetProfit)
        };

        for (int i = 0; i < rows.Length; i++)
        {
            ws.Cell(i + 2, 1).Value = rows[i].Label;
            ws.Cell(i + 2, 2).Value = rows[i].Value;
            ws.Cell(i + 2, 3).Value = report.Currency;
        }

        return SaveToBytes(wb);
    }

    public static byte[] GenerateDoctorSummaryExcel(IReadOnlyList<DoctorFinancialReportRowResult> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Doctors");
        string[] headers =
        [
            "DoctorId", "DoctorName", "SpecialtyCode", "SessionsCount", "CompletedSessionsCount",
            "BilledTotal", "CollectedTotal", "RefundedTotal", "ToolsCost", "LabCost", "PayrollCost", "NetContribution"
        ];
        for (int col = 0; col < headers.Length; col++)
        {
            ws.Cell(1, col + 1).Value = headers[col];
        }

        ws.Row(1).Style.Font.Bold = true;

        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            int row = i + 2;
            ws.Cell(row, 1).Value = r.DoctorId.ToString();
            ws.Cell(row, 2).Value = r.DoctorName;
            ws.Cell(row, 3).Value = r.SpecialtyCode;
            ws.Cell(row, 4).Value = r.SessionsCount;
            ws.Cell(row, 5).Value = r.CompletedSessionsCount;
            ws.Cell(row, 6).Value = r.BilledTotal;
            ws.Cell(row, 7).Value = r.CollectedTotal;
            ws.Cell(row, 8).Value = r.RefundedTotal;
            ws.Cell(row, 9).Value = r.ToolsCost;
            ws.Cell(row, 10).Value = r.LabCost;
            ws.Cell(row, 11).Value = r.PayrollCost;
            ws.Cell(row, 12).Value = r.NetContribution;
        }

        return SaveToBytes(wb);
    }

    public static byte[] GenerateSessionsAndCategoriesExcel(IReadOnlyList<SessionCategoryReportRowResult> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sessions by Category");
        string[] headers = ["VisitDate", "CategoryEn", "CategoryAr", "SessionsCount", "CompletedSessionsCount", "BilledTotal"];
        for (int col = 0; col < headers.Length; col++)
        {
            ws.Cell(1, col + 1).Value = headers[col];
        }

        ws.Row(1).Style.Font.Bold = true;

        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            int row = i + 2;
            ws.Cell(row, 1).Value = r.VisitDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            ws.Cell(row, 2).Value = r.CategoryNameEn;
            ws.Cell(row, 3).Value = r.CategoryNameAr;
            ws.Cell(row, 4).Value = r.SessionsCount;
            ws.Cell(row, 5).Value = r.CompletedSessionsCount;
            ws.Cell(row, 6).Value = r.BilledTotal;
        }

        return SaveToBytes(wb);
    }

    public static byte[] GenerateMethodTotalsExcel(IReadOnlyList<MethodTotalReportRowResult> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Payment Methods");
        string[] headers = ["Method", "PaymentsIn", "RefundsOut", "ExpensesOut", "SupplierPaymentsOut", "NetFlow"];
        for (int col = 0; col < headers.Length; col++)
        {
            ws.Cell(1, col + 1).Value = headers[col];
        }

        ws.Row(1).Style.Font.Bold = true;

        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            int row = i + 2;
            ws.Cell(row, 1).Value = r.Method.ToString();
            ws.Cell(row, 2).Value = r.PaymentsIn;
            ws.Cell(row, 3).Value = r.RefundsOut;
            ws.Cell(row, 4).Value = r.ExpensesOut;
            ws.Cell(row, 5).Value = r.SupplierPaymentsOut;
            ws.Cell(row, 6).Value = r.NetFlow;
        }

        return SaveToBytes(wb);
    }

    private static byte[] SaveToBytes(XLWorkbook workbook)
    {
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}

