using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticket.Application;
using Ticket.Application.DTOs;
using Ticket.Application.Features.AgentArea.Commands;
using Ticket.Application.Features.AgentArea.Queries;
using Ticket.Application.Features.Requester.Commands;
using Ticket.Domain;
using Ticket.Domain.Enums;

namespace Ticket.Api.Controllers;

/// <summary>Agent area routes under /api/v1/agent (Phase 4).</summary>
[ApiController]
[Authorize(Roles = RoleNames.Agent)]
[Route("api/v1/agent")]
public sealed class AgentController(ISender sender) : ControllerBase
{
    [HttpGet("tickets")]
    public Task<PagedResponse<AgentTicketListItem>> GetTickets(
        [FromQuery] string? search,
        [FromQuery] TicketStatus? status,
        [FromQuery] TicketPriority? priority,
        [FromQuery] bool? assignedToMe,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        sender.Send(new GetAgentTicketsQuery(search, status, priority, assignedToMe, pageNumber, pageSize), cancellationToken);

    [HttpGet("tickets/{ticketId:int}")]
    public Task<TicketDetailDto> GetTicket(int ticketId, CancellationToken cancellationToken) =>
        sender.Send(new GetAgentTicketQuery(ticketId), cancellationToken);

    [HttpGet("tickets/{ticketId:int}/messages")]
    public Task<PagedResponse<TicketMessageDto>> GetMessages(
        int ticketId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        sender.Send(new GetAgentMessagesQuery(ticketId, pageNumber, pageSize), cancellationToken);

    [HttpPost("tickets/{ticketId:int}/messages")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<CreatedMessageResponse>> SendMessage(
        int ticketId,
        [FromForm] string? text,
        [FromForm] List<IFormFile>? attachments,
        CancellationToken cancellationToken)
    {
        var parts = await ToPartsAsync(attachments, cancellationToken);
        var created = await sender.Send(new SendAgentMessageCommand(ticketId, text, parts), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPatch("tickets/{ticketId:int}/seen")]
    public Task<SuccessResponse> MarkSeen(int ticketId, CancellationToken cancellationToken) =>
        sender.Send(new MarkTicketSeenCommand(ticketId), cancellationToken);

    [HttpPatch("tickets/{ticketId:int}/reassign")]
    public Task<SuccessResponse> Reassign(int ticketId, [FromBody] ReassignTicketRequest request, CancellationToken cancellationToken) =>
        sender.Send(new ReassignTicketCommand(ticketId, request.NewAgentId), cancellationToken);

    [HttpPatch("tickets/{ticketId:int}/status")]
    public Task<SuccessResponse> UpdateStatus(int ticketId, [FromBody] UpdateTicketStatusRequest request, CancellationToken cancellationToken) =>
        sender.Send(new UpdateTicketStatusCommand(ticketId, request.NewStatus), cancellationToken);

    [HttpPatch("tickets/{ticketId:int}/priority")]
    public Task<SuccessResponse> UpdatePriority(int ticketId, [FromBody] UpdateTicketPriorityRequest request, CancellationToken cancellationToken) =>
        sender.Send(new UpdateTicketPriorityCommand(ticketId, request.Priority), cancellationToken);

    [HttpGet("tickets/{ticketId:int}/notes")]
    public Task<IReadOnlyList<TicketNoteDto>> GetNotes(int ticketId, CancellationToken cancellationToken) =>
        sender.Send(new GetTicketNotesQuery(ticketId), cancellationToken);

    [HttpPost("tickets/{ticketId:int}/notes")]
    public async Task<ActionResult<CreatedNoteResponse>> AddNote(
        int ticketId,
        [FromBody] CreateNoteRequest request,
        CancellationToken cancellationToken)
    {
        var created = await sender.Send(new CreateTicketNoteCommand(ticketId, request.Text), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpGet("active-agents")]
    public Task<IReadOnlyList<ActiveAgentListItem>> GetActiveAgents(CancellationToken cancellationToken) =>
        sender.Send(new GetActiveAgentsQuery(), cancellationToken);

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
