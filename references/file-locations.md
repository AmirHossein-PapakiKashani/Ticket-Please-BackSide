# File location rules — Ticketing System

```
src/Ticket/
├── Ticket.Api/
│   ├── Controllers/
│   │   ├── AuthController.cs              → /api/v1/auth
│   │   ├── ProfileController.cs           → /api/v1/profile
│   │   ├── LayoutController.cs            → /api/v1/layout
│   │   ├── NotificationsController.cs     → /api/v1/notifications
│   │   ├── AdminController.cs             → /api/v1/admin
│   │   ├── ProviderManagerController.cs   → /api/v1/pm
│   │   ├── AgentController.cs             → /api/v1/agent
│   │   ├── ClientManagerController.cs     → /api/v1/cm
│   │   └── RequesterController.cs         → /api/v1/requester
│   └── Program.cs
│
├── Ticket.Application/
│   ├── DTOs/
│   ├── Errors/
│   ├── Features/
│   │   └── [Feature]/Commands|Queries/
│   └── Data/IApplicationDbContext.cs
│
├── Ticket.Domain/
│   ├── Entities/          # Providers, Clients, Users, Tickets, … (ERD §5)
│   ├── Enums/             # TicketStatus, TicketPriority, NotificationType
│   └── Services/          # AutoAssignAgent (DESIGN §6)
│
└── Ticket.Infrastructure/
    ├── Persistence/
    └── Services/          # JWT, files, email/SMS stubs
```

## Naming

| Type | Pattern | Example |
|------|---------|---------|
| Command | `[Action][Feature]Command.cs` | `CreateProviderCommand.cs` |
| Handler | `[Action][Feature]CommandHandler.cs` | `CreateProviderCommandHandler.cs` |
| Query | `Get[Feature]sQuery.cs` | `GetProvidersQuery.cs` |
| Controller | Fixed area names above | `ProviderManagerController.cs` |
| Domain service | `[Name]Service` / algorithm | `AutoAssignAgent` |

## Where does this go?

```
Business + DB?        → Features/.../Handlers
HTTP route?           → matching Area Controller only
ERD table?            → Domain/Entities (field names per DESIGN §5)
Auto-Assign?          → Domain/Services + unit tests
JWT / EF / files?     → Infrastructure
```
