using System.Security.Cryptography;
using System.Text;
using Ticket.Application;
using Ticket.Domain;

namespace Ticket.Infrastructure;

public sealed class InMemoryAdminService : IAdminService
{
    private readonly object _gate = new();
    private readonly List<Provider> _providers = [];
    private readonly List<Plan> _plans = [];
    private readonly List<ProviderSubscription> _subscriptions = [];
    private readonly List<User> _users = [new() { Id = 1, Username = "superadmin", FullName = "System SuperAdmin", PasswordHash = Hash("ChangeMe123!"), RoleName = RoleNames.SuperAdmin }];
    private int _providerId;
    private int _planId;
    private int _subscriptionId;
    private int _userId = 1;

    public Task<AdminDashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            return Task.FromResult(new AdminDashboardStats(_providers.Count(p => !p.IsDeleted), _providers.Count(p => !p.IsDeleted && p.IsActive), 0, 0, _subscriptions.Where(s => s.PurchaseDate >= month).Sum(s => s.MoneyPaid)));
        }
    }

    public Task<PagedResponse<ProviderListItem>> GetProvidersAsync(string? search, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var query = _providers.Where(p => !p.IsDeleted);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || p.Email.Contains(search, StringComparison.OrdinalIgnoreCase));
            var items = query.OrderBy(p => p.Id).Select(p => new ProviderListItem(p.Id, p.Name, p.Email, p.IsActive, ActivePlanName(p.Id), 0));
            return Task.FromResult(Page(items, pageNumber, pageSize));
        }
    }

    public Task<CreatedProviderResponse> CreateProviderAsync(CreateProviderRequest request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_users.Any(u => u.Username.Equals(request.ManagerUsername, StringComparison.OrdinalIgnoreCase))) throw new ConflictException("This username is already in use");
            var provider = new Provider { Id = ++_providerId, Name = request.Name.Trim(), Email = request.Email.Trim(), PhoneNumber = request.PhoneNumber.Trim() };
            _providers.Add(provider);
            _users.Add(new User { Id = ++_userId, Username = request.ManagerUsername.Trim(), FullName = request.ManagerFullName.Trim(), PasswordHash = Hash(request.ManagerPassword), RoleName = RoleNames.ProviderManager, ProviderId = provider.Id });
            return Task.FromResult(new CreatedProviderResponse(provider.Id));
        }
    }

    public Task<SuccessResponse> DeactivateProviderAsync(int providerId, CancellationToken cancellationToken)
    {
        lock (_gate) { FindProvider(providerId).IsActive = false; return Task.FromResult(new SuccessResponse()); }
    }

    public Task<ProviderDetail> GetProviderAsync(int providerId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var provider = FindProvider(providerId);
            var subscription = _subscriptions.Where(s => s.ProviderId == providerId && s.IsActive).OrderByDescending(s => s.ExpireDate).FirstOrDefault();
            var plan = subscription is null ? null : _plans.SingleOrDefault(p => p.Id == subscription.PlanId);
            return Task.FromResult(new ProviderDetail(provider.Name, provider.Email, provider.PhoneNumber, provider.IsActive, plan is null || subscription is null ? null : new SubscriptionSummary(plan.Name, subscription.ExpireDate), 0, _users.Count(u => u.ProviderId == providerId && u.RoleName == "Agent" && u.IsActive && !u.IsDeleted)));
        }
    }

    public Task<SuccessResponse> UpdateProviderAsync(int providerId, UpdateProviderRequest request, CancellationToken cancellationToken)
    {
        lock (_gate) { var p = FindProvider(providerId); p.Name = request.Name.Trim(); p.Email = request.Email.Trim(); p.PhoneNumber = request.PhoneNumber.Trim(); p.IsActive = request.IsActive; return Task.FromResult(new SuccessResponse()); }
    }

    public Task<PagedResponse<PlanListItem>> GetPlansAsync(string? search, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var query = _plans.Where(p => string.IsNullOrWhiteSpace(search) || p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).OrderBy(p => p.Id).Select(p => new PlanListItem(p.Id, p.Name, p.Price, p.DurationDays, p.MaxClientCount, p.MaxAgentCount));
            return Task.FromResult(Page(query, pageNumber, pageSize));
        }
    }

    public Task<CreatedPlanResponse> CreatePlanAsync(CreatePlanRequest request, CancellationToken cancellationToken)
    {
        lock (_gate) { var plan = NewPlan(request); _plans.Add(plan); return Task.FromResult(new CreatedPlanResponse(plan.Id)); }
    }

    public Task<SuccessResponse> UpdatePlanAsync(int planId, UpdatePlanRequest request, CancellationToken cancellationToken)
    {
        lock (_gate) { var p = _plans.SingleOrDefault(p => p.Id == planId) ?? throw new NotFoundException("Plan was not found"); p.Name = request.Name.Trim(); p.DurationDays = request.DurationDays; p.Price = request.Price; p.MaxClientCount = request.MaxClientCount; p.MaxAgentCount = request.MaxAgentCount; p.ModulesJson = request.ModulesJson; return Task.FromResult(new SuccessResponse()); }
    }

    public Task<SuccessResponse> DeactivatePlanAsync(int planId, CancellationToken cancellationToken)
    {
        lock (_gate) { (_plans.SingleOrDefault(p => p.Id == planId) ?? throw new NotFoundException("Plan was not found")).IsActive = false; return Task.FromResult(new SuccessResponse()); }
    }

    public Task<PagedResponse<SubscriptionListItem>> GetSubscriptionsAsync(int providerId, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        lock (_gate) { FindProvider(providerId); var values = _subscriptions.Where(s => s.ProviderId == providerId).OrderByDescending(s => s.PurchaseDate).Select(s => new SubscriptionListItem(_plans.Single(p => p.Id == s.PlanId).Name, s.PurchaseDate, s.ExpireDate, s.MoneyPaid, s.IsActive)); return Task.FromResult(Page(values, pageNumber, pageSize)); }
    }

    public Task<CreatedSubscriptionResponse> CreateSubscriptionAsync(int providerId, CreateSubscriptionRequest request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            FindProvider(providerId);
            var plan = _plans.SingleOrDefault(p => p.Id == request.PlanId) ?? throw new NotFoundException("Plan was not found");
            foreach (var active in _subscriptions.Where(s => s.ProviderId == providerId && s.IsActive)) active.IsActive = false;
            var purchaseDate = DateTime.UtcNow;
            var subscription = new ProviderSubscription { Id = ++_subscriptionId, ProviderId = providerId, PlanId = plan.Id, PurchaseDate = purchaseDate, ExpireDate = purchaseDate.AddDays(plan.DurationDays), MoneyPaid = request.MoneyPaid };
            _subscriptions.Add(subscription);
            return Task.FromResult(new CreatedSubscriptionResponse(subscription.Id, subscription.ExpireDate));
        }
    }

    private Provider FindProvider(int id) => _providers.SingleOrDefault(p => p.Id == id && !p.IsDeleted) ?? throw new NotFoundException("Provider was not found");
    private Plan NewPlan(CreatePlanRequest r) => new() { Id = ++_planId, Name = r.Name.Trim(), DurationDays = r.DurationDays, Price = r.Price, MaxClientCount = r.MaxClientCount, MaxAgentCount = r.MaxAgentCount, ModulesJson = r.ModulesJson };
    private string? ActivePlanName(int providerId) { var active = _subscriptions.Where(s => s.ProviderId == providerId && s.IsActive).OrderByDescending(s => s.ExpireDate).FirstOrDefault(); return active is null ? null : _plans.Single(p => p.Id == active.PlanId).Name; }
    private static PagedResponse<T> Page<T>(IEnumerable<T> source, int pageNumber, int pageSize) { var list = source.ToList(); return new PagedResponse<T>(list.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(), pageNumber, pageSize, list.Count, (int)Math.Ceiling(list.Count / (double)pageSize)); }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
