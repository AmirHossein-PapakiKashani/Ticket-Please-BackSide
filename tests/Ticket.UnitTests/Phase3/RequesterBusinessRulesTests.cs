using Microsoft.EntityFrameworkCore;
using Ticket.Application;
using Ticket.Application.DTOs;
using Ticket.Application.Features.Pm.Commands;
using Ticket.Application.Features.Requester.Commands;
using Ticket.Domain;
using Ticket.Domain.Enums;
using Ticket.Infrastructure;
using Ticket.Infrastructure.Services;
using Xunit;

namespace Ticket.UnitTests.Phase3;

public sealed class RequesterBusinessRulesTests
{
    private static async Task<(int ProviderId, int ClientId, int RequesterId, int AgentId, FakeCurrentUser Requester)> SeedAsync(
        Ticket.Infrastructure.Persistence.TicketDbContext db,
        AspNetPasswordHasher hasher,
        int maxAgents = 5)
    {
        var admin = new EfAdminService(db, hasher);
        var provider = await admin.CreateProviderAsync(
            new CreateProviderRequest("Acme", "a@acme.test", "09120000000", "pm.r3", "PM", "Secret1!"),
            CancellationToken.None);
        var plan = await admin.CreatePlanAsync(
            new CreatePlanRequest("Std", 30, 10, 10, maxAgents, null), CancellationToken.None);
        await admin.CreateSubscriptionAsync(
            provider.ProviderId, new CreateSubscriptionRequest(plan.PlanId, 10), CancellationToken.None);

        var pm = new FakeCurrentUser { ProviderId = provider.ProviderId };
        var agent = await new CreateAgentCommandHandler(db, pm, hasher).Handle(
            new CreateAgentCommand(new CreateAgentRequest("Agent", "agent.r3", "09121111111", "Secret1!")),
            CancellationToken.None);
        var client = await new CreateClientCommandHandler(db, pm, hasher).Handle(
            new CreateClientCommand(new CreateClientRequest(
                "Alpha", "alpha@test.com", "02111111111", "cm.r3", "CM", "Secret1!")),
            CancellationToken.None);

        var requesterRoleId = await db.Roles.Where(r => r.Name == RoleNames.Requester).Select(r => r.Id).FirstAsync();
        var requester = new User
        {
            Username = "req.r3",
            FullName = "Requester One",
            PhoneNumber = "09123333333",
            PassHash = hasher.Hash("Secret1!"),
            RoleId = requesterRoleId,
            ClientId = client.ClientId
        };
        db.Users.Add(requester);
        await db.SaveChangesAsync();

        return (provider.ProviderId, client.ClientId, requester.Id, agent.UserId,
            new FakeCurrentUser { UserId = requester.Id, RoleName = RoleNames.Requester, ClientId = client.ClientId });
    }

    [Fact]
    public async Task CreateTicket_AssignsAgent_AndNotifies()
    {
        var (db, hasher) = await TestDbFactory.CreateAsync();
        await using (db)
        {
            var seed = await SeedAsync(db, hasher);
            var created = await new CreateTicketCommandHandler(db, seed.Requester, TestDbFactory.Files())
                .Handle(new CreateTicketCommand("Login broken", "Cannot enter panel", null, null), CancellationToken.None);

            var ticket = await db.Tickets.SingleAsync(t => t.Id == created.TicketId);
            Assert.Equal(seed.AgentId, ticket.AssignedAgentId);
            Assert.Equal(TicketPriority.Medium, ticket.Priority);
            Assert.True(await db.Notifications.AnyAsync(n =>
                n.UserId == seed.AgentId && n.Type == NotificationType.NewTicketAssigned && n.RefId == ticket.Id));
            Assert.True(await db.TicketMessages.AnyAsync(m => m.TicketId == ticket.Id && m.SenderId == seed.RequesterId));
        }
    }

