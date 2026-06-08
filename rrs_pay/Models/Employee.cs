namespace rrs_pay.Models;

public class Employee
{
    public int Id { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public DateTime HireDate { get; set; }
    public EmploymentStatus Status { get; set; } = EmploymentStatus.Active;
    public bool IsActive { get; set; } = true;

    public decimal BasicSalary { get; set; }
    public decimal DefaultAllowance { get; set; }
    public decimal DefaultTaxRate { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }

    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;

    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    public ICollection<PayrollDetail> PayrollDetails { get; set; } = new List<PayrollDetail>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}
