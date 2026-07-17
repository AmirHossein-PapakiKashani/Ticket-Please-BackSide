using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticket.Application.Abstractions;
using Ticket.Domain;
using Ticket.Infrastructure.Persistence;

namespace Ticket.Infrastructure;

public static class DbSeed
{
    public static async Task EnsureSeededAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (!await db.Roles.AnyAsync(cancellationToken))
        {
            db.Roles.AddRange(
                new Role { Id = 1, Name = RoleNames.SuperAdmin, Description = "Platform admin" },
                new Role { Id = 2, Name = RoleNames.ProviderManager, Description = "Provider manager" },
                new Role { Id = 3, Name = RoleNames.Agent, Description = "Support agent" },
                new Role { Id = 4, Name = RoleNames.ClientManager, Description = "Client manager" },
                new Role { Id = 5, Name = RoleNames.Requester, Description = "Ticket requester" });
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!await db.Users.AnyAsync(u => u.Username == "superadmin", cancellationToken))
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            db.Users.Add(new User
            {
                Username = "superadmin",
                FullName = "System SuperAdmin",
                PhoneNumber = "09000000000",
                PassHash = hasher.Hash("ChangeMe123!"),
                RoleId = 1,
                IsActive = true
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
