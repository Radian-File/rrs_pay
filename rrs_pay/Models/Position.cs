namespace rrs_pay.Models;

public class Position
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DefaultBasicSalary { get; set; }
    public bool IsActive { get; set; } = true;

    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
