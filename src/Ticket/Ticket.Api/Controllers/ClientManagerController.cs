using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticket.Application;
using Ticket.Application.DTOs;
using Ticket.Application.Features.Cm;
using Ticket.Domain;
using Ticket.Domain.Enums;

namespace Ticket.Api.Controllers;

/// <summary>ClientManager area under /api/v1/cm (Phase 5). No ticket write/chat routes.</summary>
[ApiController]
[Authorize(Roles = RoleNames.ClientManager)]
[Route("api/v1/cm")]
public sealed class ClientManagerController(ISender sender) : ControllerBase
{
    [HttpGet("dashboard/stats")]
    public Task<ClientDashboardStats> GetDashboardStats(CancellationToken cancellationToken) =>
        sender.Send(new GetCmDashboardStatsQuery(), cancellationToken);

    [HttpGet("requesters")]
    public Task<PagedResponse<RequesterListItem>> GetRequesters(
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        sender.Send(new GetRequestersQuery(search, pageNumber, pageSize), cancellationToken);

    [HttpPost("requesters")]
    public async Task<ActionResult<CreatedRequesterResponse>> CreateRequester(
        [FromBody] CreateRequesterRequest request,
        CancellationToken cancellationToken)
    {
        var created = await sender.Send(new CreateRequesterCommand(request), cancellationToken);
        return CreatedAtAction(nameof(GetRequester), new { userId = created.UserId }, created);
    }

    [HttpPatch("requesters/{userId:int}/deactivate")]
    public Task<SuccessResponse> DeactivateRequester(int userId, CancellationToken cancellationToken) =>
        sender.Send(new DeactivateRequesterCommand(userId), cancellationToken);

    [HttpGet("requesters/{userId:int}")]
    public Task<RequesterDetailDto> GetRequester(int userId, CancellationToken cancellationToken) =>
        sender.Send(new GetRequesterDetailQuery(userId), cancellationToken);

    [HttpPut("requesters/{userId:int}")]
    public Task<SuccessResponse> UpdateRequester(
        int userId,
        [FromBody] UpdateRequesterRequest request,
        CancellationToken cancellationToken) =>
        sender.Send(new UpdateRequesterCommand(userId, request), cancellationToken);

    [HttpGet("tickets")]
    public Task<PagedResponse<ClientTicketListItem>> GetTickets(
        [FromQuery] string? search,
        [FromQuery] TicketStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        sender.Send(new GetCmTicketsQuery(search, status, pageNumber, pageSize), cancellationToken);
}
