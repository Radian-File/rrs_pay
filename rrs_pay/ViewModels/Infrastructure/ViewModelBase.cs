namespace rrs_pay.ViewModels.Infrastructure;

public abstract class ViewModelBase : ObservableObject
{
    private bool _isBusy;
    private string? _errorMessage;
    private string _title = string.Empty;

    public string Title
    {
        get => _title;
        protected set => SetProperty(ref _title, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        protected set => SetProperty(ref _isBusy, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        protected set => SetProperty(ref _errorMessage, value);
    }

    protected void ClearError() => ErrorMessage = null;

    protected void SetError(Exception exception) => ErrorMessage = exception.Message;
}
