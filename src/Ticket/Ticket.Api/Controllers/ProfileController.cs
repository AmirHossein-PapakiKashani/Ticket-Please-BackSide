using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticket.Application;
using Ticket.Application.DTOs;
using Ticket.Application.Features.Profile.Commands;

namespace Ticket.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/profile")]
public sealed class ProfileController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<UserProfile> Get(CancellationToken cancellationToken) =>
        sender.Send(new GetProfileQuery(), cancellationToken);

    [HttpPut]
    public Task<SuccessResponse> Update([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken) =>
        sender.Send(new UpdateProfileCommand(request), cancellationToken);

    [HttpPut("change-password")]
    public Task<SuccessResponse> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken) =>
        sender.Send(new ChangePasswordCommand(request), cancellationToken);

    [HttpGet("subscription")]
    public Task<SubscriptionInfo> Subscription(CancellationToken cancellationToken) =>
        sender.Send(new GetSubscriptionQuery(), cancellationToken);
}
