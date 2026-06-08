using System.Collections.ObjectModel;
using rrs_pay.ViewModels;

namespace rrs_pay.ViewModels.Pages;

public sealed class ReportsViewModel : ViewModelBase
{
    private string _selectedReport = "Payroll Summary";
    private string _selectedPeriod = "June 2026";

    public ReportsViewModel()
    {
        ReportTypes = new ObservableCollection<string> { "Payroll Summary", "Attendance Exceptions", "Employee Headcount", "Statutory Deductions" };
        Periods = new ObservableCollection<string> { "April 2026", "May 2026", "June 2026" };
        ReportRows = new ObservableCollection<ReportRow>
        {
            new("Gross payroll", "Rp 35.42M", "+4.8% vs May"),
            new("Net payout", "Rp 33.13M", "3 employees in sample"),
            new("Late attendance", "11 cases", "Operations highest"),
            new("Open HR actions", "5", "Bank account and tax IDs")
        };
    }

    public ObservableCollection<string> ReportTypes { get; }
    public ObservableCollection<string> Periods { get; }
    public ObservableCollection<ReportRow> ReportRows { get; }
    public string SelectedReport { get => _selectedReport; set => SetProperty(ref _selectedReport, value); }
    public string SelectedPeriod { get => _selectedPeriod; set => SetProperty(ref _selectedPeriod, value); }
}

public sealed record ReportRow(string Metric, string Value, string Notes);
