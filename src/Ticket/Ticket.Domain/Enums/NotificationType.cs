namespace Ticket.Domain.Enums;

/// <summary>Notification event types (persist as int; serialize as string in API).</summary>
public enum NotificationType
{
    NewTicketAssigned = 1,
    NewMessage = 2,
    TicketReassigned = 3,
    TicketResolved = 4,
    TicketClosed = 5,
    TicketReopened = 6
}
