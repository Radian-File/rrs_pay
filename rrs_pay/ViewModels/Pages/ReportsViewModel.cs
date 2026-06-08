using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using rrs_pay.Services;
using rrs_pay.ViewModels;
using rrs_pay.ViewModels.Infrastructure;

namespace rrs_pay.ViewModels.Pages;

public sealed class ReportsViewModel : ViewModelBase
{
    private readonly ReportExportService _reportExportService;
    private readonly AsyncRelayCommand _previewCommand;
    private readonly AsyncRelayCommand _exportExcelCommand;
    private readonly AsyncRelayCommand _exportPdfCommand;

    private string _selectedReport = "Payroll Summary";
    private string _selectedPeriod;
    private string _statusMessage = "Select a payroll period and preview it before exporting.";
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public ReportsViewModel()
    {
        _reportExportService = new ReportExportService();
        ReportTypes = new ObservableCollection<string> { "Payroll Summary", "Department Payroll Recap" };
        Periods = new ObservableCollection<string>(BuildPeriodOptions());
        _selectedPeriod = DateTime.Now.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        ReportRows = new ObservableCollection<ReportRow>();

        _previewCommand = new AsyncRelayCommand(PreviewAsync, () => !IsBusy);
        _exportExcelCommand = new AsyncRelayCommand(ExportExcelAsync, () => !IsBusy && ReportRows.Count > 0);
        _exportPdfCommand = new AsyncRelayCommand(ExportPdfAsync, () => !IsBusy && ReportRows.Count > 0);

        PreviewCommand = _previewCommand;
        ExportExcelCommand = _exportExcelCommand;
        ExportPdfCommand = _exportPdfCommand;

        _ = PreviewAsync();
    }

    public ObservableCollection<string> ReportTypes { get; }
    public ObservableCollection<string> Periods { get; }
    public ObservableCollection<ReportRow> ReportRows { get; }
    public ICommand PreviewCommand { get; }
    public ICommand ExportExcelCommand { get; }
    public ICommand ExportPdfCommand { get; }

    public string SelectedReport { get => _selectedReport; set => SetProperty(ref _selectedReport, value); }
    public string SelectedPeriod { get => _selectedPeriod; set => SetProperty(ref _selectedPeriod, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public bool HasReportRows => ReportRows.Count > 0;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    private async Task PreviewAsync()
    {
        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            var snapshot = await _reportExportService.BuildPayrollSummaryAsync(ParseSelectedPeriod());
            ReportRows.Clear();

            if (SelectedReport == "Department Payroll Recap")
            {
                foreach (var recap in snapshot.DepartmentRecaps)
                {
                    ReportRows.Add(new ReportRow(
                        recap.Department,
                        $"Employees {recap.EmployeeCount:N0} • Net {recap.NetPay:N2}",
                        $"Gross {recap.GrossPay:N2}; deductions/tax {recap.Deductions:N2}"));
                }
            }
            else
            {
                foreach (var row in snapshot.Rows)
                {
                    ReportRows.Add(new ReportRow(row.Metric, row.Value, row.Notes));
                }
            }

            OnPropertyChanged(nameof(HasReportRows));
            RaiseCommandStates();
            StatusMessage = $"Preview refreshed for {SelectedReport} • {SelectedPeriod} ({snapshot.Payroll.PayrollNumber}).";
        }
        catch (Exception ex)
        {
            ReportRows.Clear();
            OnPropertyChanged(nameof(HasReportRows));
            ErrorMessage = $"Could not preview report: {ex.Message}";
            StatusMessage = "Generate payroll for this period before exporting reports.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportExcelAsync()
    {
        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            var path = await _reportExportService.ExportPayrollSummaryExcelAsync(ParseSelectedPeriod());
            StatusMessage = $"Excel report exported to {path}.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not export Excel report: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportPdfAsync()
    {
        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            var path = await _reportExportService.ExportPayrollSummaryPdfAsync(ParseSelectedPeriod());
            StatusMessage = $"PDF report exported to {path}.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not export PDF report: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RaiseCommandStates()
    {
        _previewCommand.RaiseCanExecuteChanged();
        _exportExcelCommand.RaiseCanExecuteChanged();
        _exportPdfCommand.RaiseCanExecuteChanged();
    }

    private DateTime ParseSelectedPeriod()
    {
        return DateTime.TryParseExact(SelectedPeriod, "MMMM yyyy", CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : DateTime.Now;
    }

    private static IEnumerable<string> BuildPeriodOptions()
    {
        var current = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        return Enumerable.Range(-5, 8)
            .Select(offset => current.AddMonths(offset).ToString("MMMM yyyy", CultureInfo.CurrentCulture))
            .Reverse();
    }
}

public sealed record ReportRow(string Metric, string Value, string Notes);
