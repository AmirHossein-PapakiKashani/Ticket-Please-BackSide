using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Application.DTOs;
using Ticket.Domain.Enums;

namespace Ticket.Application.Features.Requester.Queries;

public sealed record GetRequesterTicketsQuery(
    string? Search,
    TicketStatus? Status,
    bool? CreatedByMe,
    int PageNumber,
    int PageSize) : IRequest<PagedResponse<RequesterTicketListItem>>;

public sealed class GetRequesterTicketsQueryValidator : AbstractValidator<GetRequesterTicketsQuery>
{
    public GetRequesterTicketsQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
    }
}

public sealed class GetRequesterTicketsQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetRequesterTicketsQuery, PagedResponse<RequesterTicketListItem>>
{
    public async Task<PagedResponse<RequesterTicketListItem>> Handle(
        GetRequesterTicketsQuery request,
        CancellationToken cancellationToken)
    {
        var clientId = RequesterHelpers.RequireClientId(current);
        var query = db.Tickets.AsNoTracking().Where(t => t.ClientId == clientId && !t.IsDeleted);
        if (request.CreatedByMe == true)
            query = query.Where(t => t.RequesterId == current.UserId);
        if (request.Status is TicketStatus status)
            query = query.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(t => t.Topic.Contains(s));
        }

        var total = await query.CountAsync(cancellationToken);
        var tickets = await query.OrderByDescending(t => t.OpenDate)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var requesterIds = tickets.Select(t => t.RequesterId).Distinct().ToList();
        var names = await db.Users.AsNoTracking()
            .Where(u => requesterIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var items = tickets.Select(t => new RequesterTicketListItem(
            t.Id,
            t.Topic,
            names.GetValueOrDefault(t.RequesterId, string.Empty),
            t.Status,
            t.Priority,
            t.OpenDate)).ToList();

        return RequesterHelpers.Page(items, request.PageNumber, request.PageSize, total);
    }
}

public sealed record GetRequesterTicketQuery(int TicketId) : IRequest<TicketDetailDto>;

public sealed class GetRequesterTicketQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetRequesterTicketQuery, TicketDetailDto>
{
    public async Task<TicketDetailDto> Handle(GetRequesterTicketQuery request, CancellationToken cancellationToken)
    {
        var clientId = RequesterHelpers.RequireClientId(current);
        var ticket = await db.Tickets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TicketId && t.ClientId == clientId && !t.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Ticket was not found");

        var requesterName = await db.Users.AsNoTracking()
            .Where(u => u.Id == ticket.RequesterId).Select(u => u.FullName).FirstAsync(cancellationToken);
        string? agentName = null;
        if (ticket.AssignedAgentId is int agentId)
            agentName = await db.Users.AsNoTracking().Where(u => u.Id == agentId).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken);
        var clientName = await db.Clients.AsNoTracking()
            .Where(c => c.Id == ticket.ClientId).Select(c => c.Name).FirstAsync(cancellationToken);

        return new TicketDetailDto(ticket.Topic, ticket.Status, ticket.Priority, clientName, requesterName, agentName);
    }
}

public sealed record GetRequesterMessagesQuery(int TicketId, int PageNumber, int PageSize)
    : IRequest<PagedResponse<TicketMessageDto>>;

public sealed class GetRequesterMessagesQueryValidator : AbstractValidator<GetRequesterMessagesQuery>
{
    public GetRequesterMessagesQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetRequesterMessagesQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetRequesterMessagesQuery, PagedResponse<TicketMessageDto>>
{
    public async Task<PagedResponse<TicketMessageDto>> Handle(
        GetRequesterMessagesQuery request,
        CancellationToken cancellationToken)
    {
        var clientId = RequesterHelpers.RequireClientId(current);
        _ = await RequesterHelpers.RequireClientTicketAsync(db, request.TicketId, clientId, cancellationToken);

        var query = db.TicketMessages.AsNoTracking()
            .Where(m => m.TicketId == request.TicketId && !m.IsDeleted);
        var total = await query.CountAsync(cancellationToken);
        var messages = await query.OrderBy(m => m.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(m => m.Attachments)
            .ToListAsync(cancellationToken);

        var senderIds = messages.Select(m => m.SenderId).Distinct().ToList();
        var names = await db.Users.AsNoTracking()
            .Where(u => senderIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var items = messages.Select(m => new TicketMessageDto(
            m.SenderId,
            names.GetValueOrDefault(m.SenderId, string.Empty),
            m.Text,
            m.CreatedAt,
            m.SeenAt,
            m.Attachments.Where(a => !a.IsDeleted)
                .Select(a => new AttachmentDto(a.FileUrl, a.FileName, a.FileType, a.FileSizeKB))
                .ToList())).ToList();

        return RequesterHelpers.Page(items, request.PageNumber, request.PageSize, total);
    }
}
