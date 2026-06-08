using System.Collections.ObjectModel;
using rrs_pay.ViewModels;

namespace rrs_pay.ViewModels.Pages;

public sealed class EmployeesViewModel : ViewModelBase
{
    private string _searchText = string.Empty;
    private EmployeeListItem? _selectedEmployee;
    private string _employeeCode = "EMP-0004";
    private string _fullName = "";
    private string _department = "Operations";
    private string _position = "Staff";
    private string _employmentStatus = "Active";
    private decimal _basicSalary = 7500000m;

    public EmployeesViewModel()
    {
        Employees = new ObservableCollection<EmployeeListItem>
        {
            new("EMP-0001", "Alya Santoso", "Finance", "Payroll Specialist", "Active", 12500000m, "Bank Mandiri"),
            new("EMP-0002", "Bima Pratama", "Operations", "Shift Supervisor", "Active", 9800000m, "BCA"),
            new("EMP-0003", "Citra Dewi", "Human Resources", "HR Officer", "On Leave", 10250000m, "BNI")
        };

        FilteredEmployees = new ObservableCollection<EmployeeListItem>(Employees);
        Departments = new ObservableCollection<string> { "Finance", "Operations", "Human Resources", "IT", "Sales" };
        Positions = new ObservableCollection<string> { "Staff", "Shift Supervisor", "HR Officer", "Payroll Specialist", "Manager" };
        EmploymentStatuses = new ObservableCollection<string> { "Active", "On Leave", "Inactive" };
        SaveEmployeeCommand = new RelayCommand(SaveEmployee);
        ClearFormCommand = new RelayCommand(ClearForm);
    }

    public ObservableCollection<EmployeeListItem> Employees { get; }
    public ObservableCollection<EmployeeListItem> FilteredEmployees { get; }
    public ObservableCollection<string> Departments { get; }
    public ObservableCollection<string> Positions { get; }
    public ObservableCollection<string> EmploymentStatuses { get; }
    public RelayCommand SaveEmployeeCommand { get; }
    public RelayCommand ClearFormCommand { get; }

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
        set => SetProperty(ref _department, value);
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

    private void SaveEmployee()
    {
        if (string.IsNullOrWhiteSpace(FullName))
        {
            return;
        }

        if (SelectedEmployee is null || !Employees.Contains(SelectedEmployee))
        {
            var newEmployee = new EmployeeListItem(EmployeeCode, FullName, Department, Position, EmploymentStatus, BasicSalary, "Pending setup");
            Employees.Add(newEmployee);
            SelectedEmployee = newEmployee;
        }
        else
        {
            SelectedEmployee.EmployeeCode = EmployeeCode;
            SelectedEmployee.FullName = FullName;
            SelectedEmployee.Department = Department;
            SelectedEmployee.Position = Position;
            SelectedEmployee.Status = EmploymentStatus;
            SelectedEmployee.BasicSalary = BasicSalary;
        }

        ApplyFilter();
    }

    private void ClearForm()
    {
        SelectedEmployee = null;
        EmployeeCode = $"EMP-{Employees.Count + 1:0000}";
        FullName = string.Empty;
        Department = Departments.FirstOrDefault() ?? string.Empty;
        Position = Positions.FirstOrDefault() ?? string.Empty;
        EmploymentStatus = "Active";
        BasicSalary = 7500000m;
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        FilteredEmployees.Clear();
        foreach (var employee in Employees.Where(employee => string.IsNullOrWhiteSpace(query)
                     || employee.EmployeeCode.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || employee.FullName.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || employee.Department.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || employee.Position.Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            FilteredEmployees.Add(employee);
        }
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

    public EmployeeListItem(string employeeCode, string fullName, string department, string position, string status, decimal basicSalary, string bankName)
    {
        _employeeCode = employeeCode;
        _fullName = fullName;
        _department = department;
        _position = position;
        _status = status;
        _basicSalary = basicSalary;
        _bankName = bankName;
    }

    public string EmployeeCode { get => _employeeCode; set => SetProperty(ref _employeeCode, value); }
    public string FullName { get => _fullName; set => SetProperty(ref _fullName, value); }
    public string Department { get => _department; set => SetProperty(ref _department, value); }
    public string Position { get => _position; set => SetProperty(ref _position, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public decimal BasicSalary { get => _basicSalary; set => SetProperty(ref _basicSalary, value); }
    public string BankName { get => _bankName; set => SetProperty(ref _bankName, value); }
}
