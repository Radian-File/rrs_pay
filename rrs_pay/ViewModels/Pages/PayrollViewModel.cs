using System.Collections.ObjectModel;
using rrs_pay.ViewModels;

namespace rrs_pay.ViewModels.Pages;

public sealed class PayrollViewModel : ViewModelBase
{
    private string _selectedPeriod = "June 2026";
    private string _statusMessage = "Draft payroll is based on loaded sample employees and attendance.";

    public PayrollViewModel()
    {
        Periods = new ObservableCollection<string> { "April 2026", "May 2026", "June 2026" };
        PayrollItems = new ObservableCollection<PayrollItem>
        {
            new("PR-202606-001", "EMP-0001", "Alya Santoso", "Finance", 12500000m, 1250000m, 650000m, 13100000m, "Draft"),
            new("PR-202606-002", "EMP-0002", "Bima Pratama", "Operations", 9800000m, 620000m, 510000m, 9910000m, "Needs review"),
            new("PR-202606-003", "EMP-0003", "Citra Dewi", "Human Resources", 10250000m, 400000m, 530000m, 10120000m, "Draft")
        };
        GeneratePayrollCommand = new RelayCommand(GeneratePayroll);
        RecalculateTotals();
    }

    public ObservableCollection<string> Periods { get; }
    public ObservableCollection<PayrollItem> PayrollItems { get; }
    public RelayCommand GeneratePayrollCommand { get; }

    public string SelectedPeriod { get => _selectedPeriod; set => SetProperty(ref _selectedPeriod, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private decimal _totalGross;
    private decimal _totalDeductions;
    private decimal _totalNet;

    public decimal TotalGross { get => _totalGross; private set => SetProperty(ref _totalGross, value); }
    public decimal TotalDeductions { get => _totalDeductions; private set => SetProperty(ref _totalDeductions, value); }
    public decimal TotalNet { get => _totalNet; private set => SetProperty(ref _totalNet, value); }

    private void GeneratePayroll()
    {
        StatusMessage = $"Generated sample payroll draft for {SelectedPeriod}. Replace this with payroll service integration.";
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        TotalGross = PayrollItems.Sum(item => item.GrossPay);
        TotalDeductions = PayrollItems.Sum(item => item.Deductions);
        TotalNet = PayrollItems.Sum(item => item.NetPay);
    }
}

public sealed record PayrollItem(string PayrollNo, string EmployeeCode, string EmployeeName, string Department, decimal BasicSalary, decimal Allowances, decimal Deductions, decimal NetPay, string Status)
{
    public decimal GrossPay => BasicSalary + Allowances;
}
