using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticket.Application;
using Ticket.Application.DTOs;
using Ticket.Application.Features.Requester.Commands;
using Ticket.Application.Features.Requester.Queries;
using Ticket.Domain;
using Ticket.Domain.Enums;

namespace Ticket.Api.Controllers;

/// <summary>Requester area routes under /api/v1/requester (Phase 3).</summary>
[ApiController]
[Authorize(Roles = RoleNames.Requester)]
[Route("api/v1/requester")]
public sealed class RequesterController(ISender sender) : ControllerBase
{
    [HttpPost("tickets")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<CreatedTicketResponse>> CreateTicket(
        [FromForm] string topic,
        [FromForm] string text,
        [FromForm] TicketPriority? priority,
        [FromForm] List<IFormFile>? attachments,
        CancellationToken cancellationToken)
    {
        var parts = await ToPartsAsync(attachments, cancellationToken);
        var created = await sender.Send(new CreateTicketCommand(topic, text, priority, parts), cancellationToken);
        return CreatedAtAction(nameof(GetTicket), new { ticketId = created.TicketId }, created);
    }

    [HttpGet("tickets")]
    public Task<PagedResponse<RequesterTicketListItem>> GetTickets(
        [FromQuery] string? search,
        [FromQuery] TicketStatus? status,
        [FromQuery] bool? createdByMe,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        sender.Send(new GetRequesterTicketsQuery(search, status, createdByMe, pageNumber, pageSize), cancellationToken);

    [HttpGet("tickets/{ticketId:int}")]
    public Task<TicketDetailDto> GetTicket(int ticketId, CancellationToken cancellationToken) =>
        sender.Send(new GetRequesterTicketQuery(ticketId), cancellationToken);

    [HttpGet("tickets/{ticketId:int}/messages")]
    public Task<PagedResponse<TicketMessageDto>> GetMessages(
        int ticketId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        sender.Send(new GetRequesterMessagesQuery(ticketId, pageNumber, pageSize), cancellationToken);

    [HttpPost("tickets/{ticketId:int}/messages")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<CreatedMessageResponse>> SendMessage(
        int ticketId,
        [FromForm] string? text,
        [FromForm] List<IFormFile>? attachments,
        CancellationToken cancellationToken)
    {
        var parts = await ToPartsAsync(attachments, cancellationToken);
        var created = await sender.Send(new SendRequesterMessageCommand(ticketId, text, parts), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPatch("tickets/{ticketId:int}/reopen")]
    public Task<SuccessResponse> Reopen(int ticketId, CancellationToken cancellationToken) =>
        sender.Send(new ReopenTicketCommand(ticketId), cancellationToken);

    private static async Task<List<UploadFilePart>?> ToPartsAsync(List<IFormFile>? files, CancellationToken cancellationToken)
    {
        if (files is null || files.Count == 0) return null;
        var parts = new List<UploadFilePart>();
        foreach (var file in files.Where(f => f.Length > 0))
        {
            var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;
            parts.Add(new UploadFilePart(ms, file.FileName, file.ContentType));
        }
        return parts;
    }
}
