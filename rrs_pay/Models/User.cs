namespace rrs_pay.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<Payroll> CreatedPayrolls { get; set; } = new List<Payroll>();
    public ICollection<Payroll> ApprovedPayrolls { get; set; } = new List<Payroll>();
}
