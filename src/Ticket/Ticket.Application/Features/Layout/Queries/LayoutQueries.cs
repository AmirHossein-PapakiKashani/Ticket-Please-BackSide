using MediatR;
using Microsoft.EntityFrameworkCore;
using Ticket.Application;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Application.DTOs;

namespace Ticket.Application.Features.Layout.Queries;

public sealed record GetProfileSummaryQuery : IRequest<ProfileSummary>;

public sealed class GetProfileSummaryQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetProfileSummaryQuery, ProfileSummary>
{
    public async Task<ProfileSummary> Handle(GetProfileSummaryQuery request, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == current.UserId && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("User was not found");
        return new ProfileSummary(user.FullName, user.Role!.Name, null);
    }
}

public sealed record GetUnreadCountQuery : IRequest<UnreadCountResponse>;

public sealed class GetUnreadCountQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetUnreadCountQuery, UnreadCountResponse>
{
    public async Task<UnreadCountResponse> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var count = await db.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == current.UserId && !n.IsRead, cancellationToken);
        return new UnreadCountResponse(count);
    }
}
