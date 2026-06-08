using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using rrs_pay.Models;
using rrs_pay.Services;
using rrs_pay.ViewModels;
using rrs_pay.ViewModels.Infrastructure;

namespace rrs_pay.ViewModels.Pages;

public sealed class PayrollViewModel : ViewModelBase
{
    private readonly DataService _dataService;
    private readonly PayrollWorkflowService _workflowService;
    private readonly PayslipPdfService _payslipPdfService;
    private readonly AsyncRelayCommand _generatePayrollCommand;
    private readonly AsyncRelayCommand _reviewPayrollCommand;
    private readonly AsyncRelayCommand _approvePayrollCommand;
    private readonly AsyncRelayCommand _exportPayslipsCommand;

    private Payroll? _currentPayroll;
    private PayrollItem? _selectedPayrollDetail;
    private string _selectedPeriod;
    private string _statusMessage = "Load or generate a payroll period to begin review.";
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private decimal _totalGross;
    private decimal _totalDeductions;
    private decimal _totalNet;

    public PayrollViewModel()
    {
        _dataService = new DataService();
        _workflowService = new PayrollWorkflowService();
        _payslipPdfService = new PayslipPdfService();

        Periods = new ObservableCollection<string>(BuildPeriodOptions());
        _selectedPeriod = Periods.FirstOrDefault() ?? DateTime.Now.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        PayrollItems = new ObservableCollection<PayrollItem>();

        _generatePayrollCommand = new AsyncRelayCommand(GeneratePayrollAsync, () => !IsBusy);
        _reviewPayrollCommand = new AsyncRelayCommand(ReviewPayrollAsync, CanReviewPayroll);
        _approvePayrollCommand = new AsyncRelayCommand(ApprovePayrollAsync, CanApprovePayroll);
        _exportPayslipsCommand = new AsyncRelayCommand(ExportPayslipsAsync, CanExportPayslips);

        GeneratePayrollCommand = _generatePayrollCommand;
        ReviewPayrollCommand = _reviewPayrollCommand;
        ApprovePayrollCommand = _approvePayrollCommand;
        ExportPayslipsCommand = _exportPayslipsCommand;

        _ = LoadPayrollForSelectedPeriodAsync();
    }

    public ObservableCollection<string> Periods { get; }
    public ObservableCollection<PayrollItem> PayrollItems { get; }
    public ICommand GeneratePayrollCommand { get; }
    public ICommand ReviewPayrollCommand { get; }
    public ICommand ApprovePayrollCommand { get; }
    public ICommand ExportPayslipsCommand { get; }
    public ICommand PrepareNewRunCommand => GeneratePayrollCommand;

    public string SelectedPeriod
    {
        get => _selectedPeriod;
        set
        {
            if (SetProperty(ref _selectedPeriod, value))
            {
                _ = LoadPayrollForSelectedPeriodAsync();
            }
        }
    }

    public PayrollItem? SelectedPayrollDetail
    {
        get => _selectedPayrollDetail;
        set => SetProperty(ref _selectedPayrollDetail, value);
    }

