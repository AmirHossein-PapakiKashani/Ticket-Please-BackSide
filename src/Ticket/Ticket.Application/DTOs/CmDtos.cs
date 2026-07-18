using Ticket.Domain.Enums;

namespace Ticket.Application.DTOs;

public sealed record ClientDashboardStats(int TotalRequesters, int OpenTicketsTotal, int ClosedTicketsTotal);

public sealed record RequesterListItem(int Id, string FullName, string Username, bool IsActive, int TicketsCreatedCount);
public sealed record CreateRequesterRequest(string FullName, string Username, string PhoneNumber, string Password);
public sealed record UpdateRequesterRequest(string FullName, string PhoneNumber, bool IsActive);
public sealed record CreatedRequesterResponse(int UserId);
public sealed record RequesterTicketStatsDto(int Open, int Closed);
public sealed record RequesterDetailDto(
    string FullName,
    string Username,
    string PhoneNumber,
    bool IsActive,
    RequesterTicketStatsDto TicketStats);

public sealed record ClientTicketListItem(
    int Id,
    string Topic,
    string RequesterName,
    string? AssignedAgentName,
    TicketStatus Status,
    TicketPriority Priority);
