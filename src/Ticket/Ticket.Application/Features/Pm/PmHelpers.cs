using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Domain;

namespace Ticket.Application.Features.Pm;

internal static class PmHelpers
{
    public static int RequireProviderId(ICurrentUser current) =>
        current.ProviderId ?? throw new ForbiddenAppException("Provider context is required.");

    public static async Task<Plan> RequireActivePlanAsync(
        IApplicationDbContext db,
        int providerId,
        CancellationToken cancellationToken)
    {
        var sub = await db.ProviderSubscriptions.AsNoTracking()
            .Where(s => s.ProviderId == providerId && s.IsActive)
            .OrderByDescending(s => s.ExpireDate)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BadRequestAppException("No active subscription found");

        return await db.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == sub.PlanId, cancellationToken)
            ?? throw new BadRequestAppException("No active subscription found");
    }

    public static async Task<int> AgentRoleIdAsync(IApplicationDbContext db, CancellationToken cancellationToken) =>
        await db.Roles.Where(r => r.Name == RoleNames.Agent).Select(r => r.Id).FirstAsync(cancellationToken);

    public static async Task<int> ClientManagerRoleIdAsync(IApplicationDbContext db, CancellationToken cancellationToken) =>
        await db.Roles.Where(r => r.Name == RoleNames.ClientManager).Select(r => r.Id).FirstAsync(cancellationToken);

    public static async Task<int> RequesterRoleIdAsync(IApplicationDbContext db, CancellationToken cancellationToken) =>
        await db.Roles.Where(r => r.Name == RoleNames.Requester).Select(r => r.Id).FirstAsync(cancellationToken);

    public static PagedResponse<T> Page<T>(IReadOnlyList<T> items, int pageNumber, int pageSize, int total) =>
        new(items, pageNumber, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
}
