using System.Collections.ObjectModel;
using rrs_pay.ViewModels;

namespace rrs_pay.ViewModels.Pages;

public sealed class AuditLogsViewModel : ViewModelBase
{
    private string _searchText = string.Empty;

    public AuditLogsViewModel()
    {
        AuditEntries = new ObservableCollection<AuditLogEntry>
        {
            new(DateTime.Now.AddMinutes(-18), "admin@rrspay.local", "Generated payroll draft", "Payroll", "June 2026"),
            new(DateTime.Now.AddHours(-2), "hr@rrspay.local", "Updated employee department", "Employee", "EMP-0002"),
            new(DateTime.Now.AddDays(-1), "system", "Imported attendance file", "Attendance", "attendance_june_week1.xlsx"),
            new(DateTime.Now.AddDays(-2), "finance@rrspay.local", "Approved payslip batch", "Payroll", "May 2026")
        };
    }

    public ObservableCollection<AuditLogEntry> AuditEntries { get; }
    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
}

public sealed record AuditLogEntry(DateTime Timestamp, string Actor, string Action, string Area, string Reference);
