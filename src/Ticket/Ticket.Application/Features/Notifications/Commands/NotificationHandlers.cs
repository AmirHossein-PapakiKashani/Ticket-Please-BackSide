using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ticket.Application;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Application.DTOs;

namespace Ticket.Application.Features.Notifications.Commands;

public sealed record GetNotificationsQuery(string? Search, int PageNumber, int PageSize, bool? OnlyUnread)
    : IRequest<PagedResponse<NotificationItem>>;

public sealed class GetNotificationsQueryValidator : AbstractValidator<GetNotificationsQuery>
{
    public GetNotificationsQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetNotificationsQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetNotificationsQuery, PagedResponse<NotificationItem>>
{
    public async Task<PagedResponse<NotificationItem>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == current.UserId);
        if (request.OnlyUnread == true)
            query = query.Where(n => !n.IsRead);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(n => n.Title.Contains(s) || (n.Body != null && n.Body.Contains(s)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(n => n.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new NotificationItem(
                n.Id,
                n.Type.ToString(),
                n.Title,
                n.Body,
                n.RefId,
                n.IsRead,
                n.CreatedAt))
            .ToListAsync(cancellationToken);

        var pages = (int)Math.Ceiling(total / (double)request.PageSize);
        return new PagedResponse<NotificationItem>(items, request.PageNumber, request.PageSize, total, pages);
    }
}

public sealed record MarkNotificationReadCommand(int NotificationId) : IRequest<SuccessResponse>;

public sealed class MarkNotificationReadCommandHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<MarkNotificationReadCommand, SuccessResponse>
{
    public async Task<SuccessResponse> Handle(MarkNotificationReadCommand command, CancellationToken cancellationToken)
    {
        var n = await db.Notifications
            .FirstOrDefaultAsync(x => x.Id == command.NotificationId && x.UserId == current.UserId, cancellationToken)
            ?? throw new NotFoundException("Notification was not found");
        n.IsRead = true;
        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }
}

public sealed record MarkAllNotificationsReadCommand : IRequest<SuccessResponse>;

public sealed class MarkAllNotificationsReadCommandHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<MarkAllNotificationsReadCommand, SuccessResponse>
{
    public async Task<SuccessResponse> Handle(MarkAllNotificationsReadCommand command, CancellationToken cancellationToken)
    {
        var unread = await db.Notifications
            .Where(n => n.UserId == current.UserId && !n.IsRead)
            .ToListAsync(cancellationToken);
        foreach (var n in unread) n.IsRead = true;
        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }
}
