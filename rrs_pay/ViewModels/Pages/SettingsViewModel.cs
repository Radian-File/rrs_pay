using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using rrs_pay.Models;
using rrs_pay.Services;
using rrs_pay.ViewModels;
using rrs_pay.ViewModels.Infrastructure;

namespace rrs_pay.ViewModels.Pages;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly DataService _dataService;
    private readonly AuditLogService _auditLogService;
    private readonly BackupRestoreService _backupRestoreService;

    private string _companyName = "RRS Pay Demo Company";
    private string _payrollCutoffDay = "25";
    private string _defaultCurrency = "USD";
    private bool _requireApprovalBeforePayslip = true;
    private bool _autoCalculateLateDeductions = true;
    private string _statusMessage = "Loading settings from database...";
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public SettingsViewModel() : this(new DataService(), new AuditLogService(), new BackupRestoreService())
    {
    }

    public SettingsViewModel(DataService dataService, AuditLogService auditLogService, BackupRestoreService backupRestoreService)
    {
        _dataService = dataService;
        _auditLogService = auditLogService;
        _backupRestoreService = backupRestoreService;
        PayrollRules = new ObservableCollection<PayrollRule>();
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, () => !IsBusy);
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        BackupDatabaseCommand = new AsyncRelayCommand(BackupDatabaseAsync, () => !IsBusy);
        RestoreDatabaseCommand = new AsyncRelayCommand(RestoreDatabaseAsync, () => !IsBusy);
        _ = LoadAsync();
    }

    public string CompanyName { get => _companyName; set => SetProperty(ref _companyName, value); }
    public string PayrollCutoffDay { get => _payrollCutoffDay; set => SetProperty(ref _payrollCutoffDay, value); }
    public string DefaultCurrency { get => _defaultCurrency; set => SetProperty(ref _defaultCurrency, value); }
    public bool RequireApprovalBeforePayslip { get => _requireApprovalBeforePayslip; set => SetProperty(ref _requireApprovalBeforePayslip, value); }
    public bool AutoCalculateLateDeductions { get => _autoCalculateLateDeductions; set => SetProperty(ref _autoCalculateLateDeductions, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
    public ObservableCollection<PayrollRule> PayrollRules { get; }
    public AsyncRelayCommand SaveSettingsCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand BackupDatabaseCommand { get; }
    public AsyncRelayCommand RestoreDatabaseCommand { get; }
    public ICommand BackupCommand => BackupDatabaseCommand;
    public ICommand RestoreCommand => RestoreDatabaseCommand;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SaveSettingsCommand.RaiseCanExecuteChanged();
                RefreshCommand.RaiseCanExecuteChanged();
                BackupDatabaseCommand.RaiseCanExecuteChanged();
                RestoreDatabaseCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            var settings = await _dataService.GetPayrollSettingValuesAsync();
            CompanyName = Get(settings, "CompanyName", CompanyName);
            PayrollCutoffDay = Get(settings, "PayrollCutoffDay", PayrollCutoffDay);
            DefaultCurrency = Get(settings, "Currency", DefaultCurrency);
            RequireApprovalBeforePayslip = GetBool(settings, "RequireApprovalBeforePayslip", RequireApprovalBeforePayslip);
            AutoCalculateLateDeductions = GetBool(settings, "AutoCalculateLateDeductions", AutoCalculateLateDeductions);
            RebuildRules(settings);
            StatusMessage = "Settings loaded from database.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Failed to load settings.";
            RebuildRules(new Dictionary<string, string>());
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveSettingsAsync()
    {
        if (!int.TryParse(PayrollCutoffDay, out var cutoffDay) || cutoffDay < 1 || cutoffDay > 31)
        {
            StatusMessage = "Payroll cutoff day must be a number from 1 to 31.";
            return;
        }

        if (string.IsNullOrWhiteSpace(CompanyName))
        {
            StatusMessage = "Company name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(DefaultCurrency))
        {
            StatusMessage = "Default currency is required.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            var settings = new Dictionary<string, string>
            {
                ["CompanyName"] = CompanyName.Trim(),
                ["PayrollCutoffDay"] = cutoffDay.ToString(),
                ["Currency"] = DefaultCurrency.Trim().ToUpperInvariant(),
                ["RequireApprovalBeforePayslip"] = RequireApprovalBeforePayslip.ToString(),
                ["AutoCalculateLateDeductions"] = AutoCalculateLateDeductions.ToString()
            };

            await _dataService.SavePayrollSettingsAsync(settings);
            await _auditLogService.LogAsync("Update Settings", nameof(PayrollSetting), "Application", "Company and payroll settings updated.");
            StatusMessage = "Settings saved to database.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Failed to save settings.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task BackupDatabaseAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            StatusMessage = "Creating database backup...";

            var result = await _backupRestoreService.BackupAsync();
            StatusMessage = $"{result.Message} {result.BackupPath}";
            MessageBox.Show(
                $"Database backup created successfully.\n\n{result.BackupPath}",
                "Backup completed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Failed to create database backup.";
            MessageBox.Show($"RRS Pay could not create a database backup.\n\n{ex.Message}", "Backup failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RestoreDatabaseAsync()
    {
        Directory.CreateDirectory(BackupRestoreService.BackupsFolder);

        var dialog = new OpenFileDialog
        {
            Title = "Select RRS Pay backup database",
            InitialDirectory = BackupRestoreService.BackupsFolder,
            Filter = "SQLite database backups (*.db)|*.db|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            StatusMessage = "Restore cancelled.";
            return;
        }

        var confirmation = MessageBox.Show(
            "Restoring replaces the current local RRS Pay database. A safety backup of the current database will be created first.\n\nContinue?",
            "Confirm database restore",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            StatusMessage = "Restore cancelled.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            StatusMessage = "Restoring database backup...";

            var result = await _backupRestoreService.RestoreAsync(dialog.FileName);
            StatusMessage = result.Message;
            MessageBox.Show(
                $"Database restore completed.\n\nRestored from: {result.RestoredFromPath}\nSafety backup: {result.SafetyBackupPath ?? "not needed"}\n\nRestart RRS Pay if any page still shows old data.",
                "Restore completed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Failed to restore database backup.";
            MessageBox.Show($"RRS Pay could not restore the selected backup.\n\n{ex.Message}", "Restore failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildRules(IReadOnlyDictionary<string, string> settings)
    {
        PayrollRules.Clear();
        PayrollRules.Add(new PayrollRule("Overtime multiplier", $"{Get(settings, "OvertimeMultiplier", "1.5")}x regular hourly rate"));
        PayrollRules.Add(new PayrollRule("Standard workday", $"{Get(settings, "StandardWorkingHoursPerDay", "8")} hours"));
        PayrollRules.Add(new PayrollRule("Working days / month", Get(settings, "StandardWorkingDaysPerMonth", "22")));
        PayrollRules.Add(new PayrollRule("Payslip distribution", RequireApprovalBeforePayslip ? "Manual approval before release" : "Can release without approval"));
        PayrollRules.Add(new PayrollRule("Late deductions", AutoCalculateLateDeductions ? "Automatic" : "Manual review"));
    }

    private static string Get(IReadOnlyDictionary<string, string> settings, string key, string fallback)
        => settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static bool GetBool(IReadOnlyDictionary<string, string> settings, string key, bool fallback)
        => settings.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;
}

public sealed record PayrollRule(string Name, string Value);
