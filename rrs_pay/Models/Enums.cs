namespace rrs_pay.Models;

public enum EmploymentStatus
{
    Active = 0,
    Probation = 1,
    Suspended = 2,
    Terminated = 3
}

public enum AttendanceStatus
{
    Present = 0,
    Absent = 1,
    Late = 2,
    HalfDay = 3,
    Leave = 4,
    Holiday = 5
}

public enum PayrollStatus
{
    Draft = 0,
    Calculated = 1,
    Approved = 2,
    Paid = 3,
    Cancelled = 4,
    Reviewed = 5
}
