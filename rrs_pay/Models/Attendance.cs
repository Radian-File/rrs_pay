namespace rrs_pay.Models;

public class Attendance
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateTime Date { get; set; }
    public TimeSpan? TimeIn { get; set; }
    public TimeSpan? TimeOut { get; set; }
    public decimal HoursWorked { get; set; }
    public decimal OvertimeHours { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public string? Notes { get; set; }
}
