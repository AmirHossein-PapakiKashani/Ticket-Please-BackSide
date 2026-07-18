using Ticket.Domain.Enums;

namespace Ticket.Application.DTOs;

public sealed record AgentTicketListItem(
    int Id,
    string Topic,
    string ClientName,
    string RequesterName,
    TicketStatus Status,
    TicketPriority Priority,
    DateTime OpenDate,
    bool IsSeen);

public sealed record ReassignTicketRequest(int NewAgentId);
public sealed record UpdateTicketStatusRequest(TicketStatus NewStatus);
public sealed record UpdateTicketPriorityRequest(TicketPriority Priority);
public sealed record CreateNoteRequest(string Text);
public sealed record CreatedNoteResponse(int NoteId);
public sealed record TicketNoteDto(string AuthorName, string Text, DateTime CreatedAt);
public sealed record ActiveAgentListItem(int Id, string FullName, int OpenTicketsCount);
