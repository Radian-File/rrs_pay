using rrs_pay.ViewModels;

namespace rrs_pay.ViewModels.Pages;

public sealed class LoginViewModel : ViewModelBase
{
    private string _email = "admin@rrspay.local";
    private string _companyCode = "RRS";
    private bool _rememberMe = true;
    private bool _isLoading;
    private string _statusMessage = "Use your payroll administrator account to continue.";

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string CompanyCode
    {
        get => _companyCode;
        set => SetProperty(ref _companyCode, value);
    }

    public bool RememberMe
    {
        get => _rememberMe;
        set => SetProperty(ref _rememberMe, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }
}
