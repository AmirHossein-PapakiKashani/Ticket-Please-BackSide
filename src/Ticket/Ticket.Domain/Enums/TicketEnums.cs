namespace Ticket.Domain.Enums;

/// <summary>Ticket lifecycle status (persist as int; serialize as string in API).</summary>
public enum TicketStatus
{
    Open = 1,
    InProgress = 2,
    PendingRequesterResponse = 3,
    Resolved = 4,
    Closed = 5
}

/// <summary>Ticket priority (persist as int; serialize as string in API).</summary>
public enum TicketPriority
{
    Low = 1,
    Medium = 2,
    High = 3
}
