using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Ticket.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <c>dotnet ef</c> migrations (PostgreSQL only).
/// Prefers env <c>TICKET_DATABASE_URL</c> / <c>DATABASE_URL</c>, then appsettings.
/// </summary>
public sealed class TicketDbContextFactory : IDesignTimeDbContextFactory<TicketDbContext>
{
    public TicketDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Ticket.Api"));
        if (!Directory.Exists(basePath))
            basePath = Directory.GetCurrentDirectory();

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            Environment.GetEnvironmentVariable("TICKET_DATABASE_URL")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? config.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=ticket;Username=ticket;Password=ticket";

        var options = new DbContextOptionsBuilder<TicketDbContext>()
            .UseNpgsql(connectionString);

        return new TicketDbContext(options.Options);
    }
}
