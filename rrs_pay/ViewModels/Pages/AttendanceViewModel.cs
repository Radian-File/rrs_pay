using System.Collections.ObjectModel;
using rrs_pay.ViewModels;

namespace rrs_pay.ViewModels.Pages;

public sealed class AttendanceViewModel : ViewModelBase
{
    private string _selectedMonth = "June 2026";
    private string _selectedStatus = "All";

    public AttendanceViewModel()
    {
        Months = new ObservableCollection<string> { "April 2026", "May 2026", "June 2026" };
        Statuses = new ObservableCollection<string> { "All", "Present", "Late", "Absent", "Leave" };
        AttendanceRecords = new ObservableCollection<AttendanceRecord>
        {
            new(new DateTime(2026, 6, 1), "EMP-0001", "Alya Santoso", "Finance", "Present", TimeSpan.Parse("08:00"), TimeSpan.Parse("17:05"), 0),
            new(new DateTime(2026, 6, 1), "EMP-0002", "Bima Pratama", "Operations", "Late", TimeSpan.Parse("08:22"), TimeSpan.Parse("17:00"), 22),
            new(new DateTime(2026, 6, 1), "EMP-0003", "Citra Dewi", "Human Resources", "Leave", null, null, 0),
            new(new DateTime(2026, 6, 2), "EMP-0002", "Bima Pratama", "Operations", "Present", TimeSpan.Parse("07:55"), TimeSpan.Parse("17:02"), 0)
        };
        Summary = new ObservableCollection<AttendanceSummary>
        {
            new("Present", 92, "92%"),
            new("Late", 11, "11 cases"),
            new("Absent", 2, "Needs approval"),
            new("Leave", 7, "Approved")
        };
        RefreshCommand = new RelayCommand(() => LastRefreshMessage = $"Attendance refreshed for {SelectedMonth} at {DateTime.Now:HH:mm}");
    }

    public ObservableCollection<string> Months { get; }
    public ObservableCollection<string> Statuses { get; }
    public ObservableCollection<AttendanceRecord> AttendanceRecords { get; }
    public ObservableCollection<AttendanceSummary> Summary { get; }
    public RelayCommand RefreshCommand { get; }

    public string SelectedMonth { get => _selectedMonth; set => SetProperty(ref _selectedMonth, value); }
    public string SelectedStatus { get => _selectedStatus; set => SetProperty(ref _selectedStatus, value); }

    private string _lastRefreshMessage = "Showing loaded sample attendance records.";
    public string LastRefreshMessage { get => _lastRefreshMessage; set => SetProperty(ref _lastRefreshMessage, value); }
}

public sealed record AttendanceSummary(string Label, int Count, string Detail);

public sealed record AttendanceRecord(DateTime Date, string EmployeeCode, string EmployeeName, string Department, string Status, TimeSpan? ClockIn, TimeSpan? ClockOut, int LateMinutes)
{
    public string ClockInText => ClockIn?.ToString(@"hh\:mm") ?? "-";
    public string ClockOutText => ClockOut?.ToString(@"hh\:mm") ?? "-";
}
