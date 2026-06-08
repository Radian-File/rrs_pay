namespace rrs_pay.Models;

public class Department
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Position> Positions { get; set; } = new List<Position>();
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
