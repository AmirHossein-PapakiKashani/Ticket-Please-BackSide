using Ticket.Domain.Enums;

namespace Ticket.Domain;

/// <summary>Role name constants matching DESIGN seed.</summary>
public static class RoleNames
{
    public const string SuperAdmin = "SuperAdmin";
    public const string ProviderManager = "ProviderManager";
    public const string Agent = "Agent";
    public const string ClientManager = "ClientManager";
    public const string Requester = "Requester";
}

/// <summary>Platform role (DESIGN §5.3).</summary>
public sealed class Role
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
}

/// <summary>Provider organization (Nice Mind customer).</summary>
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

/// <summary>Client organization receiving support from a Provider.</summary>
public sealed class Client
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Subscription plan limits and pricing.</summary>
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

/// <summary>Provider subscription to a Plan.</summary>
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

/// <summary>Application user (DESIGN §5.4).</summary>
public sealed class User
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public Role? Role { get; set; }
    public int? ProviderId { get; set; }
    public Provider? Provider { get; set; }
    public int? ClientId { get; set; }
    public Client? Client { get; set; }
    public required string FullName { get; set; }
    public required string Username { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public required string PassHash { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? LastAssignedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string RoleName => Role?.Name ?? string.Empty;
}

/// <summary>Persisted refresh token for rotation.</summary>
public sealed class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One-time password reset token.</summary>
public sealed class PasswordResetToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Token { get; set; }
    public DateTime ExpireAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>In-app notification for a user.</summary>
public sealed class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public NotificationType Type { get; set; }
    public int? RefId { get; set; }
    public required string Title { get; set; }
    public string? Body { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Support ticket (DESIGN §5.7).</summary>
public sealed class Ticket
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public int ProviderId { get; set; }
    public int RequesterId { get; set; }
    public int? AssignedAgentId { get; set; }
    public required string Topic { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public DateTime OpenDate { get; set; } = DateTime.UtcNow;
    public DateTime? SeenByAgentDate { get; set; }
    public DateTime? CloseDate { get; set; }
    public int ReopenCount { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>Chat message on a ticket (DESIGN §5.8).</summary>
public sealed class TicketMessage
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int SenderId { get; set; }
    public required string Text { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SeenAt { get; set; }
    public bool IsDeleted { get; set; }
    public List<Attachment> Attachments { get; set; } = [];
}

/// <summary>File attached to a ticket message (DESIGN §5.9).</summary>
public sealed class Attachment
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public required string FileUrl { get; set; }
    public required string FileName { get; set; }
    public required string FileType { get; set; }
    public int FileSizeKB { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
}

/// <summary>Internal agent note (DESIGN §5.10) — never exposed to Requester.</summary>
public sealed class TicketNote
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public int AuthorId { get; set; }
    public required string Text { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
}
