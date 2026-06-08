using rrs_pay.Models;

namespace rrs_pay.Services;

public class SessionService
{
    public User? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;
    public string CurrentUsername => CurrentUser?.Username ?? string.Empty;
    public string CurrentRoleName => CurrentUser?.Role?.Name ?? string.Empty;

    public event EventHandler? SessionChanged;

    public void SignIn(User user)
    {
        CurrentUser = user ?? throw new ArgumentNullException(nameof(user));
        OnSessionChanged();
    }

    public void SignOut()
    {
        CurrentUser = null;
        OnSessionChanged();
    }

    private void OnSessionChanged()
    {
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}