    [Fact]
    public async Task CreateTicket_NoAgent_LeavesUnassigned()
    {
        var (db, hasher) = await TestDbFactory.CreateAsync();
        await using (db)
        {
            var admin = new EfAdminService(db, hasher);
            var provider = await admin.CreateProviderAsync(
                new CreateProviderRequest("Acme", "a@acme.test", "09120000000", "pm.none", "PM", "Secret1!"),
                CancellationToken.None);
            var plan = await admin.CreatePlanAsync(
                new CreatePlanRequest("Std", 30, 10, 10, 5, null), CancellationToken.None);
            await admin.CreateSubscriptionAsync(
                provider.ProviderId, new CreateSubscriptionRequest(plan.PlanId, 10), CancellationToken.None);
            var client = await new CreateClientCommandHandler(db, new FakeCurrentUser { ProviderId = provider.ProviderId }, hasher)
                .Handle(new CreateClientCommand(new CreateClientRequest(
                    "Alpha", "alpha@test.com", "02111111111", "cm.none", "CM", "Secret1!")), CancellationToken.None);
            var requesterRoleId = await db.Roles.Where(r => r.Name == RoleNames.Requester).Select(r => r.Id).FirstAsync();
            var requester = new User
            {
                Username = "req.none",
                FullName = "R",
                PhoneNumber = "09120000001",
                PassHash = hasher.Hash("Secret1!"),
                RoleId = requesterRoleId,
                ClientId = client.ClientId
            };
            db.Users.Add(requester);
            await db.SaveChangesAsync();

            var created = await new CreateTicketCommandHandler(
                    db,
                    new FakeCurrentUser { UserId = requester.Id, RoleName = RoleNames.Requester, ClientId = client.ClientId },
                    TestDbFactory.Files())
                .Handle(new CreateTicketCommand("Topic ok", "Body text", TicketPriority.High, null), CancellationToken.None);

            var ticket = await db.Tickets.SingleAsync(t => t.Id == created.TicketId);
            Assert.Null(ticket.AssignedAgentId);
            Assert.False(await db.Notifications.AnyAsync());
        }
    }

    [Fact]
    public async Task ColleagueCannotPostMessage_ButCreatorCan()
    {
        var (db, hasher) = await TestDbFactory.CreateAsync();
        await using (db)
        {
            var seed = await SeedAsync(db, hasher);
            var created = await new CreateTicketCommandHandler(db, seed.Requester, TestDbFactory.Files())
                .Handle(new CreateTicketCommand("Topic", "First", null, null), CancellationToken.None);

            var colleagueRoleId = await db.Roles.Where(r => r.Name == RoleNames.Requester).Select(r => r.Id).FirstAsync();
            var colleague = new User
            {
                Username = "req.col",
                FullName = "Colleague",
                PhoneNumber = "09124444444",
                PassHash = hasher.Hash("Secret1!"),
                RoleId = colleagueRoleId,
                ClientId = seed.ClientId
            };
            db.Users.Add(colleague);
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<ForbiddenAppException>(() =>
                new SendRequesterMessageCommandHandler(
                        db,
                        new FakeCurrentUser { UserId = colleague.Id, RoleName = RoleNames.Requester, ClientId = seed.ClientId },
                        TestDbFactory.Files())
                    .Handle(new SendRequesterMessageCommand(created.TicketId, "Hi", null), CancellationToken.None));
        }
    }

    [Fact]
    public async Task Reopen_KeepsSameAgent_AndDoesNotReassign()
    {
        var (db, hasher) = await TestDbFactory.CreateAsync();
        await using (db)
        {
            var seed = await SeedAsync(db, hasher);
            var created = await new CreateTicketCommandHandler(db, seed.Requester, TestDbFactory.Files())
                .Handle(new CreateTicketCommand("Topic", "First", null, null), CancellationToken.None);
            var ticket = await db.Tickets.SingleAsync(t => t.Id == created.TicketId);
            ticket.Status = TicketStatus.Resolved;
            ticket.CloseDate = DateTime.UtcNow;
            await db.SaveChangesAsync();

            await new ReopenTicketCommandHandler(db, seed.Requester)
                .Handle(new ReopenTicketCommand(ticket.Id), CancellationToken.None);

            await db.Entry(ticket).ReloadAsync();
            Assert.Equal(TicketStatus.Open, ticket.Status);
            Assert.Equal(1, ticket.ReopenCount);
            Assert.Null(ticket.CloseDate);
            Assert.Equal(seed.AgentId, ticket.AssignedAgentId);
            Assert.True(await db.Notifications.AnyAsync(n =>
                n.Type == NotificationType.TicketReopened && n.UserId == seed.AgentId));
        }
    }

    [Fact]
    public async Task Reopen_FromOpen_Conflicts()
    {
        var (db, hasher) = await TestDbFactory.CreateAsync();
        await using (db)
        {
            var seed = await SeedAsync(db, hasher);
            var created = await new CreateTicketCommandHandler(db, seed.Requester, TestDbFactory.Files())
                .Handle(new CreateTicketCommand("Topic", "First", null, null), CancellationToken.None);

            await Assert.ThrowsAsync<ConflictException>(() =>
                new ReopenTicketCommandHandler(db, seed.Requester)
                    .Handle(new ReopenTicketCommand(created.TicketId), CancellationToken.None));
        }
    }
}
