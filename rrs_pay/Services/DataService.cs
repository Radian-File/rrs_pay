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
        return await context.Roles.AsNoTracking().OrderBy(role => role.Name).ToListAsync(cancellationToken);
    }

    public async Task<List<Department>> GetDepartmentsAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var query = context.Departments.AsNoTracking().AsQueryable();
        if (activeOnly)
        {
            query = query.Where(department => department.IsActive);
        }

        return await query.OrderBy(department => department.Name).ToListAsync(cancellationToken);
    }

    public async Task<List<Department>> GetDepartmentsWithEmployeesAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var query = context.Departments
            .AsNoTracking()
            .Include(department => department.Employees.Where(employee => employee.IsActive))
            .AsQueryable();

        if (activeOnly)
        {
            query = query.Where(department => department.IsActive);
        }

        return await query.OrderBy(department => department.Name).ToListAsync(cancellationToken);
    }

    public async Task<Department?> GetDepartmentByIdAsync(int departmentId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        return await context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(department => department.Id == departmentId, cancellationToken);
    }

    public async Task<Department?> GetDepartmentByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var normalizedCode = code.Trim().ToUpperInvariant();
        return await context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(department => department.Code.ToUpper() == normalizedCode, cancellationToken);
    }

    public async Task<Department> SaveDepartmentAsync(Department department, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(department.Code))
        {
            throw new InvalidOperationException("Department code is required.");
        }

        if (string.IsNullOrWhiteSpace(department.Name))
        {
            throw new InvalidOperationException("Department name is required.");
        }

        await using var context = _contextFactory();
        department.Code = department.Code.Trim().ToUpperInvariant();
        department.Name = department.Name.Trim();
        department.Description = string.IsNullOrWhiteSpace(department.Description) ? null : department.Description.Trim();

        var duplicate = await context.Departments.AsNoTracking().FirstOrDefaultAsync(item =>
            item.Id != department.Id &&
            (item.Code.ToUpper() == department.Code || item.Name.ToUpper() == department.Name.ToUpper()), cancellationToken);

        if (duplicate is not null)
        {
            throw new InvalidOperationException("Another department already uses the same code or name.");
        }

        if (department.Id == 0)
        {
            context.Departments.Add(department);
        }
        else
        {
            var existing = await context.Departments.FirstOrDefaultAsync(item => item.Id == department.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Department with id {department.Id} was not found.");

            existing.Code = department.Code;
            existing.Name = department.Name;
            existing.Description = department.Description;
            existing.IsActive = department.IsActive;
        }

        await context.SaveChangesAsync(cancellationToken);
        return department.Id == 0
            ? department
            : await GetDepartmentByIdAsync(department.Id, cancellationToken) ?? department;
    }

    public async Task<List<Position>> GetPositionsAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var query = context.Positions
            .AsNoTracking()
            .Include(position => position.Department)
            .Include(position => position.Employees.Where(employee => employee.IsActive))
            .AsQueryable();
        if (activeOnly)
        {
            query = query.Where(position => position.IsActive);
        }

        return await query
            .OrderBy(position => position.Department.Name)
            .ThenBy(position => position.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<Position?> GetPositionByIdAsync(int positionId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        return await context.Positions
            .AsNoTracking()
            .Include(position => position.Department)
            .FirstOrDefaultAsync(position => position.Id == positionId, cancellationToken);
    }

    public async Task<Position> SavePositionAsync(Position position, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(position.Code))
        {
            throw new InvalidOperationException("Position code is required.");
        }

        if (string.IsNullOrWhiteSpace(position.Title))
        {
            throw new InvalidOperationException("Position title is required.");
        }

        await using var context = _contextFactory();
        var departmentExists = await context.Departments.AnyAsync(item => item.Id == position.DepartmentId && item.IsActive, cancellationToken);
        if (!departmentExists)
        {
            throw new InvalidOperationException("Select a valid active department for this position.");
        }

        position.Code = position.Code.Trim().ToUpperInvariant();
        position.Title = position.Title.Trim();
        position.Description = string.IsNullOrWhiteSpace(position.Description) ? null : position.Description.Trim();

        var duplicate = await context.Positions.AsNoTracking().FirstOrDefaultAsync(item =>
            item.Id != position.Id && item.Code.ToUpper() == position.Code, cancellationToken);

        if (duplicate is not null)
        {
            throw new InvalidOperationException("Another position already uses the same code.");
        }

        if (position.Id == 0)
        {
            context.Positions.Add(position);
        }
        else
        {
            var existing = await context.Positions.FirstOrDefaultAsync(item => item.Id == position.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Position with id {position.Id} was not found.");

            existing.Code = position.Code;
            existing.Title = position.Title;
            existing.DepartmentId = position.DepartmentId;
            existing.DefaultBasicSalary = position.DefaultBasicSalary;
            existing.Description = position.Description;
            existing.IsActive = position.IsActive;
        }

        await context.SaveChangesAsync(cancellationToken);
        return position.Id == 0
            ? position
            : await GetPositionByIdAsync(position.Id, cancellationToken) ?? position;
    }

    public async Task<List<Employee>> GetEmployeesAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var query = context.Employees
            .AsNoTracking()
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
            .AsNoTracking()
            .Include(employee => employee.Department)
            .Include(employee => employee.Position)
            .FirstOrDefaultAsync(employee => employee.Id == employeeId, cancellationToken);
    }

    public async Task<Employee> SaveEmployeeAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employee.EmployeeNumber))
        {
            throw new InvalidOperationException("Employee code is required.");
        }

        if (string.IsNullOrWhiteSpace(employee.FirstName) || string.IsNullOrWhiteSpace(employee.LastName))
        {
            throw new InvalidOperationException("Employee first and last name are required.");
        }

        await using var context = _contextFactory();
        var departmentExists = await context.Departments.AnyAsync(item => item.Id == employee.DepartmentId && item.IsActive, cancellationToken);
        var positionExists = await context.Positions.AnyAsync(item => item.Id == employee.PositionId && item.DepartmentId == employee.DepartmentId && item.IsActive, cancellationToken);

        if (!departmentExists)
        {
            throw new InvalidOperationException("Select a valid active department for this employee.");
        }

        if (!positionExists)
        {
            throw new InvalidOperationException("Select a valid active position in the selected department.");
        }

        employee.EmployeeNumber = employee.EmployeeNumber.Trim().ToUpperInvariant();
        employee.FirstName = employee.FirstName.Trim();
        employee.LastName = employee.LastName.Trim();
        employee.Email = string.IsNullOrWhiteSpace(employee.Email) ? null : employee.Email.Trim();
        employee.Phone = string.IsNullOrWhiteSpace(employee.Phone) ? null : employee.Phone.Trim();
        employee.Address = string.IsNullOrWhiteSpace(employee.Address) ? null : employee.Address.Trim();
        employee.BankName = string.IsNullOrWhiteSpace(employee.BankName) ? null : employee.BankName.Trim();
        employee.BankAccountNumber = string.IsNullOrWhiteSpace(employee.BankAccountNumber) ? null : employee.BankAccountNumber.Trim();

        var duplicate = await context.Employees.AsNoTracking().FirstOrDefaultAsync(item =>
            item.Id != employee.Id &&
            (item.EmployeeNumber.ToUpper() == employee.EmployeeNumber ||
             (employee.Email != null && item.Email != null && item.Email.ToUpper() == employee.Email.ToUpper())), cancellationToken);

        if (duplicate is not null)
        {
            throw new InvalidOperationException("Another employee already uses the same code or email.");
        }

        if (employee.Id == 0)
        {
            context.Employees.Add(employee);
        }
        else
        {
            var existing = await context.Employees.FirstOrDefaultAsync(item => item.Id == employee.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Employee with id {employee.Id} was not found.");

            existing.EmployeeNumber = employee.EmployeeNumber;
            existing.FirstName = employee.FirstName;
            existing.LastName = employee.LastName;
            existing.Email = employee.Email;
            existing.Phone = employee.Phone;
            existing.Address = employee.Address;
            existing.HireDate = employee.HireDate == default ? existing.HireDate : employee.HireDate;
            existing.Status = employee.Status;
            existing.IsActive = employee.IsActive;
            existing.BasicSalary = employee.BasicSalary;
            existing.DefaultAllowance = employee.DefaultAllowance;
            existing.DefaultTaxRate = employee.DefaultTaxRate;
            existing.BankName = employee.BankName;
            existing.BankAccountNumber = employee.BankAccountNumber;
            existing.DepartmentId = employee.DepartmentId;
            existing.PositionId = employee.PositionId;
        }

        await context.SaveChangesAsync(cancellationToken);
        return employee.Id == 0
            ? employee
            : await GetEmployeeByIdAsync(employee.Id, cancellationToken) ?? employee;
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
            .AsNoTracking()
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

    public async Task<Attendance?> GetAttendanceByEmployeeDateAsync(int employeeId, DateTime date, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        return await context.Attendances
            .AsNoTracking()
            .Include(attendance => attendance.Employee)
            .FirstOrDefaultAsync(attendance => attendance.EmployeeId == employeeId && attendance.Date == date.Date, cancellationToken);
    }

    public async Task<Attendance> SaveAttendanceAsync(Attendance attendance, CancellationToken cancellationToken = default)
    {
        if (attendance.EmployeeId <= 0)
        {
            throw new InvalidOperationException("Employee is required for attendance.");
        }

        await using var context = _contextFactory();
        attendance.Date = attendance.Date.Date;
        attendance.Notes = string.IsNullOrWhiteSpace(attendance.Notes) ? null : attendance.Notes.Trim();

        var employeeExists = await context.Employees.AnyAsync(item => item.Id == attendance.EmployeeId && item.IsActive, cancellationToken);
        if (!employeeExists)
        {
            throw new InvalidOperationException("Select a valid active employee for attendance.");
        }

        var existing = attendance.Id == 0
            ? await context.Attendances.FirstOrDefaultAsync(item => item.EmployeeId == attendance.EmployeeId && item.Date == attendance.Date, cancellationToken)
            : await context.Attendances.FirstOrDefaultAsync(item => item.Id == attendance.Id, cancellationToken);

        if (existing is null)
        {
            context.Attendances.Add(attendance);
        }
        else
        {
            existing.EmployeeId = attendance.EmployeeId;
            existing.Date = attendance.Date;
            existing.TimeIn = attendance.TimeIn;
            existing.TimeOut = attendance.TimeOut;
            existing.HoursWorked = attendance.HoursWorked;
            existing.OvertimeHours = attendance.OvertimeHours;
            existing.Status = attendance.Status;
            existing.Notes = attendance.Notes;
        }

        await context.SaveChangesAsync(cancellationToken);
        return existing ?? attendance;
    }

    public async Task<List<Payroll>> GetPayrollsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        return await context.Payrolls
            .AsNoTracking()
            .Include(payroll => payroll.CreatedByUser)
            .Include(payroll => payroll.ApprovedByUser)
            .Include(payroll => payroll.Details)
            .ThenInclude(detail => detail.Employee)
            .ThenInclude(employee => employee.Department)
            .Include(payroll => payroll.Details)
            .ThenInclude(detail => detail.Employee)
            .ThenInclude(employee => employee.Position)
            .OrderByDescending(payroll => payroll.PeriodEnd)
            .ThenByDescending(payroll => payroll.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Payroll?> GetPayrollByIdAsync(int payrollId, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        return await context.Payrolls
            .AsNoTracking()
            .Include(payroll => payroll.CreatedByUser)
            .Include(payroll => payroll.ApprovedByUser)
            .Include(payroll => payroll.Details)
            .ThenInclude(detail => detail.Employee)
            .ThenInclude(employee => employee.Department)
            .Include(payroll => payroll.Details)
            .ThenInclude(detail => detail.Employee)
            .ThenInclude(employee => employee.Position)
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
            var existing = await context.Payrolls.AsNoTracking().FirstOrDefaultAsync(item => item.Id == payroll.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Payroll with id {payroll.Id} was not found.");

            if (existing.Status is PayrollStatus.Approved or PayrollStatus.Paid)
            {
                throw new InvalidOperationException($"Payroll {existing.PayrollNumber} is {existing.Status} and locked from edits.");
            }

            context.Payrolls.Update(payroll);
        }

        await context.SaveChangesAsync(cancellationToken);
        return payroll;
    }

    public async Task<List<AuditLog>> GetAuditLogsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? action = null,
        string? user = null,
        string? search = null,
        int maxRows = 500,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var query = context.AuditLogs
            .AsNoTracking()
            .Include(audit => audit.User)
            .AsQueryable();

        if (startDate.HasValue)
        {
            var start = startDate.Value.Date;
            query = query.Where(audit => audit.Timestamp >= start);
        }

        if (endDate.HasValue)
        {
            var endExclusive = endDate.Value.Date.AddDays(1);
            query = query.Where(audit => audit.Timestamp < endExclusive);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var actionFilter = action.Trim().ToUpperInvariant();
            query = query.Where(audit => audit.Action.ToUpper().Contains(actionFilter));
        }

        if (!string.IsNullOrWhiteSpace(user))
        {
            var userFilter = user.Trim().ToUpperInvariant();
            query = query.Where(audit => audit.User != null &&
                (audit.User.Username.ToUpper().Contains(userFilter) || audit.User.FullName.ToUpper().Contains(userFilter)));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchFilter = search.Trim().ToUpperInvariant();
            query = query.Where(audit =>
                audit.Action.ToUpper().Contains(searchFilter) ||
                audit.EntityName.ToUpper().Contains(searchFilter) ||
                (audit.EntityId != null && audit.EntityId.ToUpper().Contains(searchFilter)) ||
                (audit.Details != null && audit.Details.ToUpper().Contains(searchFilter)) ||
                (audit.User != null && (audit.User.Username.ToUpper().Contains(searchFilter) || audit.User.FullName.ToUpper().Contains(searchFilter))));
        }

        return await query
            .OrderByDescending(audit => audit.Timestamp)
            .Take(Math.Clamp(maxRows, 1, 2000))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PayrollSetting>> GetPayrollSettingsAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var query = context.PayrollSettings.AsNoTracking().AsQueryable();
        if (activeOnly)
        {
            query = query.Where(setting => setting.IsActive);
        }

        return await query.OrderBy(setting => setting.Key).ToListAsync(cancellationToken);
    }

    public async Task<PayrollSetting?> GetPayrollSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        return await context.PayrollSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(setting => setting.Key == key, cancellationToken);
    }

    public async Task<Dictionary<string, string>> GetPayrollSettingValuesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        return await context.PayrollSettings
            .AsNoTracking()
            .Where(setting => setting.IsActive)
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);
    }

    public async Task<PayrollSetting> SavePayrollSettingAsync(PayrollSetting setting, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(setting.Key))
        {
            throw new InvalidOperationException("Setting key is required.");
        }

        await using var context = _contextFactory();
        setting.Key = setting.Key.Trim();
        setting.Value = setting.Value?.Trim() ?? string.Empty;
        setting.Description = string.IsNullOrWhiteSpace(setting.Description) ? null : setting.Description.Trim();
        setting.EffectiveDate = setting.EffectiveDate == default ? DateTime.UtcNow : setting.EffectiveDate;

        if (setting.Id == 0)
        {
            var existing = await context.PayrollSettings.FirstOrDefaultAsync(item => item.Key == setting.Key, cancellationToken);
            if (existing is null)
            {
                context.PayrollSettings.Add(setting);
            }
            else
            {
                existing.Value = setting.Value;
                existing.Description = setting.Description ?? existing.Description;
                existing.EffectiveDate = setting.EffectiveDate;
                existing.IsActive = setting.IsActive;
            }
        }
        else
        {
            var existing = await context.PayrollSettings.FirstOrDefaultAsync(item => item.Id == setting.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Setting with id {setting.Id} was not found.");

            existing.Key = setting.Key;
            existing.Value = setting.Value;
            existing.Description = setting.Description;
            existing.EffectiveDate = setting.EffectiveDate;
            existing.IsActive = setting.IsActive;
        }

        await context.SaveChangesAsync(cancellationToken);
        return setting;
    }

    public async Task SavePayrollSettingsAsync(IDictionary<string, string> settings, CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var now = DateTime.UtcNow;

        foreach (var pair in settings)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            var key = pair.Key.Trim();
            var value = pair.Value?.Trim() ?? string.Empty;
            var existing = await context.PayrollSettings.FirstOrDefaultAsync(item => item.Key == key, cancellationToken);
            if (existing is null)
            {
                context.PayrollSettings.Add(new PayrollSetting
                {
                    Key = key,
                    Value = value,
                    Description = $"Application setting: {key}",
                    EffectiveDate = now,
                    IsActive = true
                });
            }
            else
            {
                existing.Value = value;
                existing.EffectiveDate = now;
                existing.IsActive = true;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
