using MediatR;
using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Application.DTOs;
using Ticket.Domain;
using Ticket.Domain.Enums;

namespace Ticket.Application.Features.Pm.Queries;

public sealed record GetPmDashboardStatsQuery : IRequest<ProviderDashboardStats>;

public sealed class GetPmDashboardStatsQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetPmDashboardStatsQuery, ProviderDashboardStats>
{
    private static readonly TicketStatus[] OpenStatuses =
    [
        TicketStatus.Open,
        TicketStatus.InProgress,
        TicketStatus.PendingRequesterResponse
    ];

    public async Task<ProviderDashboardStats> Handle(GetPmDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        var providerId = PmHelpers.RequireProviderId(current);
        var agentRoleId = await PmHelpers.AgentRoleIdAsync(db, cancellationToken);

        var totalClients = await db.Clients.CountAsync(c => c.ProviderId == providerId && !c.IsDeleted, cancellationToken);
        var totalAgents = await db.Users.CountAsync(
            u => u.ProviderId == providerId && u.RoleId == agentRoleId && !u.IsDeleted,
            cancellationToken);

        var tickets = db.Tickets.AsNoTracking().Where(t => t.ProviderId == providerId && !t.IsDeleted);
        var openTicketsTotal = await tickets.CountAsync(t => OpenStatuses.Contains(t.Status), cancellationToken);
        var closedTicketsTotal = await tickets.CountAsync(
            t => t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed,
            cancellationToken);
        var unassignedTicketsCount = await tickets.CountAsync(
            t => t.AssignedAgentId == null && OpenStatuses.Contains(t.Status),
            cancellationToken);

        return new ProviderDashboardStats(totalClients, totalAgents, openTicketsTotal, closedTicketsTotal, unassignedTicketsCount);
    }
}
