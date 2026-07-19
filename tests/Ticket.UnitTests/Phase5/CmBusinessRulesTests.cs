using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Ticket.Api.Controllers;
using Ticket.Application;
using Ticket.Application.DTOs;
using Ticket.Application.Features.Cm;
using Ticket.Application.Features.Pm.Commands;
using Ticket.Domain;
using Ticket.Infrastructure;
using Ticket.Infrastructure.Services;
using Xunit;

namespace Ticket.UnitTests.Phase5;

public sealed class CmBusinessRulesTests
{
    [Fact]
    public async Task CreateRequester_And_CrossClient_ReturnsNotFound()
    {
        var (db, hasher) = await TestDbFactory.CreateAsync();
        await using (db)
        {
            var admin = new EfAdminService(db, hasher);
            var p = await admin.CreateProviderAsync(
                new CreateProviderRequest("Acme", "a@acme.test", "09120000000", "pm.cm5", "PM", "Secret1!"),
                CancellationToken.None);
            var plan = await admin.CreatePlanAsync(
                new CreatePlanRequest("Std", 30, 10, 10, 10, null), CancellationToken.None);
            await admin.CreateSubscriptionAsync(p.ProviderId, new CreateSubscriptionRequest(plan.PlanId, 10), CancellationToken.None);
            var pm = new FakeCurrentUser { ProviderId = p.ProviderId };
            var c1 = await new CreateClientCommandHandler(db, pm, hasher).Handle(
                new CreateClientCommand(new CreateClientRequest("C1", "c1@test.com", "02111111111", "cm1", "CM1", "Secret1!")),
                CancellationToken.None);
            var c2 = await new CreateClientCommandHandler(db, pm, hasher).Handle(
                new CreateClientCommand(new CreateClientRequest("C2", "c2@test.com", "02122222222", "cm2", "CM2", "Secret1!")),
                CancellationToken.None);

            var cm1 = new FakeCurrentUser { RoleName = RoleNames.ClientManager, ClientId = c1.ClientId };
            var created = await new CreateRequesterCommandHandler(db, cm1, hasher).Handle(
                new CreateRequesterCommand(new CreateRequesterRequest("Sara", "sara.cm", "09123333333", "Secret1!")),
                CancellationToken.None);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                new GetRequesterDetailQueryHandler(db, new FakeCurrentUser { RoleName = RoleNames.ClientManager, ClientId = c2.ClientId })
                    .Handle(new GetRequesterDetailQuery(created.UserId), CancellationToken.None));
        }
    }

    [Fact]
    public void ClientManagerController_HasNoTicketWriteRoutes()
    {
        var methods = typeof(ClientManagerController).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.DoesNotContain(methods, m =>
            m.GetCustomAttribute<HttpPostAttribute>() is { } post &&
            (post.Template?.Contains("message", StringComparison.OrdinalIgnoreCase) == true ||
             post.Template?.Contains("tickets", StringComparison.OrdinalIgnoreCase) == true));
        Assert.DoesNotContain(methods, m =>
            m.GetCustomAttribute<HttpPatchAttribute>() is { } patch &&
            (patch.Template?.Contains("status", StringComparison.OrdinalIgnoreCase) == true ||
             patch.Template?.Contains("reopen", StringComparison.OrdinalIgnoreCase) == true ||
             patch.Template?.Contains("reassign", StringComparison.OrdinalIgnoreCase) == true));
    }
}
