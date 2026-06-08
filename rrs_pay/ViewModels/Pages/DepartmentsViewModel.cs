using System.Collections.ObjectModel;
using rrs_pay.Models;
using rrs_pay.Services;
using rrs_pay.ViewModels;
using rrs_pay.ViewModels.Infrastructure;

namespace rrs_pay.ViewModels.Pages;

public sealed class DepartmentsViewModel : ViewModelBase
{
    private readonly DataService _dataService;
    private readonly AuditLogService _auditLogService;

    private DepartmentItem? _selectedDepartment;
    private string _departmentName = string.Empty;
    private string _departmentCode = string.Empty;
    private string _manager = "Unassigned";
    private string _description = string.Empty;
    private string _statusMessage = "Loading departments from database...";
    private bool _isBusy;

    public DepartmentsViewModel() : this(new DataService(), new AuditLogService())
    {
    }

    public DepartmentsViewModel(DataService dataService, AuditLogService auditLogService)
    {
        _dataService = dataService;
        _auditLogService = auditLogService;
        Departments = new ObservableCollection<DepartmentItem>();
        SaveDepartmentCommand = new AsyncRelayCommand(SaveDepartmentAsync, () => !IsBusy);
        ClearFormCommand = new RelayCommand(ClearForm);
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        _ = LoadAsync();
    }

    public ObservableCollection<DepartmentItem> Departments { get; }
    public AsyncRelayCommand SaveDepartmentCommand { get; }
    public RelayCommand ClearFormCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    public DepartmentItem? SelectedDepartment
    {
        get => _selectedDepartment;
        set
        {
            if (SetProperty(ref _selectedDepartment, value) && value is not null)
            {
                DepartmentCode = value.Code;
                DepartmentName = value.Name;
                Manager = value.Manager;
                Description = value.Description;
                StatusMessage = $"Editing department {value.Code}.";
            }
        }
    }

    public string DepartmentName { get => _departmentName; set => SetProperty(ref _departmentName, value); }
    public string DepartmentCode { get => _departmentCode; set => SetProperty(ref _departmentCode, value); }
    public string Manager { get => _manager; set => SetProperty(ref _manager, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SaveDepartmentCommand.RaiseCanExecuteChanged();
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
            var departments = await _dataService.GetDepartmentsWithEmployeesAsync(activeOnly: true);
            foreach (var department in departments)
            {
                Departments.Add(DepartmentItem.FromModel(department));
            }

            StatusMessage = $"Loaded {Departments.Count} departments from database.";
            if (Departments.Count == 0)
            {
                ClearForm();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load departments: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveDepartmentAsync()
    {
        if (string.IsNullOrWhiteSpace(DepartmentName) || string.IsNullOrWhiteSpace(DepartmentCode))
        {
            StatusMessage = "Department code and name are required.";
            return;
        }

        try
        {
            IsBusy = true;
            var selectedId = SelectedDepartment?.Id ?? 0;
            var existingByCode = selectedId == 0
                ? Departments.FirstOrDefault(item => item.Code.Equals(DepartmentCode.Trim(), StringComparison.OrdinalIgnoreCase))
                : null;

            var department = new Department
            {
                Id = selectedId != 0 ? selectedId : existingByCode?.Id ?? 0,
                Code = DepartmentCode,
                Name = DepartmentName,
                Description = Description,
                IsActive = true
            };

            var isNew = department.Id == 0;
            var saved = await _dataService.SaveDepartmentAsync(department);
            await _auditLogService.LogAsync(
                isNew ? "Create Department" : "Update Department",
                nameof(Department),
                saved.Id.ToString(),
                $"{saved.Code} - {saved.Name}");

            StatusMessage = $"Department {saved.Code} saved to database.";
            await LoadAsync();
            SelectedDepartment = Departments.FirstOrDefault(item => item.Id == saved.Id);
            ClearForm();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save department: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearForm()
    {
        SelectedDepartment = null;
        DepartmentName = string.Empty;
        DepartmentCode = string.Empty;
        Manager = "Unassigned";
        Description = string.Empty;
        StatusMessage = Departments.Count == 0 ? "No departments found. Create the first department." : "Ready.";
    }
}

public sealed class DepartmentItem : ViewModelBase
{
    private string _code;
    private string _name;
    private string _manager;
    private int _employeeCount;
    private string _description;

    public DepartmentItem(int id, string code, string name, string manager, int employeeCount, string description)
    {
        Id = id;
        _code = code;
        _name = name;
        _manager = manager;
        _employeeCount = employeeCount;
        _description = description;
    }

    public int Id { get; }
    public string Code { get => _code; set => SetProperty(ref _code, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string Manager { get => _manager; set => SetProperty(ref _manager, value); }
    public int EmployeeCount { get => _employeeCount; set => SetProperty(ref _employeeCount, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }

    public static DepartmentItem FromModel(Department department) => new(
        department.Id,
        department.Code,
        department.Name,
        "Unassigned",
        department.Employees.Count(employee => employee.IsActive),
        department.Description ?? string.Empty);
}
