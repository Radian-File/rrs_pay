using System.Collections.ObjectModel;
using rrs_pay.ViewModels;

namespace rrs_pay.ViewModels.Pages;

public sealed class PayrollDetailViewModel : ViewModelBase
{
    public PayrollDetailViewModel()
    {
        Earnings = new ObservableCollection<PayslipLineItem>
        {
            new("Basic salary", 12500000m),
            new("Transport allowance", 750000m),
            new("Meal allowance", 500000m)
        };
        Deductions = new ObservableCollection<PayslipLineItem>
        {
            new("BPJS Kesehatan", 250000m),
            new("BPJS Ketenagakerjaan", 300000m),
            new("Income tax", 100000m)
        };
    }

    public string PayrollNo { get; } = "PR-202606-001";
    public string Period { get; } = "June 2026";
    public string EmployeeCode { get; } = "EMP-0001";
    public string EmployeeName { get; } = "Alya Santoso";
    public string Department { get; } = "Finance";
    public string Position { get; } = "Payroll Specialist";
    public string BankAccount { get; } = "Bank Mandiri •••• 1234";
    public ObservableCollection<PayslipLineItem> Earnings { get; }
    public ObservableCollection<PayslipLineItem> Deductions { get; }
    public decimal GrossPay => Earnings.Sum(item => item.Amount);
    public decimal TotalDeductions => Deductions.Sum(item => item.Amount);
    public decimal NetSalary => GrossPay - TotalDeductions;
    public string Notes { get; } = "MVP payslip preview. Values are sample data until the payroll calculation service is connected.";
}

public sealed record PayslipLineItem(string Description, decimal Amount);
