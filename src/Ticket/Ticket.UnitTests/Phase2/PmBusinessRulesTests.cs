using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ticket.Application;
using Ticket.Application.DTOs;
using Ticket.Application.Features.Auth.Commands;
using Ticket.Application.Features.Pm.Commands;
using Ticket.Application.Features.Pm.Queries;
using Ticket.Domain;
using Ticket.Infrastructure;
using Ticket.Infrastructure.Services;
using Xunit;

namespace Ticket.UnitTests.Phase2;

public sealed class PmBusinessRulesTests
{
    [Fact]
    public async Task CreateAgent_EnforcesMaxAgentCount()
    {
        var (db, hasher) = await TestDbFactory.CreateAsync();
        await using (db)
        {
            var admin = new EfAdminService(db, hasher);
            var provider = await admin.CreateProviderAsync(
                new CreateProviderRequest("Acme", "a@acme.test", "09120000000", "pm.maxa", "PM", "Secret1!"),
                CancellationToken.None);
            var plan = await admin.CreatePlanAsync(
                new CreatePlanRequest("Tiny", 30, 10, 5, 1, null), CancellationToken.None);
            await admin.CreateSubscriptionAsync(
                provider.ProviderId, new CreateSubscriptionRequest(plan.PlanId, 10), CancellationToken.None);

            var current = new FakeCurrentUser { ProviderId = provider.ProviderId };
            var create = new CreateAgentCommandHandler(db, current, hasher);
            await create.Handle(
                new CreateAgentCommand(new CreateAgentRequest("A1", "agent1", "09121111111", "Secret1!")),
                CancellationToken.None);

            var ex = await Assert.ThrowsAsync<BadRequestAppException>(() =>
                create.Handle(
                    new CreateAgentCommand(new CreateAgentRequest("A2", "agent2", "09122222222", "Secret1!")),
                    CancellationToken.None));
            Assert.Contains("maximum number of agents", ex.Message);
        }
    }

    [Fact]
    public async Task CreateClient_CreatesClientManager_AndEnforcesMaxClientCount()
    {
        var (db, hasher) = await TestDbFactory.CreateAsync();
        await using (db)
        {
            var admin = new EfAdminService(db, hasher);
            var provider = await admin.CreateProviderAsync(
                new CreateProviderRequest("Acme", "a@acme.test", "09120000000", "pm.maxc", "PM", "Secret1!"),
                CancellationToken.None);
            var plan = await admin.CreatePlanAsync(
                new CreatePlanRequest("Tiny", 30, 10, 1, 5, null), CancellationToken.None);
            await admin.CreateSubscriptionAsync(
                provider.ProviderId, new CreateSubscriptionRequest(plan.PlanId, 10), CancellationToken.None);

            var current = new FakeCurrentUser { ProviderId = provider.ProviderId };
            var create = new CreateClientCommandHandler(db, current, hasher);
            var created = await create.Handle(
                new CreateClientCommand(new CreateClientRequest(
                    "Alpha", "alpha@test.com", "02111111111", "cm.alpha", "CM Alpha", "Secret1!")),
                CancellationToken.None);

            var cm = await db.Users.Include(u => u.Role).SingleAsync(u => u.Username == "cm.alpha");
            Assert.Equal(RoleNames.ClientManager, cm.Role!.Name);
            Assert.Equal(created.ClientId, cm.ClientId);

            var ex = await Assert.ThrowsAsync<BadRequestAppException>(() =>
                create.Handle(
                    new CreateClientCommand(new CreateClientRequest(
                        "Beta", "beta@test.com", "02122222222", "cm.beta", "CM Beta", "Secret1!")),
                    CancellationToken.None));
            Assert.Contains("maximum number of clients", ex.Message);
        }
    }

