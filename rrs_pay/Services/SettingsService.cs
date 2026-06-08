using rrs_pay.Models;

namespace rrs_pay.Services;

public sealed class SettingsService
{
    public const string CompanyNameKey = "CompanyName";
    public const string PayrollCutoffDayKey = "PayrollCutoffDay";
    public const string CurrencyKey = "Currency";
    public const string RequireApprovalBeforePayslipKey = "RequireApprovalBeforePayslip";
    public const string AutoCalculateLateDeductionsKey = "AutoCalculateLateDeductions";
    public const string OvertimeMultiplierKey = "OvertimeMultiplier";
    public const string DefaultTaxRateKey = "DefaultTaxRate";
    public const string StandardWorkingHoursPerDayKey = "StandardWorkingHoursPerDay";
    public const string StandardWorkingDaysPerMonthKey = "StandardWorkingDaysPerMonth";
    public const string PayrollNumberPrefixKey = "PayrollNumberPrefix";

    private readonly DataService _dataService;
    private readonly AuditLogService _auditLogService;

    public SettingsService()
        : this(new DataService(), new AuditLogService())
    {
    }

    public SettingsService(DataService dataService, AuditLogService auditLogService)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
    }

    public async Task<AppSettingsSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dataService.GetPayrollSettingsAsync(activeOnly: false, cancellationToken);
        var values = settings.ToDictionary(setting => setting.Key, setting => setting.Value, StringComparer.OrdinalIgnoreCase);

        return new AppSettingsSnapshot(
            GetValue(values, CompanyNameKey, "RRS Pay Demo Company"),
            GetValue(values, PayrollCutoffDayKey, "25"),
            GetValue(values, CurrencyKey, "IDR"),
            GetBool(values, RequireApprovalBeforePayslipKey, defaultValue: true),
            GetBool(values, AutoCalculateLateDeductionsKey, defaultValue: true),
            new[]
            {
                new PayrollRuleSnapshot("Overtime multiplier", $"{GetValue(values, OvertimeMultiplierKey, "1.5")}x regular hourly rate"),
                new PayrollRuleSnapshot("Default tax rate", FormatPercent(GetValue(values, DefaultTaxRateKey, "0.10"))),
                new PayrollRuleSnapshot("Standard work day", $"{GetValue(values, StandardWorkingHoursPerDayKey, "8")} hours"),
                new PayrollRuleSnapshot("Working days / month", GetValue(values, StandardWorkingDaysPerMonthKey, "22")),
                new PayrollRuleSnapshot("Payroll number prefix", GetValue(values, PayrollNumberPrefixKey, "PAY"))
            });
    }

    public async Task SaveAsync(AppSettingsSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(snapshot.CompanyName))
        {
            throw new InvalidOperationException("Company name is required.");
        }

        if (!int.TryParse(snapshot.PayrollCutoffDay, out var cutoffDay) || cutoffDay < 1 || cutoffDay > 31)
        {
            throw new InvalidOperationException("Payroll cutoff day must be a number from 1 to 31.");
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [CompanyNameKey] = snapshot.CompanyName.Trim(),
            [PayrollCutoffDayKey] = cutoffDay.ToString(),
            [CurrencyKey] = snapshot.DefaultCurrency.Trim().ToUpperInvariant(),
            [RequireApprovalBeforePayslipKey] = snapshot.RequireApprovalBeforePayslip.ToString(),
            [AutoCalculateLateDeductionsKey] = snapshot.AutoCalculateLateDeductions.ToString()
        };

        await _dataService.SavePayrollSettingsAsync(values, cancellationToken);
        await _auditLogService.LogAsync(
            "Update Settings",
            nameof(PayrollSetting),
            "CompanyPayrollSettings",
            $"Company/payroll settings updated for '{snapshot.CompanyName.Trim()}'.",
            cancellationToken: cancellationToken);
    }

    private static string GetValue(Dictionary<string, string> values, string key, string fallback)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
    }

    private static bool GetBool(Dictionary<string, string> values, string key, bool defaultValue)
    {
        return values.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static string FormatPercent(string value)
    {
        return decimal.TryParse(value, out var parsed) ? $"{parsed:P0}" : value;
    }
}

public sealed record AppSettingsSnapshot(
    string CompanyName,
    string PayrollCutoffDay,
    string DefaultCurrency,
    bool RequireApprovalBeforePayslip,
    bool AutoCalculateLateDeductions,
    IReadOnlyCollection<PayrollRuleSnapshot> PayrollRules);

public sealed record PayrollRuleSnapshot(string Name, string Value);
