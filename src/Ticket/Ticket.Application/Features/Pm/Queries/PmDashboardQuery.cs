using MediatR;
using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Application.DTOs;
using Ticket.Domain;

namespace Ticket.Application.Features.Pm.Queries;

public sealed record GetPmDashboardStatsQuery : IRequest<ProviderDashboardStats>;

public sealed class GetPmDashboardStatsQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetPmDashboardStatsQuery, ProviderDashboardStats>
{
    public async Task<ProviderDashboardStats> Handle(GetPmDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        var providerId = PmHelpers.RequireProviderId(current);
        var agentRoleId = await PmHelpers.AgentRoleIdAsync(db, cancellationToken);

        var totalClients = await db.Clients.CountAsync(c => c.ProviderId == providerId && !c.IsDeleted, cancellationToken);
        var totalAgents = await db.Users.CountAsync(
            u => u.ProviderId == providerId && u.RoleId == agentRoleId && !u.IsDeleted,
            cancellationToken);

        // Ticket entity arrives in Phase 3; counts stay zero until then.
        return new ProviderDashboardStats(totalClients, totalAgents, 0, 0, 0);
    }
}
