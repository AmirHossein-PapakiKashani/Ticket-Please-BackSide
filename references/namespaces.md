# Namespace rules — Ticketing System

| Location | Namespace |
|----------|-----------|
| Entities | `Ticket.Domain.Entities` |
| Enums | `Ticket.Domain.Enums` |
| Domain services | `Ticket.Domain.Services` |
| DTOs | `Ticket.Application.DTOs` |
| Errors | `Ticket.Application.Errors` |
| Commands | `Ticket.Application.Features.[Feature].Commands` |
| Queries | `Ticket.Application.Features.[Feature].Queries` |
| Controllers | `Ticket.Api.Controllers` |
| Infrastructure | `Ticket.Infrastructure.[Area]` |

## Naming locked by DESIGN glossary

Use **Provider**, **Client**, **Agent**, **Requester**, **ProviderManager**, **ClientManager**, **SuperAdmin** only.

## Folder spelling

`Commands` / `Queries` (correct English). Single solution — no Elay-style Admin/Customer bounded-context prefixes.
