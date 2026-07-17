using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Application.DTOs;
using Ticket.Domain;
using Ticket.Domain.Enums;

namespace Ticket.Application.Features.AgentArea.Queries;

public sealed record GetAgentTicketsQuery(
    string? Search,
    TicketStatus? Status,
    TicketPriority? Priority,
    bool? AssignedToMe,
    int PageNumber,
    int PageSize) : IRequest<PagedResponse<AgentTicketListItem>>;

public sealed class GetAgentTicketsQueryValidator : AbstractValidator<GetAgentTicketsQuery>
{
    public GetAgentTicketsQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.Priority).IsInEnum().When(x => x.Priority.HasValue);
    }
}

public sealed class GetAgentTicketsQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetAgentTicketsQuery, PagedResponse<AgentTicketListItem>>
{
    public async Task<PagedResponse<AgentTicketListItem>> Handle(GetAgentTicketsQuery request, CancellationToken cancellationToken)
    {
        var providerId = AgentHelpers.RequireProviderId(current);
        var query = db.Tickets.AsNoTracking().Where(t => t.ProviderId == providerId && !t.IsDeleted);
        if (request.AssignedToMe == true)
            query = query.Where(t => t.AssignedAgentId == current.UserId);
        if (request.Status is TicketStatus status) query = query.Where(t => t.Status == status);
        if (request.Priority is TicketPriority priority) query = query.Where(t => t.Priority == priority);
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

        var clientIds = tickets.Select(t => t.ClientId).Distinct().ToList();
        var requesterIds = tickets.Select(t => t.RequesterId).Distinct().ToList();
        var clients = await db.Clients.AsNoTracking().Where(c => clientIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var requesters = await db.Users.AsNoTracking().Where(u => requesterIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var items = tickets.Select(t => new AgentTicketListItem(
            t.Id,
            t.Topic,
            clients.GetValueOrDefault(t.ClientId, string.Empty),
            requesters.GetValueOrDefault(t.RequesterId, string.Empty),
            t.Status,
            t.Priority,
            t.OpenDate,
            t.SeenByAgentDate is not null)).ToList();

        return AgentHelpers.Page(items, request.PageNumber, request.PageSize, total);
    }
}

public sealed record GetAgentTicketQuery(int TicketId) : IRequest<TicketDetailDto>;

public sealed class GetAgentTicketQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetAgentTicketQuery, TicketDetailDto>
{
    public async Task<TicketDetailDto> Handle(GetAgentTicketQuery request, CancellationToken cancellationToken)
    {
        var providerId = AgentHelpers.RequireProviderId(current);
        var ticket = await db.Tickets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TicketId && t.ProviderId == providerId && !t.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Ticket was not found");

        var clientName = await db.Clients.AsNoTracking().Where(c => c.Id == ticket.ClientId).Select(c => c.Name).FirstAsync(cancellationToken);
        var requesterName = await db.Users.AsNoTracking().Where(u => u.Id == ticket.RequesterId).Select(u => u.FullName).FirstAsync(cancellationToken);
        string? agentName = null;
        if (ticket.AssignedAgentId is int agentId)
            agentName = await db.Users.AsNoTracking().Where(u => u.Id == agentId).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken);

        return new TicketDetailDto(ticket.Topic, ticket.Status, ticket.Priority, clientName, requesterName, agentName);
    }
}

public sealed record GetAgentMessagesQuery(int TicketId, int PageNumber, int PageSize)
    : IRequest<PagedResponse<TicketMessageDto>>;

public sealed class GetAgentMessagesQueryValidator : AbstractValidator<GetAgentMessagesQuery>
{
    public GetAgentMessagesQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetAgentMessagesQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetAgentMessagesQuery, PagedResponse<TicketMessageDto>>
{
    public async Task<PagedResponse<TicketMessageDto>> Handle(GetAgentMessagesQuery request, CancellationToken cancellationToken)
    {
        var providerId = AgentHelpers.RequireProviderId(current);
        _ = await AgentHelpers.RequireProviderTicketAsync(db, request.TicketId, providerId, cancellationToken);

        var query = db.TicketMessages.AsNoTracking().Where(m => m.TicketId == request.TicketId && !m.IsDeleted);
        var total = await query.CountAsync(cancellationToken);
        var messages = await query.OrderBy(m => m.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(m => m.Attachments)
            .ToListAsync(cancellationToken);

        var senderIds = messages.Select(m => m.SenderId).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => senderIds.Contains(u.Id))
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

        return AgentHelpers.Page(items, request.PageNumber, request.PageSize, total);
    }
}

public sealed record GetTicketNotesQuery(int TicketId) : IRequest<IReadOnlyList<TicketNoteDto>>;

public sealed class GetTicketNotesQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetTicketNotesQuery, IReadOnlyList<TicketNoteDto>>
{
    public async Task<IReadOnlyList<TicketNoteDto>> Handle(GetTicketNotesQuery request, CancellationToken cancellationToken)
    {
        var providerId = AgentHelpers.RequireProviderId(current);
        _ = await AgentHelpers.RequireProviderTicketAsync(db, request.TicketId, providerId, cancellationToken);

        var notes = await db.TicketNotes.AsNoTracking()
            .Where(n => n.TicketId == request.TicketId && !n.IsDeleted)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
        var authorIds = notes.Select(n => n.AuthorId).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => authorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return notes.Select(n => new TicketNoteDto(
            names.GetValueOrDefault(n.AuthorId, string.Empty),
            n.Text,
            n.CreatedAt)).ToList();
    }
}

public sealed record GetActiveAgentsQuery : IRequest<IReadOnlyList<ActiveAgentListItem>>;

public sealed class GetActiveAgentsQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetActiveAgentsQuery, IReadOnlyList<ActiveAgentListItem>>
{
    public async Task<IReadOnlyList<ActiveAgentListItem>> Handle(GetActiveAgentsQuery request, CancellationToken cancellationToken)
    {
        var providerId = AgentHelpers.RequireProviderId(current);
        var agentRoleId = await db.Roles.Where(r => r.Name == RoleNames.Agent).Select(r => r.Id).FirstAsync(cancellationToken);
        var agents = await db.Users.AsNoTracking()
            .Where(u => u.ProviderId == providerId && u.RoleId == agentRoleId && u.IsActive && !u.IsDeleted)
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);

        var result = new List<ActiveAgentListItem>();
        foreach (var agent in agents)
        {
            var open = await db.Tickets.CountAsync(t =>
                t.AssignedAgentId == agent.Id &&
                !t.IsDeleted &&
                AgentHelpers.OpenStatuses.Contains(t.Status),
                cancellationToken);
            result.Add(new ActiveAgentListItem(agent.Id, agent.FullName, open));
        }
        return result;
    }
}
