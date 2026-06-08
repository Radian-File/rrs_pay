using System.Collections.ObjectModel;
using System.Globalization;
using rrs_pay.Models;
using rrs_pay.Services;
using rrs_pay.ViewModels;
using rrs_pay.ViewModels.Infrastructure;

namespace rrs_pay.ViewModels.Pages;

public sealed class AttendanceViewModel : ViewModelBase
{
    private readonly DataService _dataService;
    private readonly AuditLogService _auditLogService;

    private string _selectedMonth = DateTime.Today.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
    private string _selectedStatus = "All";
    private bool _isBusy;

    public AttendanceViewModel() : this(new DataService(), new AuditLogService())
    {
    }

    public AttendanceViewModel(DataService dataService, AuditLogService auditLogService)
    {
        _dataService = dataService;
        _auditLogService = auditLogService;
        Months = new ObservableCollection<string>(Enumerable.Range(-5, 8)
            .Select(offset => DateTime.Today.AddMonths(offset).ToString("MMMM yyyy", CultureInfo.InvariantCulture))
            .Distinct());
        Statuses = new ObservableCollection<string>(new[] { "All" }.Concat(Enum.GetNames<AttendanceStatus>()));
        AttendanceRecords = new ObservableCollection<AttendanceRecord>();
        Summary = new ObservableCollection<AttendanceSummary>();
        RefreshCommand = new AsyncRelayCommand(LoadAttendanceAsync, () => !IsBusy);
        _ = LoadAttendanceAsync();
    }

    public ObservableCollection<string> Months { get; }
    public ObservableCollection<string> Statuses { get; }
    public ObservableCollection<AttendanceRecord> AttendanceRecords { get; }
    public ObservableCollection<AttendanceSummary> Summary { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    public string SelectedMonth
    {
        get => _selectedMonth;
        set
        {
            if (SetProperty(ref _selectedMonth, value))
            {
                _ = LoadAttendanceAsync();
            }
        }
    }

    public string SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (SetProperty(ref _selectedStatus, value))
            {
                _ = LoadAttendanceAsync();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private string _lastRefreshMessage = "Loading attendance records from database...";
    public string LastRefreshMessage { get => _lastRefreshMessage; set => SetProperty(ref _lastRefreshMessage, value); }

    private async Task LoadAttendanceAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var (startDate, endDate) = ResolveMonthRange(SelectedMonth);
            var records = await _dataService.GetAttendanceAsync(startDate, endDate);
            if (!SelectedStatus.Equals("All", StringComparison.OrdinalIgnoreCase) && Enum.TryParse<AttendanceStatus>(SelectedStatus, out var status))
            {
                records = records.Where(record => record.Status == status).ToList();
            }

            AttendanceRecords.Clear();
            foreach (var record in records)
            {
                AttendanceRecords.Add(AttendanceRecord.FromModel(record));
            }

            RebuildSummary(records);
            LastRefreshMessage = $"Loaded {AttendanceRecords.Count} attendance records for {SelectedMonth} at {DateTime.Now:HH:mm}.";
        }
        catch (Exception ex)
        {
            LastRefreshMessage = $"Failed to load attendance: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveAttendanceAsync(int employeeId, DateTime date, TimeSpan? clockIn, TimeSpan? clockOut, AttendanceStatus status, string? notes = null)
    {
        var hoursWorked = 0m;
        if (clockIn.HasValue && clockOut.HasValue && clockOut.Value > clockIn.Value)
        {
            hoursWorked = (decimal)(clockOut.Value - clockIn.Value).TotalHours;
        }

        var attendance = new Attendance
        {
            EmployeeId = employeeId,
            Date = date.Date,
            TimeIn = clockIn,
            TimeOut = clockOut,
            HoursWorked = hoursWorked,
            OvertimeHours = Math.Max(0, hoursWorked - 8m),
            Status = status,
            Notes = notes
        };

        var saved = await _dataService.SaveAttendanceAsync(attendance);
        await _auditLogService.LogAsync("Save Attendance", nameof(Attendance), saved.Id.ToString(), $"Employee {employeeId} on {date:yyyy-MM-dd}: {status}");
        await LoadAttendanceAsync();
    }

    private void RebuildSummary(IEnumerable<Attendance> records)
    {
        var materialized = records.ToList();
        Summary.Clear();
        foreach (var status in Enum.GetValues<AttendanceStatus>())
        {
            var count = materialized.Count(record => record.Status == status);
            if (count == 0 && status is AttendanceStatus.HalfDay or AttendanceStatus.Holiday)
            {
                continue;
            }

            Summary.Add(new AttendanceSummary(status.ToString(), count, count == 1 ? "1 record" : $"{count} records"));
        }
    }

    private static (DateTime Start, DateTime End) ResolveMonthRange(string monthText)
    {
        if (!DateTime.TryParseExact(monthText, "MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            month = DateTime.Today;
        }

        var start = new DateTime(month.Year, month.Month, 1);
        return (start, start.AddMonths(1).AddDays(-1));
    }
}

public sealed record AttendanceSummary(string Label, int Count, string Detail);

public sealed record AttendanceRecord(DateTime Date, string EmployeeCode, string EmployeeName, string Department, string Status, TimeSpan? ClockIn, TimeSpan? ClockOut, int LateMinutes)
{
    public string ClockInText => ClockIn?.ToString(@"hh\:mm") ?? "-";
    public string ClockOutText => ClockOut?.ToString(@"hh\:mm") ?? "-";

    public static AttendanceRecord FromModel(Attendance attendance) => new(
        attendance.Date,
        attendance.Employee.EmployeeNumber,
        attendance.Employee.FullName,
        attendance.Employee.Department.Name,
        attendance.Status.ToString(),
        attendance.TimeIn,
        attendance.TimeOut,
        CalculateLateMinutes(attendance.TimeIn));

    private static int CalculateLateMinutes(TimeSpan? clockIn)
    {
        if (!clockIn.HasValue)
        {
            return 0;
        }

        var graceTime = new TimeSpan(8, 10, 0);
        return clockIn.Value > graceTime ? (int)Math.Round((clockIn.Value - graceTime).TotalMinutes) : 0;
    }
}
