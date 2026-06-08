namespace rrs_pay.Models;

public class PayrollSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EffectiveDate { get; set; }
    public bool IsActive { get; set; } = true;
}
