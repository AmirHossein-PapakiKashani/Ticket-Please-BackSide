# Templates — Ticketing System

Aligned with DESIGN-v6 ERD and Backend Guide. Replace placeholders.

---

## Role / enum constants

```csharp
namespace Ticket.Domain;

public static class RoleNames
{
    public const string SuperAdmin = "SuperAdmin";
    public const string ProviderManager = "ProviderManager";
    public const string Agent = "Agent";
    public const string ClientManager = "ClientManager";
    public const string Requester = "Requester";
}
```

```csharp
namespace Ticket.Domain.Enums;

public enum TicketStatus { Open = 1, InProgress = 2, PendingRequesterResponse = 3, Resolved = 4, Closed = 5 }
public enum TicketPriority { Low = 1, Medium = 2, High = 3 }
public enum NotificationType
{
    NewTicketAssigned = 1, NewMessage = 2, TicketReassigned = 3,
    TicketResolved = 4, TicketClosed = 5, TicketReopened = 6
}
```

JSON: enums as **strings**. DB: ints per ERD.

---

## Entity (match DESIGN §5 field names)

```csharp
namespace Ticket.Domain.Entities;

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
```

Soft delete only. Users: `PassHash`, `LastAssignedAt` (Agent), nullable `ProviderId`/`ClientId` per DESIGN §5.4.

---

## DTO / Command / Handler / Controller

Keep openapi property names. Controllers use area routes:

```csharp
[Authorize(Roles = RoleNames.SuperAdmin)]
[Route("api/v1/admin")]
public sealed class AdminController(ISender sender) : ControllerBase { /* thin */ }
```

Pagination query: `pageNumber` (1-based), `pageSize` (1–100), optional `search`.

Create responses: use **201** only when scenarios/openapi say so.

Full CQRS file shapes: same as prior templates in spirit — see `AGENTS.md` §6 and Backend Guide §3 coding order.