    [Fact]
    public async Task GetAgent_FromOtherProvider_ReturnsNotFound()
    {
        var (db, hasher) = await TestDbFactory.CreateAsync();
        await using (db)
        {
            var admin = new EfAdminService(db, hasher);
            var p1 = await admin.CreateProviderAsync(
                new CreateProviderRequest("P1", "p1@test.com", "09120000001", "pm.p1", "PM1", "Secret1!"),
                CancellationToken.None);
            var p2 = await admin.CreateProviderAsync(
                new CreateProviderRequest("P2", "p2@test.com", "09120000002", "pm.p2", "PM2", "Secret1!"),
                CancellationToken.None);
            var plan = await admin.CreatePlanAsync(
                new CreatePlanRequest("Std", 30, 10, 5, 5, null), CancellationToken.None);
            await admin.CreateSubscriptionAsync(p1.ProviderId, new CreateSubscriptionRequest(plan.PlanId, 10), CancellationToken.None);

            var agent = await new CreateAgentCommandHandler(db, new FakeCurrentUser { ProviderId = p1.ProviderId }, hasher)
                .Handle(new CreateAgentCommand(new CreateAgentRequest("A", "agent.x", "09123333333", "Secret1!")), CancellationToken.None);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                new GetAgentQueryHandler(db, new FakeCurrentUser { ProviderId = p2.ProviderId })
                    .Handle(new GetAgentQuery(agent.UserId), CancellationToken.None));
        }
    }

    [Fact]
    public async Task DeactivateClient_BlocksClientManagerLogin()
    {
        var (db, hasher) = await TestDbFactory.CreateAsync();
        await using (db)
        {
            var admin = new EfAdminService(db, hasher);
            var provider = await admin.CreateProviderAsync(
                new CreateProviderRequest("Acme", "a@acme.test", "09120000000", "pm.dcl", "PM", "Secret1!"),
                CancellationToken.None);
            var plan = await admin.CreatePlanAsync(
                new CreatePlanRequest("Std", 30, 10, 5, 5, null), CancellationToken.None);
            await admin.CreateSubscriptionAsync(
                provider.ProviderId, new CreateSubscriptionRequest(plan.PlanId, 10), CancellationToken.None);

            var current = new FakeCurrentUser { ProviderId = provider.ProviderId };
            var created = await new CreateClientCommandHandler(db, current, hasher).Handle(
                new CreateClientCommand(new CreateClientRequest(
                    "Alpha", "alpha@test.com", "02111111111", "cm.lock", "CM", "Secret1!")),
                CancellationToken.None);
            await new DeactivateClientCommandHandler(db, current)
                .Handle(new DeactivateClientCommand(created.ClientId), CancellationToken.None);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "Ticket.Api",
                ["Jwt:Audience"] = "Ticket.Client",
                ["Jwt:SigningKey"] = "unit-test-signing-key-please-change-32b",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7"
            }).Build();

            await Assert.ThrowsAsync<ForbiddenAppException>(() =>
                new LoginCommandHandler(db, hasher, new JwtTokenService(config), config)
                    .Handle(new LoginCommand(new LoginRequest("cm.lock", "Secret1!")), CancellationToken.None));
        }
    }

    [Fact]
    public async Task Dashboard_IsScopedToProvider()
    {
        var (db, hasher) = await TestDbFactory.CreateAsync();
        await using (db)
        {
            var admin = new EfAdminService(db, hasher);
            var p1 = await admin.CreateProviderAsync(
                new CreateProviderRequest("P1", "p1@test.com", "09120000001", "pm.d1", "PM1", "Secret1!"),
                CancellationToken.None);
            var p2 = await admin.CreateProviderAsync(
                new CreateProviderRequest("P2", "p2@test.com", "09120000002", "pm.d2", "PM2", "Secret1!"),
                CancellationToken.None);
            var plan = await admin.CreatePlanAsync(
                new CreatePlanRequest("Std", 30, 10, 10, 10, null), CancellationToken.None);
            await admin.CreateSubscriptionAsync(p1.ProviderId, new CreateSubscriptionRequest(plan.PlanId, 10), CancellationToken.None);
            await admin.CreateSubscriptionAsync(p2.ProviderId, new CreateSubscriptionRequest(plan.PlanId, 10), CancellationToken.None);

            await new CreateAgentCommandHandler(db, new FakeCurrentUser { ProviderId = p1.ProviderId }, hasher)
                .Handle(new CreateAgentCommand(new CreateAgentRequest("A", "a1", "09121111111", "Secret1!")), CancellationToken.None);
            await new CreateClientCommandHandler(db, new FakeCurrentUser { ProviderId = p2.ProviderId }, hasher)
                .Handle(new CreateClientCommand(new CreateClientRequest(
                    "C2", "c2@test.com", "02122222222", "cm2", "CM2", "Secret1!")), CancellationToken.None);

            var stats = await new GetPmDashboardStatsQueryHandler(db, new FakeCurrentUser { ProviderId = p1.ProviderId })
                .Handle(new GetPmDashboardStatsQuery(), CancellationToken.None);

            Assert.Equal(1, stats.TotalAgents);
            Assert.Equal(0, stats.TotalClients);
        }
    }
}
