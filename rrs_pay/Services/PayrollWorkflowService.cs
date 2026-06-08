using Microsoft.EntityFrameworkCore;
using rrs_pay.Data;
using rrs_pay.Models;

namespace rrs_pay.Services;

public class PayrollWorkflowService
{
    private readonly Func<AppDbContext> _contextFactory;
    private readonly PayrollCalculationService _calculationService;
    private readonly AuditLogService _auditLogService;
    private readonly SessionService? _sessionService;

    public PayrollWorkflowService()
        : this(AppDbContext.CreateDefault, new PayrollCalculationService(), new AuditLogService(), null)
    {
    }

    public PayrollWorkflowService(SessionService sessionService)
        : this(AppDbContext.CreateDefault, new PayrollCalculationService(), new AuditLogService(sessionService), sessionService)
    {
    }

    public PayrollWorkflowService(
        Func<AppDbContext> contextFactory,
        PayrollCalculationService? calculationService = null,
        AuditLogService? auditLogService = null,
        SessionService? sessionService = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _calculationService = calculationService ?? new PayrollCalculationService();
        _auditLogService = auditLogService ?? new AuditLogService(contextFactory, sessionService);
        _sessionService = sessionService;
    }

    public async Task<Payroll> GenerateMonthlyPayrollAsync(DateTime month, CancellationToken cancellationToken = default)
    {
        var periodStart = new DateTime(month.Year, month.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var payDate = periodEnd;

        await using var context = _contextFactory();
        var existing = await context.Payrolls
            .Include(payroll => payroll.Details)
            .FirstOrDefaultAsync(payroll => payroll.PeriodStart == periodStart && payroll.PeriodEnd == periodEnd, cancellationToken);

        if (existing is not null && existing.Status is not PayrollStatus.Draft and not PayrollStatus.Calculated)
        {
            throw new InvalidOperationException($"Payroll for {periodStart:MMMM yyyy} already exists with status {existing.Status} and cannot be regenerated.");
        }

        var settings = await context.PayrollSettings
            .AsNoTracking()
            .Where(setting => setting.IsActive)
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);

        var employees = await context.Employees
            .AsNoTracking()
            .Include(employee => employee.Department)
            .Include(employee => employee.Position)
            .Where(employee => employee.IsActive && employee.Status != EmploymentStatus.Terminated && employee.Status != EmploymentStatus.Suspended)
            .OrderBy(employee => employee.EmployeeNumber)
            .ToListAsync(cancellationToken);

        if (employees.Count == 0)
        {
            throw new InvalidOperationException("No active employees were found for payroll generation.");
        }

        var overtimeByEmployee = await context.Attendances
            .AsNoTracking()
            .Where(attendance => attendance.Date >= periodStart && attendance.Date <= periodEnd)
            .GroupBy(attendance => attendance.EmployeeId)
            .Select(group => new { EmployeeId = group.Key, OvertimeHours = group.Sum(attendance => attendance.OvertimeHours) })
            .ToDictionaryAsync(item => item.EmployeeId, item => item.OvertimeHours, cancellationToken);

        var overtimeMultiplier = GetDecimalSetting(settings, "OvertimeMultiplier", 1.5m);
        var workingDays = GetDecimalSetting(settings, "StandardWorkingDaysPerMonth", 22m);
        var workingHoursPerDay = GetDecimalSetting(settings, "StandardWorkingHoursPerDay", 8m);
        var fallbackTaxRate = GetDecimalSetting(settings, "DefaultTaxRate", 0.10m);

        var payroll = existing ?? new Payroll
        {
            PayrollNumber = await GeneratePayrollNumberAsync(context, periodStart, settings, cancellationToken),
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            PayDate = payDate,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = _sessionService?.CurrentUser?.Id
        };

        if (existing is not null)
        {
            context.PayrollDetails.RemoveRange(existing.Details);
            payroll.Details.Clear();
            payroll.ApprovedAt = null;
            payroll.ApprovedByUserId = null;
        }
        else
        {
            context.Payrolls.Add(payroll);
        }

        payroll.Status = PayrollStatus.Calculated;
        payroll.Notes = $"Generated from active employees and default payroll settings on {DateTime.Now:MMM dd, yyyy h:mm tt}.";
        payroll.PayDate = payDate;

        var detailResults = new List<PayrollCalculationResult>();
        foreach (var employee in employees)
        {
            var overtimeHours = overtimeByEmployee.GetValueOrDefault(employee.Id);
            var taxRate = employee.DefaultTaxRate > 0m ? employee.DefaultTaxRate : fallbackTaxRate;
            var calculation = _calculationService.CalculateFromOvertimeHours(
                employee,
                overtimeHours,
                overtimeMultiplier,
                workingDays,
                workingHoursPerDay,
                employee.DefaultAllowance,
                tax: null);

            var resolvedTax = Math.Round(calculation.GrossPay * taxRate, 2, MidpointRounding.AwayFromZero);
            calculation = calculation with
            {
                Tax = resolvedTax,
                NetPay = calculation.GrossPay - calculation.Deduction - resolvedTax
            };

            detailResults.Add(calculation);
            payroll.Details.Add(new PayrollDetail
            {
                EmployeeId = employee.Id,
                BasicSalary = calculation.BasicSalary,
                Allowance = calculation.Allowance,
                Bonus = calculation.Bonus,
                OvertimePay = calculation.OvertimePay,
                Deduction = calculation.Deduction,
                Tax = calculation.Tax,
                GrossPay = calculation.GrossPay,
                NetPay = calculation.NetPay,
                Notes = overtimeHours > 0m ? $"Includes {overtimeHours:N2} overtime hours." : null
            });
        }

        var totals = _calculationService.CalculateTotals(detailResults);
        payroll.GrossTotal = totals.GrossTotal;
        payroll.DeductionTotal = totals.DeductionTotal;
        payroll.TaxTotal = totals.TaxTotal;
        payroll.NetTotal = totals.NetTotal;

        await context.SaveChangesAsync(cancellationToken);
        await _auditLogService.LogAsync(
            "Payroll Generated",
            nameof(Payroll),
            payroll.Id.ToString(),
            $"{payroll.PayrollNumber} for {periodStart:MMMM yyyy}; {payroll.Details.Count} employees; net {payroll.NetTotal:N2}.",
            cancellationToken: cancellationToken);

        return await LoadPayrollAsync(payroll.Id, cancellationToken)
            ?? throw new InvalidOperationException("Generated payroll could not be reloaded.");
    }

