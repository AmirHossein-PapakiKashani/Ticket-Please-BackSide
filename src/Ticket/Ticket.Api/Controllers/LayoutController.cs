using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticket.Application.DTOs;
using Ticket.Application.Features.Layout.Queries;

namespace Ticket.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/layout")]
public sealed class LayoutController(ISender sender) : ControllerBase
{
    [HttpGet("profile-summary")]
    public Task<ProfileSummary> ProfileSummary(CancellationToken cancellationToken) =>
        sender.Send(new GetProfileSummaryQuery(), cancellationToken);

    [HttpGet("notifications/unread-count")]
    public Task<UnreadCountResponse> UnreadCount(CancellationToken cancellationToken) =>
        sender.Send(new GetUnreadCountQuery(), cancellationToken);
}
