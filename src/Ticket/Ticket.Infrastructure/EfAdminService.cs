using Microsoft.EntityFrameworkCore;
using Ticket.Application;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Domain;

namespace Ticket.Infrastructure;

public sealed class EfAdminService(IApplicationDbContext db, IPasswordHasher passwordHasher) : IAdminService
{
    public async Task<AdminDashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken)
    {
        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var totalProviders = await db.Providers.CountAsync(p => !p.IsDeleted, cancellationToken);
        var activeProviders = await db.Providers.CountAsync(p => !p.IsDeleted && p.IsActive, cancellationToken);
        var revenue = await db.ProviderSubscriptions.Where(s => s.PurchaseDate >= month).SumAsync(s => (decimal?)s.MoneyPaid, cancellationToken) ?? 0;
        var totalClients = await db.Clients.CountAsync(c => !c.IsDeleted, cancellationToken);
        return new AdminDashboardStats(totalProviders, activeProviders, totalClients, 0, revenue);
    }

    public async Task<PagedResponse<ProviderListItem>> GetProvidersAsync(string? search, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Providers.AsNoTracking().Where(p => !p.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.Email.Contains(search));

        var total = await query.CountAsync(cancellationToken);
        var providers = await query.OrderBy(p => p.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = new List<ProviderListItem>();
        foreach (var p in providers)
        {
            var planName = await ActivePlanNameAsync(p.Id, cancellationToken);
            var clientCount = await db.Clients.CountAsync(c => c.ProviderId == p.Id && !c.IsDeleted, cancellationToken);
            items.Add(new ProviderListItem(p.Id, p.Name, p.Email, p.IsActive, planName, clientCount));
        }
        return Page(items, pageNumber, pageSize, total);
    }

    public async Task<CreatedProviderResponse> CreateProviderAsync(CreateProviderRequest request, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.Username == request.ManagerUsername.Trim(), cancellationToken))
            throw new ConflictException("This username is already in use");

        var pmRole = await db.Roles.FirstAsync(r => r.Name == RoleNames.ProviderManager, cancellationToken);
        var provider = new Provider
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            PhoneNumber = request.PhoneNumber.Trim()
        };
        db.Providers.Add(provider);
        db.Users.Add(new User
        {
            Username = request.ManagerUsername.Trim(),
            FullName = request.ManagerFullName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            PassHash = passwordHasher.Hash(request.ManagerPassword),
            RoleId = pmRole.Id,
            Provider = provider
        });
        await db.SaveChangesAsync(cancellationToken);
        return new CreatedProviderResponse(provider.Id);
    }

    public async Task<SuccessResponse> DeactivateProviderAsync(int providerId, CancellationToken cancellationToken)
    {
        var provider = await FindProviderAsync(providerId, cancellationToken);
        provider.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }

    public async Task<ProviderDetail> GetProviderAsync(int providerId, CancellationToken cancellationToken)
    {
        var provider = await FindProviderAsync(providerId, cancellationToken);
        var subscription = await db.ProviderSubscriptions.AsNoTracking()
            .Where(s => s.ProviderId == providerId && s.IsActive)
            .OrderByDescending(s => s.ExpireDate)
            .FirstOrDefaultAsync(cancellationToken);
        SubscriptionSummary? summary = null;
        if (subscription is not null)
        {
            var plan = await db.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == subscription.PlanId, cancellationToken);
            if (plan is not null) summary = new SubscriptionSummary(plan.Name, subscription.ExpireDate);
        }
        var clientCount = await db.Clients.CountAsync(c => c.ProviderId == providerId && !c.IsDeleted, cancellationToken);
        var agentRoleId = await db.Roles.Where(r => r.Name == RoleNames.Agent).Select(r => r.Id).FirstAsync(cancellationToken);
        var agentCount = await db.Users.CountAsync(u => u.ProviderId == providerId && u.RoleId == agentRoleId && u.IsActive && !u.IsDeleted, cancellationToken);
        return new ProviderDetail(provider.Name, provider.Email, provider.PhoneNumber, provider.IsActive, summary, clientCount, agentCount);
    }

    public async Task<SuccessResponse> UpdateProviderAsync(int providerId, UpdateProviderRequest request, CancellationToken cancellationToken)
    {
        var p = await FindProviderAsync(providerId, cancellationToken);
        p.Name = request.Name.Trim();
        p.Email = request.Email.Trim();
        p.PhoneNumber = request.PhoneNumber.Trim();
        p.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }

    public async Task<PagedResponse<PlanListItem>> GetPlansAsync(string? search, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Plans.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search));
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(p => p.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(p => new PlanListItem(p.Id, p.Name, p.Price, p.DurationDays, p.MaxClientCount, p.MaxAgentCount))
            .ToListAsync(cancellationToken);
        return Page(items, pageNumber, pageSize, total);
    }

    public async Task<CreatedPlanResponse> CreatePlanAsync(CreatePlanRequest request, CancellationToken cancellationToken)
    {
        var plan = new Plan
        {
            Name = request.Name.Trim(),
            DurationDays = request.DurationDays,
            Price = request.Price,
            MaxClientCount = request.MaxClientCount,
            MaxAgentCount = request.MaxAgentCount,
            ModulesJson = request.ModulesJson
        };
        db.Plans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);
        return new CreatedPlanResponse(plan.Id);
    }

    public async Task<SuccessResponse> UpdatePlanAsync(int planId, UpdatePlanRequest request, CancellationToken cancellationToken)
    {
        var p = await db.Plans.FirstOrDefaultAsync(x => x.Id == planId, cancellationToken)
            ?? throw new NotFoundException("Plan was not found");
        p.Name = request.Name.Trim();
        p.DurationDays = request.DurationDays;
        p.Price = request.Price;
        p.MaxClientCount = request.MaxClientCount;
        p.MaxAgentCount = request.MaxAgentCount;
        p.ModulesJson = request.ModulesJson;
        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }

    public async Task<SuccessResponse> DeactivatePlanAsync(int planId, CancellationToken cancellationToken)
    {
        var p = await db.Plans.FirstOrDefaultAsync(x => x.Id == planId, cancellationToken)
            ?? throw new NotFoundException("Plan was not found");
        p.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }

    public async Task<PagedResponse<SubscriptionListItem>> GetSubscriptionsAsync(int providerId, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        await FindProviderAsync(providerId, cancellationToken);
        var query = db.ProviderSubscriptions.AsNoTracking().Where(s => s.ProviderId == providerId);
        var total = await query.CountAsync(cancellationToken);
        var subs = await query.OrderByDescending(s => s.PurchaseDate).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = new List<SubscriptionListItem>();
        foreach (var s in subs)
        {
            var plan = await db.Plans.AsNoTracking().FirstAsync(p => p.Id == s.PlanId, cancellationToken);
            items.Add(new SubscriptionListItem(plan.Name, s.PurchaseDate, s.ExpireDate, s.MoneyPaid, s.IsActive));
        }
        return Page(items, pageNumber, pageSize, total);
    }

    public async Task<CreatedSubscriptionResponse> CreateSubscriptionAsync(int providerId, CreateSubscriptionRequest request, CancellationToken cancellationToken)
    {
        await FindProviderAsync(providerId, cancellationToken);
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == request.PlanId, cancellationToken)
            ?? throw new NotFoundException("Plan was not found");

        var actives = await db.ProviderSubscriptions.Where(s => s.ProviderId == providerId && s.IsActive).ToListAsync(cancellationToken);
        foreach (var a in actives) a.IsActive = false;

        var purchaseDate = DateTime.UtcNow;
        var subscription = new ProviderSubscription
        {
            ProviderId = providerId,
            PlanId = plan.Id,
            PurchaseDate = purchaseDate,
            ExpireDate = purchaseDate.AddDays(plan.DurationDays),
            MoneyPaid = request.MoneyPaid,
            IsActive = true
        };
        db.ProviderSubscriptions.Add(subscription);
        await db.SaveChangesAsync(cancellationToken);
        return new CreatedSubscriptionResponse(subscription.Id, subscription.ExpireDate);
    }

    private async Task<Provider> FindProviderAsync(int id, CancellationToken cancellationToken) =>
        await db.Providers.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken)
        ?? throw new NotFoundException("Provider was not found");

    private async Task<string?> ActivePlanNameAsync(int providerId, CancellationToken cancellationToken)
    {
        var active = await db.ProviderSubscriptions.AsNoTracking()
            .Where(s => s.ProviderId == providerId && s.IsActive)
            .OrderByDescending(s => s.ExpireDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (active is null) return null;
        return await db.Plans.AsNoTracking().Where(p => p.Id == active.PlanId).Select(p => p.Name).FirstAsync(cancellationToken);
    }

    private static PagedResponse<T> Page<T>(IReadOnlyList<T> items, int pageNumber, int pageSize, int total) =>
        new(items, pageNumber, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
}
