using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticket.Application;
using Ticket.Application.DTOs;
using Ticket.Application.Features.Notifications.Commands;

namespace Ticket.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public sealed class NotificationsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<NotificationItem>> List(
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? onlyUnread = null,
        CancellationToken cancellationToken = default) =>
        sender.Send(new GetNotificationsQuery(search, pageNumber, pageSize, onlyUnread), cancellationToken);

    [HttpPatch("{notificationId:int}/read")]
    public Task<SuccessResponse> MarkRead(int notificationId, CancellationToken cancellationToken) =>
        sender.Send(new MarkNotificationReadCommand(notificationId), cancellationToken);

    [HttpPatch("read-all")]
    public Task<SuccessResponse> MarkAll(CancellationToken cancellationToken) =>
        sender.Send(new MarkAllNotificationsReadCommand(), cancellationToken);
}
