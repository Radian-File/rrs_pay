namespace rrs_pay.Models;

public class Payroll
{
    public int Id { get; set; }
    public string PayrollNumber { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime PayDate { get; set; }
    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public string? Notes { get; set; }

    public decimal GrossTotal { get; set; }
    public decimal DeductionTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal NetTotal { get; set; }

    public int? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public int? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }

    public ICollection<PayrollDetail> Details { get; set; } = new List<PayrollDetail>();
}

public class PayrollDetail
{
    public int Id { get; set; }

    public int PayrollId { get; set; }
    public Payroll Payroll { get; set; } = null!;

    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public decimal BasicSalary { get; set; }
    public decimal Allowance { get; set; }
    public decimal Bonus { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal Deduction { get; set; }
    public decimal Tax { get; set; }
    public decimal GrossPay { get; set; }
    public decimal NetPay { get; set; }
    public string? Notes { get; set; }
}
