using System.IO;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using rrs_pay.Data;
using rrs_pay.Models;

namespace rrs_pay.Services;

public class ReportExportService
{
    private readonly Func<AppDbContext> _contextFactory;
    private readonly AuditLogService _auditLogService;

    static ReportExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ReportExportService() : this(AppDbContext.CreateDefault, new AuditLogService())
    {
    }

    public ReportExportService(SessionService sessionService) : this(AppDbContext.CreateDefault, new AuditLogService(sessionService))
    {
    }

    public ReportExportService(Func<AppDbContext> contextFactory, AuditLogService? auditLogService = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _auditLogService = auditLogService ?? new AuditLogService(contextFactory);
    }

    public static string DefaultReportExportFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "RRS Pay",
        "Exports",
        "Reports");

    public async Task<PayrollReportSnapshot> BuildPayrollSummaryAsync(DateTime month, CancellationToken cancellationToken = default)
    {
        var periodStart = new DateTime(month.Year, month.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var payroll = await LoadPayrollForPeriodAsync(periodStart, periodEnd, cancellationToken)
            ?? throw new InvalidOperationException($"No payroll was found for {periodStart:MMMM yyyy}. Generate payroll first.");

        var departmentRecaps = payroll.Details
            .GroupBy(detail => detail.Employee.Department?.Name ?? "Unassigned")
            .Select(group => new DepartmentPayrollRecap(
                Department: group.Key,
                EmployeeCount: group.Count(),
                GrossPay: group.Sum(detail => detail.GrossPay),
                Deductions: group.Sum(detail => detail.Deduction + detail.Tax),
                NetPay: group.Sum(detail => detail.NetPay)))
            .OrderBy(item => item.Department)
            .ToList();

        var rows = new List<PayrollReportRow>
        {
            new("Payroll Number", payroll.PayrollNumber, payroll.Status.ToString()),
            new("Payroll Period", $"{payroll.PeriodStart:MMM dd, yyyy} - {payroll.PeriodEnd:MMM dd, yyyy}", $"Pay date {payroll.PayDate:MMM dd, yyyy}"),
            new("Employees Paid", payroll.Details.Count.ToString("N0"), $"{departmentRecaps.Count:N0} department(s)"),
            new("Gross Payroll", payroll.GrossTotal.ToString("N2"), "Before deductions and tax"),
            new("Deductions + Tax", (payroll.DeductionTotal + payroll.TaxTotal).ToString("N2"), $"Tax {payroll.TaxTotal:N2}"),
            new("Net Payout", payroll.NetTotal.ToString("N2"), "Final payroll payable")
        };

        return new PayrollReportSnapshot(payroll, rows, departmentRecaps);
    }

    public async Task<string> ExportPayrollSummaryExcelAsync(DateTime month, CancellationToken cancellationToken = default)
    {
        var snapshot = await BuildPayrollSummaryAsync(month, cancellationToken);
        Directory.CreateDirectory(DefaultReportExportFolder);
        var path = Path.Combine(DefaultReportExportFolder, $"PayrollSummary_{SanitizeFileName(snapshot.Payroll.PayrollNumber)}_{snapshot.Payroll.PeriodStart:yyyyMM}.xlsx");

        using var workbook = new XLWorkbook();
        var summary = workbook.Worksheets.Add("Payroll Summary");
        summary.Cell(1, 1).Value = "RRS Pay Payroll Summary";
        summary.Cell(1, 1).Style.Font.Bold = true;
        summary.Cell(1, 1).Style.Font.FontSize = 16;
        summary.Range(1, 1, 1, 3).Merge();

        summary.Cell(3, 1).Value = "Payroll No";
        summary.Cell(3, 2).Value = snapshot.Payroll.PayrollNumber;
        summary.Cell(4, 1).Value = "Period";
        summary.Cell(4, 2).Value = $"{snapshot.Payroll.PeriodStart:MMM dd, yyyy} - {snapshot.Payroll.PeriodEnd:MMM dd, yyyy}";
        summary.Cell(5, 1).Value = "Status";
        summary.Cell(5, 2).Value = snapshot.Payroll.Status.ToString();

        var headerRow = 7;
        summary.Cell(headerRow, 1).Value = "Employee No";
        summary.Cell(headerRow, 2).Value = "Employee";
        summary.Cell(headerRow, 3).Value = "Department";
        summary.Cell(headerRow, 4).Value = "Position";
        summary.Cell(headerRow, 5).Value = "Basic";
        summary.Cell(headerRow, 6).Value = "Allowance";
        summary.Cell(headerRow, 7).Value = "Bonus";
        summary.Cell(headerRow, 8).Value = "Overtime";
        summary.Cell(headerRow, 9).Value = "Deductions";
        summary.Cell(headerRow, 10).Value = "Tax";
        summary.Cell(headerRow, 11).Value = "Gross";
        summary.Cell(headerRow, 12).Value = "Net";
        summary.Range(headerRow, 1, headerRow, 12).Style.Font.Bold = true;
        summary.Range(headerRow, 1, headerRow, 12).Style.Fill.BackgroundColor = XLColor.FromHtml("1D4ED8");
        summary.Range(headerRow, 1, headerRow, 12).Style.Font.FontColor = XLColor.White;

        var row = headerRow + 1;
        foreach (var detail in snapshot.Payroll.Details.OrderBy(detail => detail.Employee.EmployeeNumber))
        {
            summary.Cell(row, 1).Value = detail.Employee.EmployeeNumber;
            summary.Cell(row, 2).Value = detail.Employee.FullName;
            summary.Cell(row, 3).Value = detail.Employee.Department?.Name ?? "-";
            summary.Cell(row, 4).Value = detail.Employee.Position?.Title ?? "-";
            summary.Cell(row, 5).Value = detail.BasicSalary;
            summary.Cell(row, 6).Value = detail.Allowance;
            summary.Cell(row, 7).Value = detail.Bonus;
            summary.Cell(row, 8).Value = detail.OvertimePay;
            summary.Cell(row, 9).Value = detail.Deduction;
            summary.Cell(row, 10).Value = detail.Tax;
            summary.Cell(row, 11).Value = detail.GrossPay;
            summary.Cell(row, 12).Value = detail.NetPay;
            row++;
        }

        var totalRow = row;
        summary.Cell(totalRow, 4).Value = "Totals";
        summary.Cell(totalRow, 5).FormulaA1 = $"SUM(E{headerRow + 1}:E{row - 1})";
        summary.Cell(totalRow, 6).FormulaA1 = $"SUM(F{headerRow + 1}:F{row - 1})";
        summary.Cell(totalRow, 7).FormulaA1 = $"SUM(G{headerRow + 1}:G{row - 1})";
        summary.Cell(totalRow, 8).FormulaA1 = $"SUM(H{headerRow + 1}:H{row - 1})";
        summary.Cell(totalRow, 9).FormulaA1 = $"SUM(I{headerRow + 1}:I{row - 1})";
        summary.Cell(totalRow, 10).FormulaA1 = $"SUM(J{headerRow + 1}:J{row - 1})";
        summary.Cell(totalRow, 11).FormulaA1 = $"SUM(K{headerRow + 1}:K{row - 1})";
        summary.Cell(totalRow, 12).FormulaA1 = $"SUM(L{headerRow + 1}:L{row - 1})";
        summary.Range(totalRow, 4, totalRow, 12).Style.Font.Bold = true;
        summary.Range(headerRow + 1, 5, totalRow, 12).Style.NumberFormat.Format = "#,##0.00";
        summary.Columns().AdjustToContents();

        var departments = workbook.Worksheets.Add("Department Recap");
        departments.Cell(1, 1).Value = "Department";
        departments.Cell(1, 2).Value = "Employees";
        departments.Cell(1, 3).Value = "Gross";
        departments.Cell(1, 4).Value = "Deductions";
        departments.Cell(1, 5).Value = "Net";
        departments.Range(1, 1, 1, 5).Style.Font.Bold = true;
        var deptRow = 2;
        foreach (var recap in snapshot.DepartmentRecaps)
        {
            departments.Cell(deptRow, 1).Value = recap.Department;
            departments.Cell(deptRow, 2).Value = recap.EmployeeCount;
            departments.Cell(deptRow, 3).Value = recap.GrossPay;
            departments.Cell(deptRow, 4).Value = recap.Deductions;
            departments.Cell(deptRow, 5).Value = recap.NetPay;
            deptRow++;
        }

        departments.Range(2, 3, Math.Max(2, deptRow - 1), 5).Style.NumberFormat.Format = "#,##0.00";
        departments.Columns().AdjustToContents();

        workbook.SaveAs(path);
        await _auditLogService.LogAsync(
            "Payroll Report Exported",
            nameof(Payroll),
            snapshot.Payroll.Id.ToString(),
            $"Excel payroll summary exported to {path}.",
            cancellationToken: cancellationToken);

        return path;
    }

    public async Task<string> ExportPayrollSummaryPdfAsync(DateTime month, CancellationToken cancellationToken = default)
    {
        var snapshot = await BuildPayrollSummaryAsync(month, cancellationToken);
        Directory.CreateDirectory(DefaultReportExportFolder);
        var path = Path.Combine(DefaultReportExportFolder, $"PayrollSummary_{SanitizeFileName(snapshot.Payroll.PayrollNumber)}_{snapshot.Payroll.PeriodStart:yyyyMM}.pdf");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(text => text.FontSize(10).FontFamily("Arial"));

                page.Header().Column(column =>
                {
                    column.Item().Text("RRS Pay Payroll Summary").FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                    column.Item().Text($"{snapshot.Payroll.PayrollNumber} • {snapshot.Payroll.PeriodStart:MMMM yyyy} • {snapshot.Payroll.Status}").FontColor(Colors.Grey.Darken2);
                    column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(18).Column(column =>
                {
                    column.Spacing(14);
                    column.Item().Row(row =>
                    {
                        AddKpi(row, "Employees", snapshot.Payroll.Details.Count.ToString("N0"));
                        AddKpi(row, "Gross", snapshot.Payroll.GrossTotal.ToString("N2"));
                        AddKpi(row, "Deductions", (snapshot.Payroll.DeductionTotal + snapshot.Payroll.TaxTotal).ToString("N2"));
                        AddKpi(row, "Net", snapshot.Payroll.NetTotal.ToString("N2"));
                    });

                    column.Item().Text("Department Recap").FontSize(13).Bold();
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Department");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Employees");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Gross");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Deductions");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Net");
                        });

                        foreach (var recap in snapshot.DepartmentRecaps)
                        {
                            table.Cell().Element(BodyCell).Text(recap.Department);
                            table.Cell().Element(BodyCell).AlignRight().Text(recap.EmployeeCount.ToString("N0"));
                            table.Cell().Element(BodyCell).AlignRight().Text(recap.GrossPay.ToString("N2"));
                            table.Cell().Element(BodyCell).AlignRight().Text(recap.Deductions.ToString("N2"));
                            table.Cell().Element(BodyCell).AlignRight().Text(recap.NetPay.ToString("N2"));
                        }
                    });

                    column.Item().Text("Payroll Details").FontSize(13).Bold();
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1.6f);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Emp No");
                            header.Cell().Element(HeaderCell).Text("Employee");
                            header.Cell().Element(HeaderCell).Text("Department");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Gross");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Net");
                        });

                        foreach (var detail in snapshot.Payroll.Details.OrderBy(detail => detail.Employee.EmployeeNumber))
                        {
                            table.Cell().Element(BodyCell).Text(detail.Employee.EmployeeNumber);
                            table.Cell().Element(BodyCell).Text(detail.Employee.FullName);
                            table.Cell().Element(BodyCell).Text(detail.Employee.Department?.Name ?? "-");
                            table.Cell().Element(BodyCell).AlignRight().Text(detail.GrossPay.ToString("N2"));
                            table.Cell().Element(BodyCell).AlignRight().Text(detail.NetPay.ToString("N2"));
                        }
                    });
                });

                page.Footer().AlignCenter().Text($"Generated by RRS Pay on {DateTime.Now:MMM dd, yyyy h:mm tt}").FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf(path);

        await _auditLogService.LogAsync(
            "Payroll Report Exported",
            nameof(Payroll),
            snapshot.Payroll.Id.ToString(),
            $"PDF payroll summary exported to {path}.",
            cancellationToken: cancellationToken);

        return path;
    }

    private async Task<Payroll?> LoadPayrollForPeriodAsync(DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken)
    {
        await using var context = _contextFactory();
        return await context.Payrolls
            .AsNoTracking()
            .Include(payroll => payroll.Details)
            .ThenInclude(detail => detail.Employee)
            .ThenInclude(employee => employee.Department)
            .Include(payroll => payroll.Details)
            .ThenInclude(detail => detail.Employee)
            .ThenInclude(employee => employee.Position)
            .Where(payroll => payroll.PeriodStart == periodStart && payroll.PeriodEnd == periodEnd)
            .OrderByDescending(payroll => payroll.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static void AddKpi(RowDescriptor row, string label, string value)
    {
        row.RelativeItem().PaddingRight(6).Background(Colors.Grey.Lighten4).Padding(10).Column(column =>
        {
            column.Item().Text(label).FontSize(9).FontColor(Colors.Grey.Darken2);
            column.Item().Text(value).FontSize(13).Bold();
        });
    }

    private static IContainer HeaderCell(IContainer container)
    {
        return container.Background(Colors.Blue.Darken2).Padding(5).DefaultTextStyle(text => text.FontColor(Colors.White).Bold().FontSize(9));
    }

    private static IContainer BodyCell(IContainer container)
    {
        return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).DefaultTextStyle(text => text.FontSize(9));
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }
}

public sealed record PayrollReportSnapshot(
    Payroll Payroll,
    IReadOnlyList<PayrollReportRow> Rows,
    IReadOnlyList<DepartmentPayrollRecap> DepartmentRecaps);

public sealed record PayrollReportRow(string Metric, string Value, string Notes);

public sealed record DepartmentPayrollRecap(
    string Department,
    int EmployeeCount,
    decimal GrossPay,
    decimal Deductions,
    decimal NetPay);
