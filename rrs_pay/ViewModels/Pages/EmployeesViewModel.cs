using System.Collections.ObjectModel;
using rrs_pay.Models;
using rrs_pay.Services;
using rrs_pay.ViewModels;
using rrs_pay.ViewModels.Infrastructure;

namespace rrs_pay.ViewModels.Pages;

public sealed class EmployeesViewModel : ViewModelBase
{
    private readonly DataService _dataService;
    private readonly AuditLogService _auditLogService;
    private readonly Dictionary<string, int> _departmentIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _positionIdsByDepartmentTitle = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Position> _allPositions = new();

    private string _searchText = string.Empty;
    private EmployeeListItem? _selectedEmployee;
    private string _employeeCode = string.Empty;
    private string _fullName = string.Empty;
    private string _department = string.Empty;
    private string _position = string.Empty;
    private string _employmentStatus = rrs_pay.Models.EmploymentStatus.Active.ToString();
    private decimal _basicSalary;
    private string _statusMessage = "Loading employees from database...";
    private bool _isBusy;

    public EmployeesViewModel() : this(new DataService(), new AuditLogService())
    {
    }

    public EmployeesViewModel(DataService dataService, AuditLogService auditLogService)
    {
        _dataService = dataService;
        _auditLogService = auditLogService;
        Employees = new ObservableCollection<EmployeeListItem>();
        FilteredEmployees = new ObservableCollection<EmployeeListItem>();
        Departments = new ObservableCollection<string>();
        Positions = new ObservableCollection<string>();
        EmploymentStatuses = new ObservableCollection<string>(Enum.GetNames<rrs_pay.Models.EmploymentStatus>());
        SaveEmployeeCommand = new AsyncRelayCommand(SaveEmployeeAsync, () => !IsBusy);
        ClearFormCommand = new RelayCommand(ClearForm);
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        _ = LoadAsync();
    }

    public ObservableCollection<EmployeeListItem> Employees { get; }
    public ObservableCollection<EmployeeListItem> FilteredEmployees { get; }
    public ObservableCollection<string> Departments { get; }
    public ObservableCollection<string> Positions { get; }
    public ObservableCollection<string> EmploymentStatuses { get; }
    public AsyncRelayCommand SaveEmployeeCommand { get; }
    public RelayCommand ClearFormCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public EmployeeListItem? SelectedEmployee
    {
        get => _selectedEmployee;
        set
        {
            if (SetProperty(ref _selectedEmployee, value) && value is not null)
            {
                EmployeeCode = value.EmployeeCode;
                FullName = value.FullName;
                Department = value.Department;
                Position = value.Position;
                EmploymentStatus = value.Status;
                BasicSalary = value.BasicSalary;
                StatusMessage = $"Editing employee {value.EmployeeCode}.";
            }
        }
    }

    public string EmployeeCode
    {
        get => _employeeCode;
        set => SetProperty(ref _employeeCode, value);
    }

    public string FullName
    {
        get => _fullName;
        set => SetProperty(ref _fullName, value);
    }

    public string Department
    {
        get => _department;
        set
        {
            if (SetProperty(ref _department, value))
            {
                UpdatePositionsForDepartment(value);
            }
        }
    }

    public string Position
    {
        get => _position;
        set => SetProperty(ref _position, value);
    }

    public string EmploymentStatus
    {
        get => _employmentStatus;
        set => SetProperty(ref _employmentStatus, value);
    }

    public decimal BasicSalary
    {
        get => _basicSalary;
        set => SetProperty(ref _basicSalary, value);
    }

    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SaveEmployeeCommand.RaiseCanExecuteChanged();
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            Departments.Clear();
            Positions.Clear();
            Employees.Clear();
            FilteredEmployees.Clear();
            _departmentIds.Clear();
            _positionIdsByDepartmentTitle.Clear();
            _allPositions.Clear();

            var departments = await _dataService.GetDepartmentsAsync(activeOnly: true);
            foreach (var department in departments)
            {
                Departments.Add(department.Name);
                _departmentIds[department.Name] = department.Id;
            }

            var positions = await _dataService.GetPositionsAsync(activeOnly: true);
            _allPositions.AddRange(positions);

            var employees = await _dataService.GetEmployeesAsync(activeOnly: true);
            foreach (var employee in employees)
            {
                Employees.Add(EmployeeListItem.FromModel(employee));
            }

