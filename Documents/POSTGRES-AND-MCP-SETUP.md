# PostgreSQL + Cursor MCP setup

SQLite has been removed. The API and EF migrations are **PostgreSQL-only**.

## What is configured

| Piece | Purpose |
|-------|---------|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | EF Core PostgreSQL provider |
| `dotnet-ef` (global tool) | Code-first migrations CLI |
| `TicketDbContextFactory` | Design-time factory for `dotnet ef` |
| Migration `InitialCreate` | Schema bootstrap |
| MCP `postgres` → `@yawlabs/postgres-mcp` | Query/inspect Postgres from Cursor |
| MCP `codebase-memory-mcp` | Code graph (already configured) |
| MCP `microsoft-learn` | Microsoft docs (already configured) |

Unit tests use EF **InMemory** (not SQLite) for speed. Integration/API runs must use a real Postgres.

## 1) Set your connection string (local machine)

Do **not** commit real passwords.

```bash
# PowerShell
$env:TICKET_DATABASE_URL = "Host=localhost;Port=5432;Database=Ticket;Username=ticket;Password=YOUR_PASSWORD"

# bash
export TICKET_DATABASE_URL='Host=localhost;Port=5432;Database=Ticket;Username=ticket;Password=YOUR_PASSWORD'
```

URI form also works for MCP:

```text
postgresql://ticket:YOUR_PASSWORD@localhost:5432/Ticket
```

Default in `appsettings.json` (dev placeholder):

```text
Host=localhost;Port=5432;Database=Ticket;Username=ticket;Password=ticket
```

Project MCP file: `.cursor/mcp.json` maps `DATABASE_URL` from env `TICKET_DATABASE_URL`.

## 2) Enable MCP in Cursor Desktop

1. `git pull` this branch  
2. **Settings → Features → MCP** → enable `postgres`  
3. Restart Cursor  
4. Authenticate **Postman** if it shows `needsAuth`

## 3) Apply migrations

```powershell
export PATH="$PATH:$HOME/.dotnet/tools"

rtk dotnet ef database update `
  --project src/Ticket/Ticket.Infrastructure/Ticket.Infrastructure.csproj `
  --startup-project src/Ticket/Ticket.Api/Ticket.Api.csproj
```

Startup also runs `MigrateAsync` via `DbSeed`.

## 4) Optional Docker Postgres

```bash
docker run --name ticket-pg -e POSTGRES_USER=ticket -e POSTGRES_PASSWORD=ticket -e POSTGRES_DB=ticket -p 5432:5432 -d postgres:16
```

## Security

- Prefer a **test** database for MCP.  
- Never point MCP at production.
