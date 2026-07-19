using Ticket.Domain.Enums;

namespace Ticket.Application.DTOs;

public sealed record CreatedTicketResponse(int TicketId);
public sealed record CreatedMessageResponse(int MessageId);

public sealed record RequesterTicketListItem(
    int Id,
    string Topic,
    string RequesterName,
    TicketStatus Status,
    TicketPriority Priority,
    DateTime OpenDate);

public sealed record TicketDetailDto(
    string Topic,
    TicketStatus Status,
    TicketPriority Priority,
    string? ClientName,
    string RequesterName,
    string? AssignedAgentName);

public sealed record AttachmentDto(string FileUrl, string FileName, string FileType, int FileSizeKB);

public sealed record TicketMessageDto(
    int SenderId,
    string SenderName,
    string Text,
    DateTime CreatedAt,
    DateTime? SeenAt,
    IReadOnlyList<AttachmentDto> Attachments);
