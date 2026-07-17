using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticket.Application;
using Ticket.Application.DTOs;
using Ticket.Application.Features.Pm.Commands;
using Ticket.Application.Features.Pm.Queries;
using Ticket.Domain;

namespace Ticket.Api.Controllers;

/// <summary>ProviderManager area routes under /api/v1/pm (Phase 2).</summary>
[ApiController]
[Authorize(Roles = RoleNames.ProviderManager)]
[Route("api/v1/pm")]
public sealed class ProviderManagerController(ISender sender) : ControllerBase
{
    [HttpGet("dashboard/stats")]
    public Task<ProviderDashboardStats> GetDashboardStats(CancellationToken cancellationToken) =>
        sender.Send(new GetPmDashboardStatsQuery(), cancellationToken);

    [HttpGet("agents")]
    public Task<PagedResponse<AgentListItem>> GetAgents(
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        sender.Send(new GetAgentsQuery(search, pageNumber, pageSize), cancellationToken);

    [HttpPost("agents")]
    public async Task<ActionResult<CreatedAgentResponse>> CreateAgent(
        [FromBody] CreateAgentRequest request,
        CancellationToken cancellationToken)
    {
        var created = await sender.Send(new CreateAgentCommand(request), cancellationToken);
        return CreatedAtAction(nameof(GetAgent), new { userId = created.UserId }, created);
    }

    [HttpPatch("agents/{userId:int}/deactivate")]
    public Task<SuccessResponse> DeactivateAgent(int userId, CancellationToken cancellationToken) =>
        sender.Send(new DeactivateAgentCommand(userId), cancellationToken);

    [HttpGet("agents/{userId:int}")]
    public Task<AgentDetail> GetAgent(int userId, CancellationToken cancellationToken) =>
        sender.Send(new GetAgentQuery(userId), cancellationToken);

    [HttpPut("agents/{userId:int}")]
    public Task<SuccessResponse> UpdateAgent(
        int userId,
        [FromBody] UpdateAgentRequest request,
        CancellationToken cancellationToken) =>
        sender.Send(new UpdateAgentCommand(userId, request), cancellationToken);

    [HttpGet("agents/{userId:int}/stats")]
    public Task<AgentTicketStats> GetAgentStats(int userId, CancellationToken cancellationToken) =>
        sender.Send(new GetAgentStatsQuery(userId), cancellationToken);

    [HttpGet("clients")]
    public Task<PagedResponse<ClientListItem>> GetClients(
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        sender.Send(new GetClientsQuery(search, pageNumber, pageSize), cancellationToken);

    [HttpPost("clients")]
    public async Task<ActionResult<CreatedClientResponse>> CreateClient(
        [FromBody] CreateClientRequest request,
        CancellationToken cancellationToken)
    {
        var created = await sender.Send(new CreateClientCommand(request), cancellationToken);
        return CreatedAtAction(nameof(GetClient), new { clientId = created.ClientId }, created);
    }

    [HttpPatch("clients/{clientId:int}/deactivate")]
    public Task<SuccessResponse> DeactivateClient(int clientId, CancellationToken cancellationToken) =>
        sender.Send(new DeactivateClientCommand(clientId), cancellationToken);

    [HttpGet("clients/{clientId:int}")]
    public Task<ClientDetail> GetClient(int clientId, CancellationToken cancellationToken) =>
        sender.Send(new GetClientQuery(clientId), cancellationToken);

    [HttpPut("clients/{clientId:int}")]
    public Task<SuccessResponse> UpdateClient(
        int clientId,
        [FromBody] UpdateClientRequest request,
        CancellationToken cancellationToken) =>
        sender.Send(new UpdateClientCommand(clientId, request), cancellationToken);
}
