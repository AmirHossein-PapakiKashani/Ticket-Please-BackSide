using Microsoft.EntityFrameworkCore;
using Ticket.Application;
using Ticket.Application.DTOs;
using Ticket.Application.Features.AgentArea.Commands;
using Ticket.Application.Features.Pm.Commands;
using Ticket.Application.Features.Requester.Commands;
using Ticket.Domain;
using Ticket.Domain.Enums;
using Ticket.Infrastructure;
using Ticket.Infrastructure.Services;
using Xunit;

namespace Ticket.UnitTests.Phase6;

public sealed class FullCycleBusinessTests
{
    [Fact]
    public async Task FullCycle_Create_Assign_Reply_Reassign_Resolve_Reopen()
    {
        var (db, connection, hasher) = await TestDbFactory.CreateAsync();
        await using (connection)
        await using (db)
        {
            var files = TestDbFactory.Files();
            var admin = new EfAdminService(db, hasher);
            var provider = await admin.CreateProviderAsync(
                new CreateProviderRequest("Acme", "a@acme.test", "09120000000", "pm.e2e", "PM", "Secret1!"),
                CancellationToken.None);
            var plan = await admin.CreatePlanAsync(
                new CreatePlanRequest("Std", 30, 10, 10, 10, null), CancellationToken.None);
            await admin.CreateSubscriptionAsync(provider.ProviderId, new CreateSubscriptionRequest(plan.PlanId, 10), CancellationToken.None);
            var pm = new FakeCurrentUser { ProviderId = provider.ProviderId };
            var a = await new CreateAgentCommandHandler(db, pm, hasher)
                .Handle(new CreateAgentCommand(new CreateAgentRequest("A", "agent.e2e.a", "09121111111", "Secret1!")), CancellationToken.None);
            var b = await new CreateAgentCommandHandler(db, pm, hasher)
                .Handle(new CreateAgentCommand(new CreateAgentRequest("B", "agent.e2e.b", "09122222222", "Secret1!")), CancellationToken.None);
            var client = await new CreateClientCommandHandler(db, pm, hasher).Handle(
                new CreateClientCommand(new CreateClientRequest("C", "c@test.com", "02111111111", "cm.e2e", "CM", "Secret1!")),
                CancellationToken.None);

            var requesterRole = await db.Roles.Where(r => r.Name == RoleNames.Requester).Select(r => r.Id).FirstAsync();
            var requester = new User
            {
                Username = "req.e2e",
                FullName = "Req",
                PhoneNumber = "09123333333",
                PassHash = hasher.Hash("Secret1!"),
                RoleId = requesterRole,
                ClientId = client.ClientId
            };
            db.Users.Add(requester);
            var agentB = await db.Users.FirstAsync(u => u.Id == b.UserId);
            agentB.LastAssignedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var requesterUser = new FakeCurrentUser { UserId = requester.Id, RoleName = RoleNames.Requester, ClientId = client.ClientId };
            var agentA = new FakeCurrentUser { UserId = a.UserId, RoleName = RoleNames.Agent, ProviderId = provider.ProviderId };
            var agentBUser = new FakeCurrentUser { UserId = b.UserId, RoleName = RoleNames.Agent, ProviderId = provider.ProviderId };

            var created = await new CreateTicketCommandHandler(db, requesterUser, files)
                .Handle(new CreateTicketCommand("Cycle topic", "First message", null, null), CancellationToken.None);
            Assert.Equal(a.UserId, (await db.Tickets.SingleAsync(t => t.Id == created.TicketId)).AssignedAgentId);

            await new SendAgentMessageCommandHandler(db, agentA, files)
                .Handle(new SendAgentMessageCommand(created.TicketId, "Agent reply", null), CancellationToken.None);
            await new ReassignTicketCommandHandler(db, agentA)
                .Handle(new ReassignTicketCommand(created.TicketId, b.UserId), CancellationToken.None);
            await new UpdateTicketStatusCommandHandler(db, agentBUser)
                .Handle(new UpdateTicketStatusCommand(created.TicketId, TicketStatus.Resolved), CancellationToken.None);
            await new ReopenTicketCommandHandler(db, requesterUser)
                .Handle(new ReopenTicketCommand(created.TicketId), CancellationToken.None);

            var ticket = await db.Tickets.SingleAsync(t => t.Id == created.TicketId);
            Assert.Equal(TicketStatus.Open, ticket.Status);
            Assert.Equal(b.UserId, ticket.AssignedAgentId);
            Assert.Equal(1, ticket.ReopenCount);
            Assert.True(await db.TicketMessages.CountAsync(m => m.TicketId == ticket.Id) >= 2);
        }
    }
}
