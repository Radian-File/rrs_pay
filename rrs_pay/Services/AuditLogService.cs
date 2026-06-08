using rrs_pay.Data;
using rrs_pay.Models;

namespace rrs_pay.Services;

public class AuditLogService
{
    private readonly Func<AppDbContext> _contextFactory;
    private readonly SessionService? _sessionService;

    public AuditLogService() : this(AppDbContext.CreateDefault, null)
    {
    }

    public AuditLogService(SessionService sessionService) : this(AppDbContext.CreateDefault, sessionService)
    {
    }

    public AuditLogService(Func<AppDbContext> contextFactory, SessionService? sessionService = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _sessionService = sessionService;
    }

    public async Task LogAsync(
        string action,
        string entityName,
        string? entityId = null,
        string? details = null,
        int? userId = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory();
        var auditLog = new AuditLog
        {
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            UserId = userId ?? _sessionService?.CurrentUser?.Id,
            Timestamp = DateTime.UtcNow
        };

        context.AuditLogs.Add(auditLog);
        await context.SaveChangesAsync(cancellationToken);
    }
}
