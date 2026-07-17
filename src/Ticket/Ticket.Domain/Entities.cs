namespace Ticket.Domain;

public static class RoleNames
{
    public const string SuperAdmin = "SuperAdmin";
    public const string ProviderManager = "ProviderManager";
}

public sealed class Provider
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Plan
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int DurationDays { get; set; }
    public decimal Price { get; set; }
    public int MaxClientCount { get; set; }
    public int MaxAgentCount { get; set; }
    public string? ModulesJson { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ProviderSubscription
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public int PlanId { get; set; }
    public DateTime PurchaseDate { get; set; }
    public DateTime ExpireDate { get; set; }
    public decimal MoneyPaid { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string FullName { get; set; }
    public required string PasswordHash { get; set; }
    public required string RoleName { get; set; }
    public int? ProviderId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
