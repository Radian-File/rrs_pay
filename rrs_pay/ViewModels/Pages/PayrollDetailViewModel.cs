using System.Collections.ObjectModel;
using System.Windows.Input;
using rrs_pay.Models;
using rrs_pay.Services;
using rrs_pay.ViewModels;
using rrs_pay.ViewModels.Infrastructure;

namespace rrs_pay.ViewModels.Pages;

public sealed class PayrollDetailViewModel : ViewModelBase
{
    private readonly DataService _dataService;
    private readonly PayslipPdfService _payslipPdfService;
    private readonly AsyncRelayCommand _exportPayslipCommand;
    private PayrollDetail? _currentDetail;

    private string _payrollNo = "No payroll loaded";
    private string _period = "-";
    private string _employeeCode = "-";
    private string _employeeName = "-";
    private string _department = "-";
    private string _position = "-";
    private string _bankAccount = "-";
    private string _notes = "Generate payroll, then open payroll detail to preview the latest employee payslip.";
    private string _statusMessage = "Loading latest payroll detail.";
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private decimal _grossPay;
    private decimal _totalDeductions;
    private decimal _netSalary;

    public PayrollDetailViewModel()
    {
        _dataService = new DataService();
        _payslipPdfService = new PayslipPdfService();
        Earnings = new ObservableCollection<PayslipLineItem>();
        Deductions = new ObservableCollection<PayslipLineItem>();
        _exportPayslipCommand = new AsyncRelayCommand(ExportPayslipAsync, () => !IsBusy && _currentDetail is not null);
        ExportPayslipCommand = _exportPayslipCommand;

        _ = LoadLatestDetailAsync();
    }

    public string PayrollNo { get => _payrollNo; private set => SetProperty(ref _payrollNo, value); }
    public string Period { get => _period; private set => SetProperty(ref _period, value); }
    public string EmployeeCode { get => _employeeCode; private set => SetProperty(ref _employeeCode, value); }
    public string EmployeeName { get => _employeeName; private set => SetProperty(ref _employeeName, value); }
    public string Department { get => _department; private set => SetProperty(ref _department, value); }
    public string Position { get => _position; private set => SetProperty(ref _position, value); }
    public string BankAccount { get => _bankAccount; private set => SetProperty(ref _bankAccount, value); }
    public ObservableCollection<PayslipLineItem> Earnings { get; }
    public ObservableCollection<PayslipLineItem> Deductions { get; }
    public decimal GrossPay { get => _grossPay; private set => SetProperty(ref _grossPay, value); }
    public decimal TotalDeductions { get => _totalDeductions; private set => SetProperty(ref _totalDeductions, value); }
    public decimal NetSalary { get => _netSalary; private set => SetProperty(ref _netSalary, value); }
    public string Notes { get => _notes; private set => SetProperty(ref _notes, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public ICommand ExportPayslipCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _exportPayslipCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private async Task LoadLatestDetailAsync()
    {
        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            var payroll = (await _dataService.GetPayrollsAsync()).FirstOrDefault(item => item.Details.Count > 0);
            var detail = payroll?.Details.OrderBy(item => item.Employee.EmployeeNumber).FirstOrDefault();
            _currentDetail = detail;

            if (payroll is null || detail is null)
            {
                StatusMessage = "No generated payroll details are available yet.";
                return;
            }

            PayrollNo = payroll.PayrollNumber;
            Period = $"{payroll.PeriodStart:MMM dd, yyyy} - {payroll.PeriodEnd:MMM dd, yyyy}";
            EmployeeCode = detail.Employee.EmployeeNumber;
            EmployeeName = detail.Employee.FullName;
            Department = detail.Employee.Department?.Name ?? "-";
            Position = detail.Employee.Position?.Title ?? "-";
            BankAccount = $"{detail.Employee.BankName ?? "Bank"} {MaskAccount(detail.Employee.BankAccountNumber)}".Trim();
            Notes = detail.Notes ?? payroll.Notes ?? "Payroll detail loaded from generated payroll data.";

            Earnings.Clear();
            Earnings.Add(new PayslipLineItem("Basic salary", detail.BasicSalary));
            Earnings.Add(new PayslipLineItem("Allowance", detail.Allowance));
            Earnings.Add(new PayslipLineItem("Bonus", detail.Bonus));
            Earnings.Add(new PayslipLineItem("Overtime pay", detail.OvertimePay));

            Deductions.Clear();
            Deductions.Add(new PayslipLineItem("Other deductions", detail.Deduction));
            Deductions.Add(new PayslipLineItem("Income tax", detail.Tax));

            GrossPay = detail.GrossPay;
            TotalDeductions = detail.Deduction + detail.Tax;
            NetSalary = detail.NetPay;
            StatusMessage = $"Loaded payslip preview for {EmployeeCode} from {PayrollNo}.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load payroll detail: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportPayslipAsync()
    {
        if (_currentDetail is null)
        {
            return;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            var path = await _payslipPdfService.GeneratePayslipAsync(_currentDetail.Id);
            StatusMessage = $"Payslip exported to {path}.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not export payslip: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

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
}

public sealed record PayslipLineItem(string Description, decimal Amount);
