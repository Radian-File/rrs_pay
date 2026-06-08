using System.Collections.ObjectModel;
using rrs_pay.ViewModels;

namespace rrs_pay.ViewModels.Pages;

public sealed class DepartmentsViewModel : ViewModelBase
{
    private string _departmentName = "Customer Support";
    private string _departmentCode = "CS";
    private string _manager = "Unassigned";
    private string _description = "Handles employee support and service requests.";

    public DepartmentsViewModel()
    {
        Departments = new ObservableCollection<DepartmentItem>
        {
            new("FIN", "Finance", "Alya Santoso", 18, "Payroll, reimbursements, and statutory reporting"),
            new("OPS", "Operations", "Bima Pratama", 64, "Daily operations and site attendance"),
            new("HR", "Human Resources", "Citra Dewi", 9, "Employee records, onboarding, and compliance")
        };
        SaveDepartmentCommand = new RelayCommand(SaveDepartment);
        ClearFormCommand = new RelayCommand(ClearForm);
    }

    public ObservableCollection<DepartmentItem> Departments { get; }
    public RelayCommand SaveDepartmentCommand { get; }
    public RelayCommand ClearFormCommand { get; }

    public string DepartmentName { get => _departmentName; set => SetProperty(ref _departmentName, value); }
    public string DepartmentCode { get => _departmentCode; set => SetProperty(ref _departmentCode, value); }
    public string Manager { get => _manager; set => SetProperty(ref _manager, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }

    private void SaveDepartment()
    {
        if (string.IsNullOrWhiteSpace(DepartmentName) || string.IsNullOrWhiteSpace(DepartmentCode))
        {
            return;
        }

        var existing = Departments.FirstOrDefault(item => item.Code.Equals(DepartmentCode, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            Departments.Add(new DepartmentItem(DepartmentCode.ToUpperInvariant(), DepartmentName, Manager, 0, Description));
        }
        else
        {
            existing.Name = DepartmentName;
            existing.Manager = Manager;
            existing.Description = Description;
        }
    }

    private void ClearForm()
    {
        DepartmentName = string.Empty;
        DepartmentCode = string.Empty;
        Manager = string.Empty;
        Description = string.Empty;
    }
}

public sealed class DepartmentItem : ViewModelBase
{
    private string _code;
    private string _name;
    private string _manager;
    private int _employeeCount;
    private string _description;

    public DepartmentItem(string code, string name, string manager, int employeeCount, string description)
    {
        _code = code;
        _name = name;
        _manager = manager;
        _employeeCount = employeeCount;
        _description = description;
    }

    public string Code { get => _code; set => SetProperty(ref _code, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Manager { get => _manager; set => SetProperty(ref _manager, value); }
    public int EmployeeCount { get => _employeeCount; set => SetProperty(ref _employeeCount, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }
}
