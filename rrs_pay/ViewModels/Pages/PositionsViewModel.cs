using System.Collections.ObjectModel;
using rrs_pay.Models;
using rrs_pay.Services;
using rrs_pay.ViewModels;
using rrs_pay.ViewModels.Infrastructure;

namespace rrs_pay.ViewModels.Pages;

public sealed class PositionsViewModel : ViewModelBase
{
    private readonly DataService _dataService;
    private readonly AuditLogService _auditLogService;
    private readonly Dictionary<string, int> _departmentIds = new(StringComparer.OrdinalIgnoreCase);

    private PositionItem? _selectedPosition;
    private string _positionTitle = string.Empty;
    private string _department = string.Empty;
    private decimal _salaryGradeMin;
    private decimal _salaryGradeMax;
    private string _description = string.Empty;
    private string _statusMessage = "Loading positions from database...";
    private bool _isBusy;

    public PositionsViewModel() : this(new DataService(), new AuditLogService())
    {
    }

    public PositionsViewModel(DataService dataService, AuditLogService auditLogService)
    {
        _dataService = dataService;
        _auditLogService = auditLogService;
        Departments = new ObservableCollection<string>();
        Positions = new ObservableCollection<PositionItem>();
        SavePositionCommand = new AsyncRelayCommand(SavePositionAsync, () => !IsBusy);
        ClearFormCommand = new RelayCommand(ClearForm);
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        _ = LoadAsync();
    }

    public ObservableCollection<string> Departments { get; }
    public ObservableCollection<PositionItem> Positions { get; }
    public AsyncRelayCommand SavePositionCommand { get; }
    public RelayCommand ClearFormCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    public PositionItem? SelectedPosition
    {
        get => _selectedPosition;
        set
        {
            if (SetProperty(ref _selectedPosition, value) && value is not null)
            {
                PositionTitle = value.Title;
                Department = value.Department;
                SalaryGradeMin = value.SalaryGradeMin;
                SalaryGradeMax = value.SalaryGradeMax;
                Description = value.Description;
                StatusMessage = $"Editing position {value.Title}.";
            }
        }
    }

    public string PositionTitle { get => _positionTitle; set => SetProperty(ref _positionTitle, value); }
    public string Department { get => _department; set => SetProperty(ref _department, value); }
    public decimal SalaryGradeMin { get => _salaryGradeMin; set => SetProperty(ref _salaryGradeMin, value); }
    public decimal SalaryGradeMax { get => _salaryGradeMax; set => SetProperty(ref _salaryGradeMax, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SavePositionCommand.RaiseCanExecuteChanged();
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
            _departmentIds.Clear();

            var departments = await _dataService.GetDepartmentsAsync(activeOnly: true);
            foreach (var department in departments)
            {
                Departments.Add(department.Name);
                _departmentIds[department.Name] = department.Id;
            }

            var positions = await _dataService.GetPositionsAsync(activeOnly: true);
            foreach (var position in positions)
            {
                Positions.Add(PositionItem.FromModel(position));
            }

            Department = Departments.FirstOrDefault() ?? string.Empty;
            StatusMessage = $"Loaded {Positions.Count} positions from database.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load positions: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SavePositionAsync()
    {
        if (string.IsNullOrWhiteSpace(PositionTitle))
        {
            StatusMessage = "Position title is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Department) || !_departmentIds.TryGetValue(Department, out var departmentId))
        {
            StatusMessage = "Select a valid department.";
            return;
        }

        if (SalaryGradeMin < 0 || SalaryGradeMax < 0 || (SalaryGradeMax > 0 && SalaryGradeMax < SalaryGradeMin))
        {
            StatusMessage = "Salary grade values must be valid and max must be greater than min.";
            return;
        }

        try
        {
            IsBusy = true;
            var selectedId = SelectedPosition?.Id ?? 0;
            var existingByTitle = selectedId == 0
                ? Positions.FirstOrDefault(item => item.Title.Equals(PositionTitle.Trim(), StringComparison.OrdinalIgnoreCase) && item.Department.Equals(Department, StringComparison.OrdinalIgnoreCase))
                : null;
            var code = selectedId != 0 ? SelectedPosition?.Code ?? GenerateCode(Department, PositionTitle) : existingByTitle?.Code ?? GenerateCode(Department, PositionTitle);
            var position = new Position
            {
                Id = selectedId != 0 ? selectedId : existingByTitle?.Id ?? 0,
                Code = code,
                Title = PositionTitle,
                DepartmentId = departmentId,
                DefaultBasicSalary = SalaryGradeMin,
                Description = Description,
                IsActive = true
            };

            var isNew = position.Id == 0;
            var saved = await _dataService.SavePositionAsync(position);
            await _auditLogService.LogAsync(
                isNew ? "Create Position" : "Update Position",
                nameof(Position),
                saved.Id.ToString(),
                $"{saved.Code} - {saved.Title}");

            StatusMessage = $"Position {saved.Title} saved to database.";
            await LoadAsync();
            SelectedPosition = Positions.FirstOrDefault(item => item.Id == saved.Id);
            ClearForm();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save position: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearForm()
    {
        SelectedPosition = null;
        PositionTitle = string.Empty;
        Department = Departments.FirstOrDefault() ?? string.Empty;
        SalaryGradeMin = 0;
        SalaryGradeMax = 0;
        Description = string.Empty;
        StatusMessage = Departments.Count == 0 ? "Create a department before adding positions." : "Ready.";
    }

    private static string GenerateCode(string department, string title)
    {
        var departmentPart = new string((department ?? string.Empty).Where(char.IsLetterOrDigit).Take(3).ToArray()).ToUpperInvariant();
        var titlePart = new string((title ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word => char.ToUpperInvariant(word[0])).Take(4).ToArray());
        if (string.IsNullOrWhiteSpace(departmentPart))
        {
            departmentPart = "POS";
        }

        if (string.IsNullOrWhiteSpace(titlePart))
        {
            titlePart = "NEW";
        }

        return $"{departmentPart}-{titlePart}";
    }
}

public sealed class PositionItem : ViewModelBase
{
    private string _title;
    private string _department;
    private decimal _salaryGradeMin;
    private decimal _salaryGradeMax;
    private int _employeeCount;
    private string _description;

    public PositionItem(int id, string code, string title, string department, decimal salaryGradeMin, decimal salaryGradeMax, int employeeCount, string description)
    {
        Id = id;
        Code = code;
        _title = title;
        _department = department;
        _salaryGradeMin = salaryGradeMin;
        _salaryGradeMax = salaryGradeMax;
        _employeeCount = employeeCount;
        _description = description;
    }

    public int Id { get; }
    public string Code { get; }
    public string Title { get => _title; set => SetProperty(ref _title, value); }
    public string Department { get => _department; set => SetProperty(ref _department, value); }
    public decimal SalaryGradeMin { get => _salaryGradeMin; set => SetProperty(ref _salaryGradeMin, value); }
    public decimal SalaryGradeMax { get => _salaryGradeMax; set => SetProperty(ref _salaryGradeMax, value); }
    public int EmployeeCount { get => _employeeCount; set => SetProperty(ref _employeeCount, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }

    public static PositionItem FromModel(Position position) => new(
        position.Id,
        position.Code,
        position.Title,
        position.Department.Name,
        position.DefaultBasicSalary,
        position.DefaultBasicSalary,
        position.Employees.Count(employee => employee.IsActive),
        position.Description ?? string.Empty);
}
