using rrs_pay.Models;

namespace rrs_pay.Services;

public class PayrollCalculationService
{
    public PayrollCalculationResult Calculate(
        Employee employee,
        decimal? allowance = null,
        decimal bonus = 0m,
        decimal overtimePay = 0m,
        decimal deduction = 0m,
        decimal? tax = null)
    {
        if (employee is null)
        {
            throw new ArgumentNullException(nameof(employee));
        }

        var basic = employee.BasicSalary;
        var resolvedAllowance = allowance ?? employee.DefaultAllowance;
        var gross = basic + resolvedAllowance + bonus + overtimePay;
        var resolvedTax = tax ?? Math.Round(gross * employee.DefaultTaxRate, 2, MidpointRounding.AwayFromZero);
        var net = gross - deduction - resolvedTax;

        return new PayrollCalculationResult(
            EmployeeId: employee.Id,
            BasicSalary: basic,
            Allowance: resolvedAllowance,
            Bonus: bonus,
            OvertimePay: overtimePay,
            Deduction: deduction,
            Tax: resolvedTax,
            GrossPay: gross,
            NetPay: net);
    }

    public PayrollCalculationResult CalculateFromOvertimeHours(
        Employee employee,
        decimal overtimeHours,
        decimal overtimeMultiplier = 1.5m,
        decimal standardWorkingDaysPerMonth = 22m,
        decimal standardWorkingHoursPerDay = 8m,
        decimal? allowance = null,
        decimal bonus = 0m,
        decimal deduction = 0m,
        decimal? tax = null)
    {
        if (standardWorkingDaysPerMonth <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(standardWorkingDaysPerMonth), "Working days must be greater than zero.");
        }

        if (standardWorkingHoursPerDay <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(standardWorkingHoursPerDay), "Working hours must be greater than zero.");
        }

        var hourlyRate = employee.BasicSalary / standardWorkingDaysPerMonth / standardWorkingHoursPerDay;
        var overtimePay = Math.Round(hourlyRate * overtimeMultiplier * overtimeHours, 2, MidpointRounding.AwayFromZero);
        return Calculate(employee, allowance, bonus, overtimePay, deduction, tax);
    }

    public PayrollTotals CalculateTotals(IEnumerable<PayrollCalculationResult> details)
    {
        var materializedDetails = details.ToList();
        return new PayrollTotals(
            GrossTotal: materializedDetails.Sum(detail => detail.GrossPay),
            DeductionTotal: materializedDetails.Sum(detail => detail.Deduction),
            TaxTotal: materializedDetails.Sum(detail => detail.Tax),
            NetTotal: materializedDetails.Sum(detail => detail.NetPay));
    }
}

public record PayrollCalculationResult(
    int EmployeeId,
    decimal BasicSalary,
    decimal Allowance,
    decimal Bonus,
    decimal OvertimePay,
    decimal Deduction,
    decimal Tax,
    decimal GrossPay,
    decimal NetPay);

public record PayrollTotals(
    decimal GrossTotal,
    decimal DeductionTotal,
    decimal TaxTotal,
    decimal NetTotal);