            Department = Departments.FirstOrDefault() ?? string.Empty;
            UpdatePositionsForDepartment(Department);
            EmploymentStatus = rrs_pay.Models.EmploymentStatus.Active.ToString();
            EmployeeCode = GenerateNextEmployeeCode();
            ApplyFilter();
            StatusMessage = $"Loaded {Employees.Count} employees from database.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load employees: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveEmployeeAsync()
    {
        if (string.IsNullOrWhiteSpace(EmployeeCode) || string.IsNullOrWhiteSpace(FullName))
        {
            StatusMessage = "Employee code and full name are required.";
            return;
        }

        if (!_departmentIds.TryGetValue(Department, out var departmentId))
        {
            StatusMessage = "Select a valid department.";
            return;
        }

        if (!_positionIdsByDepartmentTitle.TryGetValue(PositionKey(Department, Position), out var positionId))
        {
            StatusMessage = "Select a valid position for the selected department.";
            return;
        }

        if (BasicSalary < 0)
        {
            StatusMessage = "Basic salary cannot be negative.";
            return;
        }

        if (!Enum.TryParse<rrs_pay.Models.EmploymentStatus>(EmploymentStatus, ignoreCase: true, out var parsedStatus))
        {
            parsedStatus = rrs_pay.Models.EmploymentStatus.Active;
        }

        try
        {
            IsBusy = true;
            var selectedId = SelectedEmployee?.Id ?? 0;
            var existingByCode = selectedId == 0
                ? Employees.FirstOrDefault(item => item.EmployeeCode.Equals(EmployeeCode.Trim(), StringComparison.OrdinalIgnoreCase))
                : null;
            var (firstName, lastName) = SplitFullName(FullName);
            var employee = new Employee
            {
                Id = selectedId != 0 ? selectedId : existingByCode?.Id ?? 0,
                EmployeeNumber = EmployeeCode,
                FirstName = firstName,
                LastName = lastName,
                HireDate = SelectedEmployee?.HireDate ?? DateTime.Today,
                Status = parsedStatus,
                IsActive = parsedStatus != rrs_pay.Models.EmploymentStatus.Terminated,
                BasicSalary = BasicSalary,
                DefaultAllowance = SelectedEmployee?.DefaultAllowance ?? 0m,
                DefaultTaxRate = SelectedEmployee?.DefaultTaxRate ?? 0.10m,
                BankName = SelectedEmployee?.BankName == "Pending setup" ? null : SelectedEmployee?.BankName,
                DepartmentId = departmentId,
                PositionId = positionId
            };

            var isNew = employee.Id == 0;
            var saved = await _dataService.SaveEmployeeAsync(employee);
            await _auditLogService.LogAsync(
                isNew ? "Create Employee" : "Update Employee",
                nameof(Employee),
                saved.Id.ToString(),
                $"{saved.EmployeeNumber} - {saved.FullName}");

            StatusMessage = $"Employee {saved.EmployeeNumber} saved to database.";
            await LoadAsync();
            SelectedEmployee = Employees.FirstOrDefault(item => item.Id == saved.Id);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save employee: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearForm()
    {
        SelectedEmployee = null;
        EmployeeCode = GenerateNextEmployeeCode();
        FullName = string.Empty;
        Department = Departments.FirstOrDefault() ?? string.Empty;
        Position = Positions.FirstOrDefault() ?? string.Empty;
        EmploymentStatus = rrs_pay.Models.EmploymentStatus.Active.ToString();
        BasicSalary = 0m;
        StatusMessage = Departments.Count == 0 ? "Create departments and positions before adding employees." : "Ready.";
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        FilteredEmployees.Clear();
        foreach (var employee in Employees.Where(employee => string.IsNullOrWhiteSpace(query)
                     || employee.EmployeeCode.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || employee.FullName.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || employee.Department.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || employee.Position.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || employee.Status.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            FilteredEmployees.Add(employee);
        }
    }

    private void UpdatePositionsForDepartment(string department)
    {
        Positions.Clear();
        foreach (var position in _allPositions.Where(item => item.Department.Name.Equals(department ?? string.Empty, StringComparison.OrdinalIgnoreCase)).OrderBy(item => item.Title))
        {
            Positions.Add(position.Title);
            _positionIdsByDepartmentTitle[PositionKey(position.Department.Name, position.Title)] = position.Id;
        }

        if (!Positions.Contains(Position))
        {
            Position = Positions.FirstOrDefault() ?? string.Empty;
        }
    }

    private string GenerateNextEmployeeCode()
    {
        var maxNumber = Employees
            .Select(item => item.EmployeeCode)
            .Select(code => code.StartsWith("EMP-", StringComparison.OrdinalIgnoreCase) && int.TryParse(code[4..], out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"EMP-{maxNumber + 1:0000}";
    }

    private static string PositionKey(string department, string position) => $"{department}|{position}";

    private static (string FirstName, string LastName) SplitFullName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (string.Empty, string.Empty),
            1 => (parts[0], parts[0]),
            _ => (parts[0], parts[1])
        };
    }
}

public sealed class EmployeeListItem : ViewModelBase
{
    private string _employeeCode;
    private string _fullName;
    private string _department;
    private string _position;
    private string _status;
    private decimal _basicSalary;
    private string _bankName;

    public EmployeeListItem(
        int id,
        string employeeCode,
        string fullName,
        string department,
        string position,
        string status,
        decimal basicSalary,
        string bankName,
        DateTime hireDate,
        decimal defaultAllowance,
        decimal defaultTaxRate)
    {
        Id = id;
        _employeeCode = employeeCode;
        _fullName = fullName;
        _department = department;
        _position = position;
        _status = status;
        _basicSalary = basicSalary;
        _bankName = bankName;
        HireDate = hireDate;
        DefaultAllowance = defaultAllowance;
        DefaultTaxRate = defaultTaxRate;
    }

    public int Id { get; }
    public DateTime HireDate { get; }
    public decimal DefaultAllowance { get; }
    public decimal DefaultTaxRate { get; }
    public string EmployeeCode { get => _employeeCode; set => SetProperty(ref _employeeCode, value); }
    public string FullName { get => _fullName; set => SetProperty(ref _fullName, value); }
    public string Department { get => _department; set => SetProperty(ref _department, value); }
    public string Position { get => _position; set => SetProperty(ref _position, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public decimal BasicSalary { get => _basicSalary; set => SetProperty(ref _basicSalary, value); }
    public string BankName { get => _bankName; set => SetProperty(ref _bankName, value); }

    public static EmployeeListItem FromModel(Employee employee) => new(
        employee.Id,
        employee.EmployeeNumber,
        employee.FullName,
        employee.Department.Name,
        employee.Position.Title,
        employee.Status.ToString(),
        employee.BasicSalary,
        employee.BankName ?? "Pending setup",
        employee.HireDate,
        employee.DefaultAllowance,
        employee.DefaultTaxRate);
}
