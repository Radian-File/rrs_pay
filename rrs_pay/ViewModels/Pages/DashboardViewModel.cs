using System.Collections.ObjectModel;
using rrs_pay.ViewModels;

namespace rrs_pay.ViewModels.Pages;

public sealed class DashboardViewModel : ViewModelBase
{
    public DashboardViewModel()
    {
        Metrics = new ObservableCollection<DashboardMetric>
        {
            new("Active Employees", "128", "+6 this month", "#FF38BDF8"),
            new("Pending Attendance", "14", "Needs review", "#FFF59E0B"),
            new("Payroll Run", "June 2026", "Draft ready", "#FF22C55E"),
            new("Net Payroll", "Rp 482.5M", "Projected payout", "#FFA78BFA")
        };

        RecentActivities = new ObservableCollection<ActivityItem>
        {
            new("Attendance imported", "June daily sheet synced for Operations", "Today 09:15"),
            new("Employee updated", "Maya Putri moved to Finance", "Today 08:44"),
            new("Payroll draft", "May 2026 payroll was generated", "Yesterday 17:30")
        };

        PayrollQueue = new ObservableCollection<PayrollQueueItem>
        {
            new("Attendance Review", 14, "Open"),
            new("Missing Bank Accounts", 3, "Needs HR"),
            new("Ready Payslips", 118, "Ready")
        };
    }

    public ObservableCollection<DashboardMetric> Metrics { get; }
    public ObservableCollection<ActivityItem> RecentActivities { get; }
    public ObservableCollection<PayrollQueueItem> PayrollQueue { get; }
}

public sealed record DashboardMetric(string Title, string Value, string Detail, string AccentColor);
public sealed record ActivityItem(string Title, string Description, string Time);
public sealed record PayrollQueueItem(string Name, int Count, string Status);
