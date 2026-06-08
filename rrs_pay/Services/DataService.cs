using Microsoft.EntityFrameworkCore;
using rrs_pay.Data;
using rrs_pay.Models;

namespace rrs_pay.Services;

public class DataService
{
    private readonly Func<AppDbContext> _contextFactory;

    public DataService() : this(AppDbContext.CreateDefault)
    {
    }

    public DataService(Func<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<List<Role>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        return await context.Roles.OrderBy(role => role.Name).ToListAsync(cancellationToken);
    }

    public async Task<List<Department>> GetDepartmentsAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var query = context.Departments.AsQueryable();
        if (activeOnly)
        {
            query = query.Where(department => department.IsActive);
        }

        return await query.OrderBy(department => department.Name).ToListAsync(cancellationToken);
    }

    public async Task<List<Position>> GetPositionsAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var query = context.Positions.Include(position => position.Department).AsQueryable();
        if (activeOnly)
        {
            query = query.Where(position => position.IsActive);
        }

        return await query
            .OrderBy(position => position.Department.Name)
            .ThenBy(position => position.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Employee>> GetEmployeesAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var query = context.Employees
            .Include(employee => employee.Department)
            .Include(employee => employee.Position)
            .AsQueryable();

        if (activeOnly)
        {
            query = query.Where(employee => employee.IsActive);
        }

        return await query
            .OrderBy(employee => employee.EmployeeNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<Employee?> GetEmployeeByIdAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        return await context.Employees
            .Include(employee => employee.Department)
            .Include(employee => employee.Position)
            .FirstOrDefaultAsync(employee => employee.Id == employeeId, cancellationToken);
    }

    public async Task<Employee> SaveEmployeeAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();

        if (employee.Id == 0)
        {
            context.Employees.Add(employee);
        }
        else
        {
            context.Employees.Update(employee);
        }

        await context.SaveChangesAsync(cancellationToken);
        return employee;
    }

    public async Task DeactivateEmployeeAsync(int employeeId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var employee = await context.Employees.FirstOrDefaultAsync(item => item.Id == employeeId, cancellationToken)
            ?? throw new InvalidOperationException($"Employee with id {employeeId} was not found.");

        employee.IsActive = false;
        employee.Status = EmploymentStatus.Terminated;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Attendance>> GetAttendanceAsync(DateTime startDate, DateTime endDate, int? employeeId = null, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var start = startDate.Date;
        var end = endDate.Date;
        var query = context.Attendances
            .Include(attendance => attendance.Employee)
            .ThenInclude(employee => employee.Department)
            .Where(attendance => attendance.Date >= start && attendance.Date <= end);

        if (employeeId.HasValue)
        {
            query = query.Where(attendance => attendance.EmployeeId == employeeId.Value);
        }

        return await query
            .OrderByDescending(attendance => attendance.Date)
            .ThenBy(attendance => attendance.Employee.EmployeeNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<Attendance> SaveAttendanceAsync(Attendance attendance, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();

        if (attendance.Id == 0)
        {
            context.Attendances.Add(attendance);
        }
        else
        {
            context.Attendances.Update(attendance);
        }

        await context.SaveChangesAsync(cancellationToken);
        return attendance;
    }

    public async Task<List<Payroll>> GetPayrollsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        return await context.Payrolls
            .Include(payroll => payroll.CreatedByUser)
            .Include(payroll => payroll.ApprovedByUser)
            .Include(payroll => payroll.Details)
            .OrderByDescending(payroll => payroll.PeriodEnd)
            .ThenByDescending(payroll => payroll.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Payroll?> GetPayrollByIdAsync(int payrollId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        return await context.Payrolls
            .Include(payroll => payroll.CreatedByUser)
            .Include(payroll => payroll.ApprovedByUser)
            .Include(payroll => payroll.Details)
            .ThenInclude(detail => detail.Employee)
            .FirstOrDefaultAsync(payroll => payroll.Id == payrollId, cancellationToken);
    }

    public async Task<Payroll> SavePayrollAsync(Payroll payroll, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();

        if (payroll.Id == 0)
        {
            context.Payrolls.Add(payroll);
        }
        else
        {
            context.Payrolls.Update(payroll);
        }

        await context.SaveChangesAsync(cancellationToken);
        return payroll;
    }

    public async Task<List<PayrollSetting>> GetPayrollSettingsAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var query = context.PayrollSettings.AsQueryable();
        if (activeOnly)
        {
            query = query.Where(setting => setting.IsActive);
        }

        return await query.OrderBy(setting => setting.Key).ToListAsync(cancellationToken);
    }

    public async Task<PayrollSetting?> GetPayrollSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        return await context.PayrollSettings.FirstOrDefaultAsync(setting => setting.Key == key, cancellationToken);
    }

    public async Task<PayrollSetting> SavePayrollSettingAsync(PayrollSetting setting, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();

        if (setting.Id == 0)
        {
            context.PayrollSettings.Add(setting);
        }
        else
        {
            context.PayrollSettings.Update(setting);
        }

        await context.SaveChangesAsync(cancellationToken);
        return setting;
    }
}
