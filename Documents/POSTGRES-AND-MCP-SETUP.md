# PostgreSQL + Cursor MCP setup

## What was installed

| Piece | Purpose |
|-------|---------|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | EF Core PostgreSQL provider |
| `dotnet-ef` (global tool) | Code-first migrations CLI |
| `TicketDbContextFactory` | Design-time factory for `dotnet ef` |
| MCP `postgres` → `@yawlabs/postgres-mcp` | Query/inspect Postgres from Cursor (maintained; replaces deprecated `@modelcontextprotocol/server-postgres`) |
| MCP `codebase-memory-mcp` | Code graph (already configured) |
| MCP `microsoft-learn` | Microsoft docs (already configured) |

## 1) Set your connection string (local machine)

Do **not** commit real passwords.

```bash
# PowerShell
$env:TICKET_DATABASE_URL = "Host=localhost;Port=5432;Database=ticket;Username=ticket;Password=YOUR_PASSWORD"

# bash
export TICKET_DATABASE_URL='Host=localhost;Port=5432;Database=ticket;Username=ticket;Password=YOUR_PASSWORD'
```

URI form also works for MCP:

```text
postgresql://ticket:YOUR_PASSWORD@localhost:5432/ticket
```

Copy `.env.example` → `.env` if you use a dotenv loader. For Cursor MCP, set the same value in:

**Cursor Settings → MCP → `postgres` → env `DATABASE_URL` / `TICKET_DATABASE_URL`**

Project file: `.cursor/mcp.json` maps MCP `DATABASE_URL` from your shell env `TICKET_DATABASE_URL`.

If the green light stays red, open `.cursor/mcp.json` and temporarily paste a **test** URI into `env.DATABASE_URL` (do not commit secrets), or set the variable in Cursor MCP UI.

## 2) Enable MCP in Cursor Desktop (required on your laptop)

Cloud agents cannot flip Desktop MCP toggles for you. On **your** Cursor:

1. `git pull` this branch  
2. **Settings → Features → MCP**  
3. Enable `postgres` (and re-auth **Postman** if shown as needsAuth)  
4. Restart Cursor  
5. Confirm Node.js is on PATH (`node` / `npx`)

`codebase-memory-mcp` only works if that binary is installed on the machine Cursor runs on.

## 3) EF migrations (code-first)

```powershell
export PATH="$PATH:$HOME/.dotnet/tools"

rtk err dotnet ef migrations add InitialCreate `
  --project src/Ticket/Ticket.Infrastructure/Ticket.Infrastructure.csproj `
  --startup-project src/Ticket/Ticket.Api/Ticket.Api.csproj `
  --output-dir Persistence/Migrations

rtk err dotnet ef database update `
  --project src/Ticket/Ticket.Infrastructure/Ticket.Infrastructure.csproj `
  --startup-project src/Ticket/Ticket.Api/Ticket.Api.csproj
```

Provider is chosen automatically:

- connection looks like Postgres → `UseNpgsql`
- otherwise → `UseSqlite` (local default `Data Source=Ticket.db`)

## 4) Optional Docker Postgres

```bash
docker run --name ticket-pg -e POSTGRES_USER=ticket -e POSTGRES_PASSWORD=ticket -e POSTGRES_DB=ticket -p 5432:5432 -d postgres:16
```

Then:

```text
TICKET_DATABASE_URL=Host=localhost;Port=5432;Database=ticket;Username=ticket;Password=ticket
```

## Security

- Prefer a **test** database for MCP (read-write tools exist).  
- Never point MCP at production.  
- Prefer a DB role with limited privileges for agents.
