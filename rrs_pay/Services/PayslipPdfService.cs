using System.IO;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using rrs_pay.Data;
using rrs_pay.Models;

namespace rrs_pay.Services;

public class PayslipPdfService
{
    private readonly Func<AppDbContext> _contextFactory;
    private readonly AuditLogService _auditLogService;

    static PayslipPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PayslipPdfService() : this(AppDbContext.CreateDefault, new AuditLogService())
    {
    }

    public PayslipPdfService(SessionService sessionService) : this(AppDbContext.CreateDefault, new AuditLogService(sessionService))
    {
    }

    public PayslipPdfService(Func<AppDbContext> contextFactory, AuditLogService? auditLogService = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _auditLogService = auditLogService ?? new AuditLogService(contextFactory);
    }

    public static string DefaultPayslipExportFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "RRS Pay",
        "Exports",
        "Payslips");

    public async Task<string> GeneratePayslipAsync(int payrollDetailId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var detail = await LoadDetailQuery(context)
            .FirstOrDefaultAsync(item => item.Id == payrollDetailId, cancellationToken)
            ?? throw new InvalidOperationException($"Payroll detail with id {payrollDetailId} was not found.");

        var path = GeneratePayslip(detail);
        await _auditLogService.LogAsync(
            "Payslip Exported",
            nameof(PayrollDetail),
            detail.Id.ToString(),
            $"Payslip for {detail.Employee.EmployeeNumber} ({detail.Payroll.PayrollNumber}) exported to {path}.",
            cancellationToken: cancellationToken);

        return path;
    }

    public async Task<IReadOnlyList<string>> GeneratePayslipsAsync(int payrollId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var details = await LoadDetailQuery(context)
            .Where(item => item.PayrollId == payrollId)
            .OrderBy(item => item.Employee.EmployeeNumber)
            .ToListAsync(cancellationToken);

        if (details.Count == 0)
        {
            throw new InvalidOperationException("No payroll details were found for payslip export.");
        }

        var paths = details.Select(GeneratePayslip).ToList();
        var payroll = details[0].Payroll;
        await _auditLogService.LogAsync(
            "Payslip Batch Exported",
            nameof(Payroll),
            payroll.Id.ToString(),
            $"{paths.Count} payslips for {payroll.PayrollNumber} exported to {DefaultPayslipExportFolder}.",
            cancellationToken: cancellationToken);

        return paths;
    }

    private static IQueryable<PayrollDetail> LoadDetailQuery(AppDbContext context)
    {
        return context.PayrollDetails
            .AsNoTracking()
            .Include(detail => detail.Payroll)
            .Include(detail => detail.Employee)
            .ThenInclude(employee => employee.Department)
            .Include(detail => detail.Employee)
            .ThenInclude(employee => employee.Position);
    }

    private static string GeneratePayslip(PayrollDetail detail)
    {
        Directory.CreateDirectory(DefaultPayslipExportFolder);

        var payroll = detail.Payroll;
        var employee = detail.Employee;
        var fileName = $"Payslip_{SanitizeFileName(payroll.PayrollNumber)}_{SanitizeFileName(employee.EmployeeNumber)}_{payroll.PeriodStart:yyyyMM}.pdf";
        var path = Path.Combine(DefaultPayslipExportFolder, fileName);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(text => text.FontSize(10).FontFamily("Arial"));

                page.Header().Column(column =>
                {
                    column.Item().Text("RRS Pay").FontSize(22).Bold().FontColor(Colors.Blue.Darken2);
                    column.Item().Text("Official Employee Payslip").FontSize(12).FontColor(Colors.Grey.Darken2);
                    column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(18).Column(column =>
                {
                    column.Spacing(14);
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text("Employee Information").Bold().FontSize(12);
                            left.Item().Text($"Name: {employee.FullName}");
                            left.Item().Text($"Employee No: {employee.EmployeeNumber}");
                            left.Item().Text($"Department: {employee.Department?.Name ?? "-"}");
                            left.Item().Text($"Position: {employee.Position?.Title ?? "-"}");
                            left.Item().Text($"Bank: {employee.BankName ?? "-"} {MaskAccount(employee.BankAccountNumber)}");
                        });

                        row.RelativeItem().Column(right =>
                        {
                            right.Item().Text("Payroll Information").Bold().FontSize(12);
                            right.Item().Text($"Payroll No: {payroll.PayrollNumber}");
                            right.Item().Text($"Period: {payroll.PeriodStart:MMM dd, yyyy} - {payroll.PeriodEnd:MMM dd, yyyy}");
                            right.Item().Text($"Pay Date: {payroll.PayDate:MMM dd, yyyy}");
                            right.Item().Text($"Status: {payroll.Status}");
                        });
                    });

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Earnings");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Amount");
                        });

                        AddMoneyRow(table, "Basic Salary", detail.BasicSalary);
                        AddMoneyRow(table, "Allowance", detail.Allowance);
                        AddMoneyRow(table, "Bonus", detail.Bonus);
                        AddMoneyRow(table, "Overtime Pay", detail.OvertimePay);
                        AddMoneyRow(table, "Gross Pay", detail.GrossPay, true);
                    });

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Deductions");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Amount");
                        });

                        AddMoneyRow(table, "Other Deductions", detail.Deduction);
                        AddMoneyRow(table, "Tax", detail.Tax);
                        AddMoneyRow(table, "Total Deductions", detail.Deduction + detail.Tax, true);
                    });

                    column.Item().Background(Colors.Green.Lighten5).Padding(14).Row(row =>
                    {
                        row.RelativeItem().Text("Net Salary").FontSize(16).Bold();
                        row.RelativeItem().AlignRight().Text(FormatMoney(detail.NetPay)).FontSize(16).Bold().FontColor(Colors.Green.Darken3);
                    });

                    if (!string.IsNullOrWhiteSpace(detail.Notes))
                    {
                        column.Item().Text($"Notes: {detail.Notes}").FontColor(Colors.Grey.Darken2);
                    }
                });

                page.Footer().AlignCenter().Text($"Generated by RRS Pay on {DateTime.Now:MMM dd, yyyy h:mm tt}").FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf(path);

        return path;
    }

    private static IContainer HeaderCell(IContainer container)
    {
        return container.Background(Colors.Blue.Darken2).Padding(6).DefaultTextStyle(text => text.FontColor(Colors.White).Bold());
    }

    private static void AddMoneyRow(TableDescriptor table, string label, decimal amount, bool bold = false)
    {
        table.Cell().Element(cell => BodyCell(cell, bold)).Text(label);
        table.Cell().Element(cell => BodyCell(cell, bold)).AlignRight().Text(FormatMoney(amount));
    }

    private static IContainer BodyCell(IContainer container, bool bold)
    {
        var styled = container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(6);
        return bold ? styled.DefaultTextStyle(text => text.Bold()) : styled;
    }

    private static string FormatMoney(decimal amount) => amount.ToString("N2");

    private static string MaskAccount(string? account)
    {
        if (string.IsNullOrWhiteSpace(account))
        {
            return string.Empty;
        }

        var trimmed = account.Trim();
        var suffix = trimmed.Length <= 4 ? trimmed : trimmed[^4..];
        return $"•••• {suffix}";
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