    public async Task<Payroll> ReviewPayrollAsync(int payrollId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var payroll = await context.Payrolls.Include(item => item.Details).FirstOrDefaultAsync(item => item.Id == payrollId, cancellationToken)
            ?? throw new InvalidOperationException($"Payroll with id {payrollId} was not found.");

        if (payroll.Status is PayrollStatus.Approved or PayrollStatus.Paid)
        {
            throw new InvalidOperationException($"Payroll {payroll.PayrollNumber} is {payroll.Status} and cannot be reviewed again.");
        }

        if (payroll.Details.Count == 0)
        {
            throw new InvalidOperationException("Payroll has no employee details to review.");
        }

        payroll.Status = PayrollStatus.Reviewed;
        payroll.Notes = AppendWorkflowNote(payroll.Notes, $"Reviewed on {DateTime.Now:MMM dd, yyyy h:mm tt}.");
        await context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "Payroll Reviewed",
            nameof(Payroll),
            payroll.Id.ToString(),
            $"{payroll.PayrollNumber} reviewed for {payroll.PeriodStart:MMMM yyyy}.",
            cancellationToken: cancellationToken);

        return await LoadPayrollAsync(payroll.Id, cancellationToken)
            ?? throw new InvalidOperationException("Reviewed payroll could not be reloaded.");
    }

    public async Task<Payroll> ApprovePayrollAsync(int payrollId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var payroll = await context.Payrolls.Include(item => item.Details).FirstOrDefaultAsync(item => item.Id == payrollId, cancellationToken)
            ?? throw new InvalidOperationException($"Payroll with id {payrollId} was not found.");

        if (payroll.Status == PayrollStatus.Approved)
        {
            return await LoadPayrollAsync(payroll.Id, cancellationToken) ?? payroll;
        }

        if (payroll.Status == PayrollStatus.Paid)
        {
            throw new InvalidOperationException($"Payroll {payroll.PayrollNumber} is already paid and locked.");
        }

        if (payroll.Status != PayrollStatus.Reviewed)
        {
            throw new InvalidOperationException("Payroll must be reviewed before approval.");
        }

        payroll.Status = PayrollStatus.Approved;
        payroll.ApprovedAt = DateTime.UtcNow;
        payroll.ApprovedByUserId = _sessionService?.CurrentUser?.Id;
        payroll.Notes = AppendWorkflowNote(payroll.Notes, $"Approved on {DateTime.Now:MMM dd, yyyy h:mm tt}.");
        await context.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogAsync(
            "Payroll Approved",
            nameof(Payroll),
            payroll.Id.ToString(),
            $"{payroll.PayrollNumber} approved for {payroll.PeriodStart:MMMM yyyy}; net {payroll.NetTotal:N2}.",
            cancellationToken: cancellationToken);

        return await LoadPayrollAsync(payroll.Id, cancellationToken)
            ?? throw new InvalidOperationException("Approved payroll could not be reloaded.");
    }

    public async Task<Payroll?> LoadPayrollAsync(int payrollId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        return await context.Payrolls
            .AsNoTracking()
            .Include(payroll => payroll.CreatedByUser)
            .Include(payroll => payroll.ApprovedByUser)
            .Include(payroll => payroll.Details)
            .ThenInclude(detail => detail.Employee)
            .ThenInclude(employee => employee.Department)
            .Include(payroll => payroll.Details)
            .ThenInclude(detail => detail.Employee)
            .ThenInclude(employee => employee.Position)
            .FirstOrDefaultAsync(payroll => payroll.Id == payrollId, cancellationToken);
    }

    private static decimal GetDecimalSetting(IReadOnlyDictionary<string, string> settings, string key, decimal fallback)
    {
        return settings.TryGetValue(key, out var value) && decimal.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }

    private static async Task<string> GeneratePayrollNumberAsync(
        AppDbContext context,
        DateTime periodStart,
        IReadOnlyDictionary<string, string> settings,
        CancellationToken cancellationToken)
    {
        var prefix = settings.TryGetValue("PayrollNumberPrefix", out var configuredPrefix) && !string.IsNullOrWhiteSpace(configuredPrefix)
            ? configuredPrefix.Trim().ToUpperInvariant()
            : "PAY";

        var stem = $"{prefix}-{periodStart:yyyyMM}";
        var existingCount = await context.Payrolls.CountAsync(payroll => payroll.PayrollNumber.StartsWith(stem), cancellationToken);
        return $"{stem}-{existingCount + 1:000}";
    }

    private static string AppendWorkflowNote(string? existingNotes, string note)
    {
        return string.IsNullOrWhiteSpace(existingNotes)
            ? note
            : $"{existingNotes.Trim()} {note}";
    }
}
