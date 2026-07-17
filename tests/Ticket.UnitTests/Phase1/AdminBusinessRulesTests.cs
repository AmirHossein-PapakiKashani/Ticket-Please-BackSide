using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ticket.Application;
using Ticket.Application.DTOs;
using Ticket.Application.Features.Auth.Commands;
using Ticket.Domain;
using Ticket.Infrastructure;
using Ticket.Infrastructure.Services;
using Xunit;

namespace Ticket.UnitTests.Phase1;

public sealed class AdminBusinessRulesTests
{
    [Fact]
    public async Task CreateProvider_CreatesProviderAndProviderManager_InOnePersist()
    {
        var (db, connection, hasher) = await TestDbFactory.CreateAsync();
        await using (connection)
        await using (db)
        {
            var admin = new EfAdminService(db, hasher);
            var created = await admin.CreateProviderAsync(
                new CreateProviderRequest("Acme", "a@acme.test", "09120000000", "pm.acme", "PM Acme", "Secret1!"),
                CancellationToken.None);

            Assert.True(created.ProviderId > 0);
            var provider = await db.Providers.SingleAsync(p => p.Id == created.ProviderId);
            var manager = await db.Users.Include(u => u.Role).SingleAsync(u => u.Username == "pm.acme");
            Assert.Equal("Acme", provider.Name);
            Assert.Equal(RoleNames.ProviderManager, manager.Role!.Name);
            Assert.Equal(provider.Id, manager.ProviderId);
        }
    }

    [Fact]
    public async Task CreateProvider_DuplicateManagerUsername_ThrowsConflict()
    {
        var (db, connection, hasher) = await TestDbFactory.CreateAsync();
        await using (connection)
        await using (db)
        {
            var admin = new EfAdminService(db, hasher);
            var request = new CreateProviderRequest("Acme", "a@acme.test", "09120000000", "pm.dup", "PM", "Secret1!");
            await admin.CreateProviderAsync(request, CancellationToken.None);

            await Assert.ThrowsAsync<ConflictException>(() =>
                admin.CreateProviderAsync(
                    request with { Name = "Other", Email = "o@acme.test" },
                    CancellationToken.None));
        }
    }

    [Fact]
    public async Task CreateSubscription_DeactivatesPreviousActiveSubscription()
    {
        var (db, connection, hasher) = await TestDbFactory.CreateAsync();
        await using (connection)
        await using (db)
        {
            var admin = new EfAdminService(db, hasher);
            var provider = await admin.CreateProviderAsync(
                new CreateProviderRequest("Acme", "a@acme.test", "09120000000", "pm.sub", "PM", "Secret1!"),
                CancellationToken.None);
            var planA = await admin.CreatePlanAsync(
                new CreatePlanRequest("Basic", 30, 100, 5, 5, null), CancellationToken.None);
            var planB = await admin.CreatePlanAsync(
                new CreatePlanRequest("Pro", 30, 200, 10, 10, null), CancellationToken.None);

            var first = await admin.CreateSubscriptionAsync(
                provider.ProviderId, new CreateSubscriptionRequest(planA.PlanId, 100), CancellationToken.None);
            var second = await admin.CreateSubscriptionAsync(
                provider.ProviderId, new CreateSubscriptionRequest(planB.PlanId, 200), CancellationToken.None);

            var subs = await db.ProviderSubscriptions.Where(s => s.ProviderId == provider.ProviderId).ToListAsync();
            Assert.Equal(2, subs.Count);
            Assert.False(subs.Single(s => s.Id == first.SubscriptionId).IsActive);
            Assert.True(subs.Single(s => s.Id == second.SubscriptionId).IsActive);
        }
    }

    [Fact]
    public async Task DeactivatedProvider_BlocksProviderManagerLogin()
    {
        var (db, connection, hasher) = await TestDbFactory.CreateAsync();
        await using (connection)
        await using (db)
        {
            var admin = new EfAdminService(db, hasher);
            var created = await admin.CreateProviderAsync(
                new CreateProviderRequest("Acme", "a@acme.test", "09120000000", "pm.lock", "PM", "Secret1!"),
                CancellationToken.None);
            await admin.DeactivateProviderAsync(created.ProviderId, CancellationToken.None);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "Ticket.Api",
                ["Jwt:Audience"] = "Ticket.Client",
                ["Jwt:SigningKey"] = "unit-test-signing-key-please-change-32b",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7"
            }).Build();
            var handler = new LoginCommandHandler(db, hasher, new JwtTokenService(config), config);

            await Assert.ThrowsAsync<ForbiddenAppException>(() =>
                handler.Handle(new LoginCommand(new LoginRequest("pm.lock", "Secret1!")), CancellationToken.None));
        }
    }
}
