using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Notifications.MarkNotificationRead;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Marks one or all of the current user's notifications as read.
/// Pass a specific NotificationId to mark one, or null to mark ALL as read.
/// </summary>
public record MarkNotificationReadCommand(Guid? NotificationId) : IRequest<int>;  // Returns count updated

public class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, int>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public MarkNotificationReadCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        var query = _db.Notifications.Where(n => n.UserId == userId && !n.IsRead);

        if (request.NotificationId.HasValue)
        {
            query = query.Where(n => n.Id == request.NotificationId.Value);
            var notification = await query.FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("Notification not found or already read.");
            notification.IsRead = true;
            await _db.SaveChangesAsync(cancellationToken);
            return 1;
        }

        // Mark all unread notifications as read
        var unread = await query.ToListAsync(cancellationToken);
        foreach (var n in unread) n.IsRead = true;
        await _db.SaveChangesAsync(cancellationToken);
        return unread.Count;
    }
}
