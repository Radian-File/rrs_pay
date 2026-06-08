using System.Collections.ObjectModel;
using rrs_pay.ViewModels;

namespace rrs_pay.ViewModels.Pages;

public sealed class SettingsViewModel : ViewModelBase
{
    private string _companyName = "RRS Pay Demo Company";
    private string _payrollCutoffDay = "25";
    private string _defaultCurrency = "IDR";
    private bool _requireApprovalBeforePayslip = true;
    private bool _autoCalculateLateDeductions = true;

    public SettingsViewModel()
    {
        PayrollRules = new ObservableCollection<PayrollRule>
        {
            new("Overtime multiplier", "1.5x regular hourly rate"),
            new("Late grace period", "10 minutes per shift"),
            new("Payslip distribution", "Manual approval before release")
        };
    }

    public string CompanyName { get => _companyName; set => SetProperty(ref _companyName, value); }
    public string PayrollCutoffDay { get => _payrollCutoffDay; set => SetProperty(ref _payrollCutoffDay, value); }
    public string DefaultCurrency { get => _defaultCurrency; set => SetProperty(ref _defaultCurrency, value); }
    public bool RequireApprovalBeforePayslip { get => _requireApprovalBeforePayslip; set => SetProperty(ref _requireApprovalBeforePayslip, value); }
    public bool AutoCalculateLateDeductions { get => _autoCalculateLateDeductions; set => SetProperty(ref _autoCalculateLateDeductions, value); }
    public ObservableCollection<PayrollRule> PayrollRules { get; }
}

public sealed record PayrollRule(string Name, string Value);
