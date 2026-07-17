using Microsoft.EntityFrameworkCore;
using Ticket.Application;
using Ticket.Application.DTOs;
using Ticket.Application.Features.AgentArea.Commands;
using Ticket.Application.Features.Pm.Commands;
using Ticket.Application.Features.Requester.Commands;
using Ticket.Application.Features.Requester.Queries;
using Ticket.Domain;
using Ticket.Domain.Enums;
using Ticket.Infrastructure;
using Ticket.Infrastructure.Services;
using Xunit;

namespace Ticket.UnitTests.Phase4;

public sealed class AgentBusinessRulesTests
{
    private static async Task<(int TicketId, FakeCurrentUser AgentA, FakeCurrentUser AgentB, FakeCurrentUser Requester)> SeedTwoAgentsAsync(
        Ticket.Infrastructure.Persistence.TicketDbContext db,
        AspNetPasswordHasher hasher)
    {
        var admin = new EfAdminService(db, hasher);
        var provider = await admin.CreateProviderAsync(
            new CreateProviderRequest("Acme", "a@acme.test", "09120000000", "pm.a4", "PM", "Secret1!"),
            CancellationToken.None);
        var plan = await admin.CreatePlanAsync(
            new CreatePlanRequest("Std", 30, 10, 10, 10, null), CancellationToken.None);
        await admin.CreateSubscriptionAsync(provider.ProviderId, new CreateSubscriptionRequest(plan.PlanId, 10), CancellationToken.None);
        var pm = new FakeCurrentUser { ProviderId = provider.ProviderId };
        var a = await new CreateAgentCommandHandler(db, pm, hasher)
            .Handle(new CreateAgentCommand(new CreateAgentRequest("A", "agent.a", "09121111111", "Secret1!")), CancellationToken.None);
        var b = await new CreateAgentCommandHandler(db, pm, hasher)
            .Handle(new CreateAgentCommand(new CreateAgentRequest("B", "agent.b", "09122222222", "Secret1!")), CancellationToken.None);
        var client = await new CreateClientCommandHandler(db, pm, hasher).Handle(
            new CreateClientCommand(new CreateClientRequest("C", "c@test.com", "02111111111", "cm.a4", "CM", "Secret1!")),
            CancellationToken.None);
        var requesterRole = await db.Roles.Where(r => r.Name == RoleNames.Requester).Select(r => r.Id).FirstAsync();
        var requester = new User
        {
            Username = "req.a4",
            FullName = "Req",
            PhoneNumber = "09123333333",
            PassHash = hasher.Hash("Secret1!"),
            RoleId = requesterRole,
            ClientId = client.ClientId
        };
        db.Users.Add(requester);
        await db.SaveChangesAsync();

        var requesterUser = new FakeCurrentUser { UserId = requester.Id, RoleName = RoleNames.Requester, ClientId = client.ClientId };
        // Prefer agent A by setting B already "busier" via LastAssignedAt and creating ticket when both free — force assign to A.
        var agentA = await db.Users.FirstAsync(u => u.Id == a.UserId);
        var agentB = await db.Users.FirstAsync(u => u.Id == b.UserId);
        agentB.LastAssignedAt = DateTime.UtcNow;
        agentA.LastAssignedAt = null;
        await db.SaveChangesAsync();

        var created = await new CreateTicketCommandHandler(db, requesterUser, TestDbFactory.Files())
            .Handle(new CreateTicketCommand("Topic", "Body", null, null), CancellationToken.None);

        return (
            created.TicketId,
            new FakeCurrentUser { UserId = a.UserId, RoleName = RoleNames.Agent, ProviderId = provider.ProviderId },
            new FakeCurrentUser { UserId = b.UserId, RoleName = RoleNames.Agent, ProviderId = provider.ProviderId },
            requesterUser);
    }

    [Fact]
    public async Task Reassign_TransfersOwnership_PreviousAgentCannotMessage()
    {
        var (db, connection, hasher) = await TestDbFactory.CreateAsync();
        await using (connection)
        await using (db)
        {
            var seed = await SeedTwoAgentsAsync(db, hasher);
            await new ReassignTicketCommandHandler(db, seed.AgentA)
                .Handle(new ReassignTicketCommand(seed.TicketId, seed.AgentB.UserId), CancellationToken.None);

            var ticket = await db.Tickets.SingleAsync(t => t.Id == seed.TicketId);
            Assert.Equal(seed.AgentB.UserId, ticket.AssignedAgentId);
            Assert.Null(ticket.SeenByAgentDate);
            Assert.True(await db.Notifications.AnyAsync(n =>
                n.UserId == seed.AgentB.UserId && n.Type == NotificationType.TicketReassigned));

            await Assert.ThrowsAsync<ForbiddenAppException>(() =>
                new SendAgentMessageCommandHandler(db, seed.AgentA, TestDbFactory.Files())
                    .Handle(new SendAgentMessageCommand(seed.TicketId, "hi", null), CancellationToken.None));
        }
    }

    [Fact]
    public async Task Resolve_NotifiesRequester_AndNotesHiddenFromRequesterMessages()
    {
        var (db, connection, hasher) = await TestDbFactory.CreateAsync();
        await using (connection)
        await using (db)
        {
            var seed = await SeedTwoAgentsAsync(db, hasher);
            await new CreateTicketNoteCommandHandler(db, seed.AgentA)
                .Handle(new CreateTicketNoteCommand(seed.TicketId, "internal only"), CancellationToken.None);
            await new UpdateTicketStatusCommandHandler(db, seed.AgentA)
                .Handle(new UpdateTicketStatusCommand(seed.TicketId, TicketStatus.Resolved), CancellationToken.None);

            var ticket = await db.Tickets.SingleAsync(t => t.Id == seed.TicketId);
            Assert.Equal(TicketStatus.Resolved, ticket.Status);
            Assert.NotNull(ticket.CloseDate);
            Assert.True(await db.Notifications.AnyAsync(n =>
                n.UserId == seed.Requester.UserId && n.Type == NotificationType.TicketResolved));

            var messages = await new GetRequesterMessagesQueryHandler(db, seed.Requester)
                .Handle(new GetRequesterMessagesQuery(seed.TicketId, 1, 20), CancellationToken.None);
            Assert.DoesNotContain(messages.Items, m => m.Text.Contains("internal only"));
        }
    }
}
