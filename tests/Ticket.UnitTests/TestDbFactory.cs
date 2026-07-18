using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ticket.Application.Abstractions;
using Ticket.Domain;
using Ticket.Infrastructure.Persistence;
using Ticket.Infrastructure.Services;

namespace Ticket.UnitTests;

internal static class TestDbFactory
{
    public static LocalFileStorage Files() => new(Options.Create(new AttachmentOptions()));

    /// <summary>Isolated in-memory EF database (no SQLite). Not a substitute for Postgres integration tests.</summary>
    public static async Task<(TicketDbContext Db, AspNetPasswordHasher Hasher)> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<TicketDbContext>()
            .UseInMemoryDatabase($"ticket-tests-{Guid.NewGuid():N}")
            .Options;
        var db = new TicketDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Roles.AddRange(
            new Role { Id = 1, Name = RoleNames.SuperAdmin },
            new Role { Id = 2, Name = RoleNames.ProviderManager },
            new Role { Id = 3, Name = RoleNames.Agent },
            new Role { Id = 4, Name = RoleNames.ClientManager },
            new Role { Id = 5, Name = RoleNames.Requester });
        await db.SaveChangesAsync();

        return (db, new AspNetPasswordHasher());
    }
}
