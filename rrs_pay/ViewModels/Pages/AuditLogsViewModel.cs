using System.Collections.ObjectModel;
using rrs_pay.Models;
using rrs_pay.Services;
using rrs_pay.ViewModels;
using rrs_pay.ViewModels.Infrastructure;

namespace rrs_pay.ViewModels.Pages;

public sealed class AuditLogsViewModel : ViewModelBase
{
    private readonly DataService _dataService;
    private string _searchText = string.Empty;
    private string _actionFilter = string.Empty;
    private string _userFilter = string.Empty;
    private DateTime? _startDate;
    private DateTime? _endDate;
    private string _statusMessage = "Loading audit logs from database...";
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public AuditLogsViewModel() : this(new DataService())
    {
    }

    public AuditLogsViewModel(DataService dataService)
    {
        _dataService = dataService;
        AuditEntries = new ObservableCollection<AuditLogEntry>();
        ApplyFilterCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        ExportCommand = new rrs_pay.ViewModels.RelayCommand(() => StatusMessage = "Audit export is handled by the export/reporting agent.");
        _ = LoadAsync();
    }

    public ObservableCollection<AuditLogEntry> AuditEntries { get; }
    public AsyncRelayCommand ApplyFilterCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public rrs_pay.ViewModels.RelayCommand ExportCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _ = LoadAsync();
            }
        }
    }

    public string ActionFilter
    {
        get => _actionFilter;
        set => SetProperty(ref _actionFilter, value);
    }

    public string UserFilter
    {
        get => _userFilter;
        set => SetProperty(ref _userFilter, value);
    }

    public DateTime? StartDate
    {
        get => _startDate;
        set => SetProperty(ref _startDate, value);
    }

    public DateTime? EndDate
    {
        get => _endDate;
        set => SetProperty(ref _endDate, value);
    }

    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ApplyFilterCommand.RaiseCanExecuteChanged();
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            var logs = await _dataService.GetAuditLogsAsync(StartDate, EndDate, ActionFilter, UserFilter, SearchText, maxRows: 500);
            AuditEntries.Clear();
            foreach (var log in logs)
            {
                AuditEntries.Add(AuditLogEntry.FromModel(log));
            }

            StatusMessage = $"Loaded {AuditEntries.Count} audit log entries.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Failed to load audit logs.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed record AuditLogEntry(DateTime Timestamp, string Actor, string Action, string Area, string Reference)
{
    public static AuditLogEntry FromModel(AuditLog log) => new(
        log.Timestamp.ToLocalTime(),
        log.User?.Username ?? "system",
        log.Action,
        log.EntityName,
        string.IsNullOrWhiteSpace(log.EntityId) ? log.Details ?? string.Empty : log.EntityId!);
}