    public string CurrentPayrollNumber => _currentPayroll?.PayrollNumber ?? "Not generated";
    public string CurrentPayrollStatus => _currentPayroll?.Status.ToString() ?? "No payroll";
    public bool HasPayrollItems => PayrollItems.Count > 0;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public decimal TotalGross { get => _totalGross; private set => SetProperty(ref _totalGross, value); }
    public decimal TotalDeductions { get => _totalDeductions; private set => SetProperty(ref _totalDeductions, value); }
    public decimal TotalNet { get => _totalNet; private set => SetProperty(ref _totalNet, value); }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    private async Task LoadPayrollForSelectedPeriodAsync()
    {
        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            var month = ParseSelectedPeriod();
            var periodStart = new DateTime(month.Year, month.Month, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);
            var payrolls = await _dataService.GetPayrollsAsync();
            _currentPayroll = payrolls.FirstOrDefault(payroll => payroll.PeriodStart == periodStart && payroll.PeriodEnd == periodEnd);
            RefreshRows();
            StatusMessage = _currentPayroll is null
                ? $"No payroll has been generated for {SelectedPeriod}."
                : $"Loaded {_currentPayroll.PayrollNumber} ({_currentPayroll.Status}) for {SelectedPeriod}.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load payroll: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task GeneratePayrollAsync()
    {
        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            _currentPayroll = await _workflowService.GenerateMonthlyPayrollAsync(ParseSelectedPeriod());
            RefreshRows();
            StatusMessage = $"Generated {_currentPayroll.PayrollNumber} for {SelectedPeriod}. Draft/calculated duplicates are reused; locked payrolls are protected.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not generate payroll: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReviewPayrollAsync()
    {
        if (_currentPayroll is null)
        {
            return;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            _currentPayroll = await _workflowService.ReviewPayrollAsync(_currentPayroll.Id);
            RefreshRows();
            StatusMessage = $"Reviewed {_currentPayroll.PayrollNumber}. It is ready for approval.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not review payroll: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApprovePayrollAsync()
    {
        if (_currentPayroll is null)
        {
            return;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            _currentPayroll = await _workflowService.ApprovePayrollAsync(_currentPayroll.Id);
            RefreshRows();
            StatusMessage = $"Approved {_currentPayroll.PayrollNumber}. Payroll is now locked from edits.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not approve payroll: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportPayslipsAsync()
    {
        if (_currentPayroll is null)
        {
            return;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            var paths = await _payslipPdfService.GeneratePayslipsAsync(_currentPayroll.Id);
            StatusMessage = $"Exported {paths.Count} payslip PDF(s) to {PayslipPdfService.DefaultPayslipExportFolder}.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not export payslips: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshRows()
    {
        PayrollItems.Clear();
        if (_currentPayroll is not null)
        {
            foreach (var detail in _currentPayroll.Details.OrderBy(detail => detail.Employee.EmployeeNumber))
            {
                PayrollItems.Add(new PayrollItem(
                    PayrollId: _currentPayroll.Id,
                    PayrollDetailId: detail.Id,
                    PayrollNo: _currentPayroll.PayrollNumber,
                    EmployeeCode: detail.Employee.EmployeeNumber,
                    EmployeeName: detail.Employee.FullName,
                    Department: detail.Employee.Department?.Name ?? "-",
                    BasicSalary: detail.BasicSalary,
                    Allowances: detail.Allowance + detail.Bonus + detail.OvertimePay,
                    Deductions: detail.Deduction + detail.Tax,
                    NetPay: detail.NetPay,
                    Status: _currentPayroll.Status.ToString()));
            }
        }

        RecalculateTotals();
        OnPropertyChanged(nameof(CurrentPayrollNumber));
        OnPropertyChanged(nameof(CurrentPayrollStatus));
        OnPropertyChanged(nameof(HasPayrollItems));
        RaiseCommandStates();
    }

    private void RecalculateTotals()
    {
        TotalGross = PayrollItems.Sum(item => item.GrossPay);
        TotalDeductions = PayrollItems.Sum(item => item.Deductions);
        TotalNet = PayrollItems.Sum(item => item.NetPay);
    }

    private bool CanReviewPayroll()
    {
        return !IsBusy && _currentPayroll is not null && _currentPayroll.Status is PayrollStatus.Draft or PayrollStatus.Calculated;
    }

    private bool CanApprovePayroll()
    {
        return !IsBusy && _currentPayroll is not null && _currentPayroll.Status == PayrollStatus.Reviewed;
    }

    private bool CanExportPayslips()
    {
        return !IsBusy && _currentPayroll is not null && PayrollItems.Count > 0;
    }

    private void RaiseCommandStates()
    {
        _generatePayrollCommand.RaiseCanExecuteChanged();
        _reviewPayrollCommand.RaiseCanExecuteChanged();
        _approvePayrollCommand.RaiseCanExecuteChanged();
        _exportPayslipsCommand.RaiseCanExecuteChanged();
    }

    private DateTime ParseSelectedPeriod()
    {
        return DateTime.TryParseExact(SelectedPeriod, "MMMM yyyy", CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : DateTime.Now;
    }

    private static IEnumerable<string> BuildPeriodOptions()
    {
        var current = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        return Enumerable.Range(-5, 8)
            .Select(offset => current.AddMonths(offset).ToString("MMMM yyyy", CultureInfo.CurrentCulture))
            .Reverse();
    }
}

public sealed record PayrollItem(
    int PayrollId,
    int PayrollDetailId,
    string PayrollNo,
    string EmployeeCode,
    string EmployeeName,
    string Department,
    decimal BasicSalary,
    decimal Allowances,
    decimal Deductions,
    decimal NetPay,
    string Status)
{
    public decimal GrossPay => BasicSalary + Allowances;
}
