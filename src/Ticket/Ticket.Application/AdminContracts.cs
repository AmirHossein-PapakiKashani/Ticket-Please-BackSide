namespace Ticket.Application;

public sealed record CreatePlanRequest(string Name, int DurationDays, decimal Price, int MaxClientCount, int MaxAgentCount, string? ModulesJson);
public sealed record UpdatePlanRequest(string Name, int DurationDays, decimal Price, int MaxClientCount, int MaxAgentCount, string? ModulesJson);
public sealed record CreateProviderRequest(string Name, string Email, string PhoneNumber, string ManagerUsername, string ManagerFullName, string ManagerPassword);
public sealed record UpdateProviderRequest(string Name, string Email, string PhoneNumber, bool IsActive);
public sealed record CreateSubscriptionRequest(int PlanId, decimal MoneyPaid);

public sealed record CreatedPlanResponse(int PlanId);
public sealed record CreatedProviderResponse(int ProviderId);
public sealed record CreatedSubscriptionResponse(int SubscriptionId, DateTime ExpireDate);
public sealed record SuccessResponse(bool Success = true);
public sealed record AdminDashboardStats(int TotalProviders, int ActiveProviders, int TotalClients, int TotalTicketsThisMonth, decimal RevenueThisMonth);
public sealed record ProviderListItem(int Id, string Name, string Email, bool IsActive, string? PlanName, int ClientCount);
public sealed record SubscriptionSummary(string PlanName, DateTime ExpireDate);
public sealed record ProviderDetail(string Name, string Email, string Phone, bool IsActive, SubscriptionSummary? Subscription, int ClientCount, int AgentCount);
public sealed record PlanListItem(int Id, string Name, decimal Price, int DurationDays, int MaxClientCount, int MaxAgentCount);
public sealed record SubscriptionListItem(string PlanName, DateTime PurchaseDate, DateTime ExpireDate, decimal MoneyPaid, bool IsActive);
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount, int TotalPages);

public interface IAdminService
{
    Task<AdminDashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken);
    Task<PagedResponse<ProviderListItem>> GetProvidersAsync(string? search, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<CreatedProviderResponse> CreateProviderAsync(CreateProviderRequest request, CancellationToken cancellationToken);
    Task<SuccessResponse> DeactivateProviderAsync(int providerId, CancellationToken cancellationToken);
    Task<ProviderDetail> GetProviderAsync(int providerId, CancellationToken cancellationToken);
    Task<SuccessResponse> UpdateProviderAsync(int providerId, UpdateProviderRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<PlanListItem>> GetPlansAsync(string? search, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<CreatedPlanResponse> CreatePlanAsync(CreatePlanRequest request, CancellationToken cancellationToken);
    Task<SuccessResponse> UpdatePlanAsync(int planId, UpdatePlanRequest request, CancellationToken cancellationToken);
    Task<SuccessResponse> DeactivatePlanAsync(int planId, CancellationToken cancellationToken);
    Task<PagedResponse<SubscriptionListItem>> GetSubscriptionsAsync(int providerId, int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<CreatedSubscriptionResponse> CreateSubscriptionAsync(int providerId, CreateSubscriptionRequest request, CancellationToken cancellationToken);
}

public sealed class NotFoundException(string detail) : Exception(detail);
public sealed class ConflictException(string detail) : Exception(detail);
