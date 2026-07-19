using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Application.DTOs;
using Ticket.Domain;

namespace Ticket.Application.Features.Pm.Queries;

public sealed record GetAgentsQuery(string? Search, int PageNumber, int PageSize) : IRequest<PagedResponse<AgentListItem>>;

public sealed class GetAgentsQueryValidator : AbstractValidator<GetAgentsQuery>
{
    public GetAgentsQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetAgentsQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetAgentsQuery, PagedResponse<AgentListItem>>
{
    public async Task<PagedResponse<AgentListItem>> Handle(GetAgentsQuery request, CancellationToken cancellationToken)
    {
        var providerId = PmHelpers.RequireProviderId(current);
        var agentRoleId = await PmHelpers.AgentRoleIdAsync(db, cancellationToken);
        var query = db.Users.AsNoTracking()
            .Where(u => u.ProviderId == providerId && u.RoleId == agentRoleId && !u.IsDeleted);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(u => u.FullName.Contains(s) || u.Username.Contains(s));
        }

        var total = await query.CountAsync(cancellationToken);
        var users = await query.OrderBy(u => u.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = users.Select(u => new AgentListItem(u.Id, u.FullName, u.Username, u.IsActive, 0, 0)).ToList();
        return PmHelpers.Page(items, request.PageNumber, request.PageSize, total);
    }
}

public sealed record GetAgentQuery(int UserId) : IRequest<AgentDetail>;

public sealed class GetAgentQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetAgentQuery, AgentDetail>
{
    public async Task<AgentDetail> Handle(GetAgentQuery request, CancellationToken cancellationToken)
    {
        var providerId = PmHelpers.RequireProviderId(current);
        var agentRoleId = await PmHelpers.AgentRoleIdAsync(db, cancellationToken);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(
            u => u.Id == request.UserId && u.ProviderId == providerId && u.RoleId == agentRoleId && !u.IsDeleted,
            cancellationToken) ?? throw new NotFoundException("Agent was not found");

        return new AgentDetail(
            user.FullName,
            user.Username,
            user.PhoneNumber,
            user.IsActive,
            new AgentTicketStats(0, 0, "00:00:00"));
    }
}

public sealed record GetAgentStatsQuery(int UserId) : IRequest<AgentTicketStats>;

public sealed class GetAgentStatsQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetAgentStatsQuery, AgentTicketStats>
{
    public async Task<AgentTicketStats> Handle(GetAgentStatsQuery request, CancellationToken cancellationToken)
    {
        var providerId = PmHelpers.RequireProviderId(current);
        var agentRoleId = await PmHelpers.AgentRoleIdAsync(db, cancellationToken);
        var exists = await db.Users.AsNoTracking().AnyAsync(
            u => u.Id == request.UserId && u.ProviderId == providerId && u.RoleId == agentRoleId && !u.IsDeleted,
            cancellationToken);
        if (!exists) throw new NotFoundException("Agent was not found");
        return new AgentTicketStats(0, 0, "00:00:00");
    }
}
