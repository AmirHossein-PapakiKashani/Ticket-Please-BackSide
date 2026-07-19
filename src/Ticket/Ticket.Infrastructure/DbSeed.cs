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
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await db.Database.MigrateAsync(cancellationToken);

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

        // Phase 6 demo graph (idempotent).
        if (await db.Plans.AnyAsync(p => p.Name == "Demo Gold", cancellationToken))
            return;

        var plan = new Plan
        {
            Name = "Demo Gold",
            DurationDays = 365,
            Price = 1_000_000,
            MaxClientCount = 50,
            MaxAgentCount = 20,
            ModulesJson = "[]",
            IsActive = true
        };
        db.Plans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);

        var provider = new Provider
        {
            Name = "Demo Provider",
            Email = "demo-provider@example.com",
            PhoneNumber = "02100000000",
            IsActive = true
        };
        db.Providers.Add(provider);
        await db.SaveChangesAsync(cancellationToken);

        db.ProviderSubscriptions.Add(new ProviderSubscription
        {
            ProviderId = provider.Id,
            PlanId = plan.Id,
            PurchaseDate = DateTime.UtcNow,
            ExpireDate = DateTime.UtcNow.AddDays(plan.DurationDays),
            MoneyPaid = plan.Price,
            IsActive = true
        });

        var pmRole = 2;
        var agentRole = 3;
        var cmRole = 4;
        var requesterRole = 5;

        db.Users.Add(new User
        {
            Username = "demo.pm",
            FullName = "Demo ProviderManager",
            PhoneNumber = "09120000001",
            PassHash = hasher.Hash("ChangeMe123!"),
            RoleId = pmRole,
            ProviderId = provider.Id
        });
        db.Users.Add(new User
        {
            Username = "demo.agent1",
            FullName = "Demo Agent One",
            PhoneNumber = "09120000002",
            PassHash = hasher.Hash("ChangeMe123!"),
            RoleId = agentRole,
            ProviderId = provider.Id
        });
        db.Users.Add(new User
        {
            Username = "demo.agent2",
            FullName = "Demo Agent Two",
            PhoneNumber = "09120000003",
            PassHash = hasher.Hash("ChangeMe123!"),
            RoleId = agentRole,
            ProviderId = provider.Id
        });

        var client = new Client
        {
            ProviderId = provider.Id,
            Name = "Demo Client",
            Email = "demo-client@example.com",
            PhoneNumber = "02111111111",
            IsActive = true
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync(cancellationToken);

        db.Users.Add(new User
        {
            Username = "demo.cm",
            FullName = "Demo ClientManager",
            PhoneNumber = "09120000004",
            PassHash = hasher.Hash("ChangeMe123!"),
            RoleId = cmRole,
            ClientId = client.Id
        });
        db.Users.Add(new User
        {
            Username = "demo.req1",
            FullName = "Demo Requester One",
            PhoneNumber = "09120000005",
            PassHash = hasher.Hash("ChangeMe123!"),
            RoleId = requesterRole,
            ClientId = client.Id
        });
        db.Users.Add(new User
        {
            Username = "demo.req2",
            FullName = "Demo Requester Two",
            PhoneNumber = "09120000006",
            PassHash = hasher.Hash("ChangeMe123!"),
            RoleId = requesterRole,
            ClientId = client.Id
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
