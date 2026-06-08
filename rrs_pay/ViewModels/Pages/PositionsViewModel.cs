using System.Collections.ObjectModel;
using rrs_pay.ViewModels;

namespace rrs_pay.ViewModels.Pages;

public sealed class PositionsViewModel : ViewModelBase
{
    private string _positionTitle = "Payroll Analyst";
    private string _department = "Finance";
    private decimal _salaryGradeMin = 8500000m;
    private decimal _salaryGradeMax = 14500000m;
    private string _description = "Prepares payroll calculations and reconciliations.";

    public PositionsViewModel()
    {
        Departments = new ObservableCollection<string> { "Finance", "Operations", "Human Resources", "IT", "Sales" };
        Positions = new ObservableCollection<PositionItem>
        {
            new("Payroll Specialist", "Finance", 10000000m, 16000000m, 6),
            new("Shift Supervisor", "Operations", 8500000m, 13500000m, 12),
            new("HR Officer", "Human Resources", 8000000m, 12500000m, 4)
        };
        SavePositionCommand = new RelayCommand(SavePosition);
        ClearFormCommand = new RelayCommand(ClearForm);
    }

    public ObservableCollection<string> Departments { get; }
    public ObservableCollection<PositionItem> Positions { get; }
    public RelayCommand SavePositionCommand { get; }
    public RelayCommand ClearFormCommand { get; }

    public string PositionTitle { get => _positionTitle; set => SetProperty(ref _positionTitle, value); }
    public string Department { get => _department; set => SetProperty(ref _department, value); }
    public decimal SalaryGradeMin { get => _salaryGradeMin; set => SetProperty(ref _salaryGradeMin, value); }
    public decimal SalaryGradeMax { get => _salaryGradeMax; set => SetProperty(ref _salaryGradeMax, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }

    private void SavePosition()
    {
        if (string.IsNullOrWhiteSpace(PositionTitle))
        {
            return;
        }

        var existing = Positions.FirstOrDefault(item => item.Title.Equals(PositionTitle, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            Positions.Add(new PositionItem(PositionTitle, Department, SalaryGradeMin, SalaryGradeMax, 0));
        }
        else
        {
            existing.Department = Department;
            existing.SalaryGradeMin = SalaryGradeMin;
            existing.SalaryGradeMax = SalaryGradeMax;
        }
    }

    private void ClearForm()
    {
        PositionTitle = string.Empty;
        Department = Departments.FirstOrDefault() ?? string.Empty;
        SalaryGradeMin = 0;
        SalaryGradeMax = 0;
        Description = string.Empty;
    }
}

public sealed class PositionItem : ViewModelBase
{
    private string _title;
    private string _department;
    private decimal _salaryGradeMin;
    private decimal _salaryGradeMax;
    private int _employeeCount;

    public PositionItem(string title, string department, decimal salaryGradeMin, decimal salaryGradeMax, int employeeCount)
    {
        _title = title;
        _department = department;
        _salaryGradeMin = salaryGradeMin;
        _salaryGradeMax = salaryGradeMax;
        _employeeCount = employeeCount;
    }

    public string Title { get => _title; set => SetProperty(ref _title, value); }
    public string Department { get => _department; set => SetProperty(ref _department, value); }
    public decimal SalaryGradeMin { get => _salaryGradeMin; set => SetProperty(ref _salaryGradeMin, value); }
    public decimal SalaryGradeMax { get => _salaryGradeMax; set => SetProperty(ref _salaryGradeMax, value); }
    public int EmployeeCount { get => _employeeCount; set => SetProperty(ref _employeeCount, value); }
}
