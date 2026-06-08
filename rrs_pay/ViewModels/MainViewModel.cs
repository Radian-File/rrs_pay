using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using rrs_pay.Views.Pages;

namespace rrs_pay.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private NavigationItem? _selectedNavigationItem;
    private object? _currentPage;
    private string _currentPageTitle = string.Empty;
    private string _currentPageSubtitle = string.Empty;
    private string _statusText = $"RRS Pay MVP • {DateTime.Now:MMM dd, yyyy h:mm tt}";

    public MainViewModel()
    {
        NavigateCommand = new RelayCommand(parameter => NavigateTo(parameter as NavigationItem));

        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new("Dashboard", "Executive overview of payroll, attendance, and operational health."),
            new("Employees", "Manage personnel records, compensation details, and employment status."),
            new("Departments", "Maintain organization units and reporting structures."),
            new("Positions", "Define job titles, grades, and role assignments."),
            new("Attendance", "Review time logs, exceptions, and attendance summaries."),
            new("Payroll", "Prepare payroll runs, deductions, allowances, and approvals."),
            new("Reports", "Generate statutory, management, and audit-ready payroll reports."),
            new("Audit Logs", "Trace critical changes and security-sensitive activity."),
            new("Settings", "Configure company profile, pay periods, policies, and access preferences.")
        };

        NavigateTo(NavigationItems[0]);
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public ICommand NavigateCommand { get; }

    public object? CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string CurrentPageTitle
    {
        get => _currentPageTitle;
        private set => SetProperty(ref _currentPageTitle, value);
    }

    public string CurrentPageSubtitle
    {
        get => _currentPageSubtitle;
        private set => SetProperty(ref _currentPageSubtitle, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public void SetStatus(string status)
    {
        StatusText = status;
    }

    private void NavigateTo(NavigationItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (_selectedNavigationItem is not null)
        {
            _selectedNavigationItem.IsSelected = false;
        }

        _selectedNavigationItem = item;
        _selectedNavigationItem.IsSelected = true;

        CurrentPageTitle = item.Title;
        CurrentPageSubtitle = item.Description;
        CurrentPage = CreatePage(item.Title);
        StatusText = $"Ready • {item.Title} • {DateTime.Now:MMM dd, yyyy h:mm tt}";
    }

    private static object CreatePage(string title) => title switch
    {
        "Dashboard" => new DashboardView(),
        "Employees" => new EmployeesView(),
        "Departments" => new DepartmentsView(),
        "Positions" => new PositionsView(),
        "Attendance" => new AttendanceView(),
        "Payroll" => new PayrollView(),
        "Reports" => new ReportsView(),
        "Audit Logs" => new AuditLogsView(),
        "Settings" => new SettingsView(),
        _ => new PlaceholderPage(title, "This page is prepared for the RRS Pay MVP workspace.")
    };
}

public sealed class NavigationItem : ViewModelBase
{
    private bool _isSelected;

    public NavigationItem(string title, string description)
    {
        Title = title;
        Description = description;
    }

    public string Title { get; }

    public string Description { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
