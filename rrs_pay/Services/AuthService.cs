using Microsoft.EntityFrameworkCore;
using rrs_pay.Data;
using rrs_pay.Models;

namespace rrs_pay.Services;

public class AuthService
{
    private readonly Func<AppDbContext> _contextFactory;
    private readonly SessionService? _sessionService;
    private readonly AuditLogService? _auditLogService;

    public AuthService() : this(AppDbContext.CreateDefault, null, null)
    {
    }

    public AuthService(SessionService sessionService) : this(AppDbContext.CreateDefault, sessionService, null)
    {
    }

    public AuthService(Func<AppDbContext> contextFactory, SessionService? sessionService = null, AuditLogService? auditLogService = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _sessionService = sessionService;
        _auditLogService = auditLogService;
    }

    public async Task<User?> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var normalizedUsername = username.Trim().ToLowerInvariant();

        await using var context = _contextFactory();
        var user = await context.Users
            .Include(item => item.Role)
            .Include(item => item.Employee)
            .FirstOrDefaultAsync(item => item.Username.ToLower() == normalizedUsername, cancellationToken);

        if (user is null || !user.IsActive || !PasswordHasher.VerifyPassword(password, user.PasswordHash))
        {
            return null;
        }

        user.LastLoginAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        _sessionService?.SignIn(user);

        if (_auditLogService is not null)
        {
            await _auditLogService.LogAsync("Login", nameof(User), user.Id.ToString(), $"User '{user.Username}' logged in.", user.Id, cancellationToken);
        }

        return user;
    }

    public void Logout()
    {
        _sessionService?.SignOut();
    }
}
