using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Notifications.GetNotifications;

// ─── Query ────────────────────────────────────────────────────────────────────
/// <summary>Returns paginated notifications for the current user, newest first.</summary>
public record GetNotificationsQuery(
    bool UnreadOnly = false,
    int Page = 1,
    int PageSize = 20
) : IRequest<NotificationListResult>;

public record NotificationListResult(
    List<NotificationDto> Items,
    int TotalCount,
    int UnreadCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record NotificationDto(
    Guid Id,
    string Type,
    string Message,
    bool IsRead,
    string? ReferenceType,
    Guid? ReferenceId,
    DateTime CreatedAt
);

public class GetNotificationsQueryHandler
    : IRequestHandler<GetNotificationsQuery, NotificationListResult>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetNotificationsQueryHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<NotificationListResult> Handle(
        GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        var baseQuery = _db.Notifications.Where(n => n.UserId == userId);

        var unreadCount = await baseQuery.CountAsync(n => !n.IsRead, cancellationToken);

        var query = request.UnreadOnly ? baseQuery.Where(n => !n.IsRead) : baseQuery;
        var total = await query.CountAsync(cancellationToken);

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var page = Math.Max(1, request.Page);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto(
                n.Id,
                n.Type.ToString(),
                n.Message,
                n.IsRead,
                n.ReferenceType,
                n.ReferenceId,
                n.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return new NotificationListResult(items, total, unreadCount, page, pageSize,
            (int)Math.Ceiling((double)total / pageSize));
    }
}
