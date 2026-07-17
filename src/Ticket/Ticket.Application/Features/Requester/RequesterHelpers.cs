using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Domain.Enums;
using TicketEntity = Ticket.Domain.Ticket;

namespace Ticket.Application.Features.Requester;

internal static class RequesterHelpers
{
    public static int RequireClientId(ICurrentUser current) =>
        current.ClientId ?? throw new ForbiddenAppException("Client context is required.");

    public static async Task<TicketEntity> RequireClientTicketAsync(
        IApplicationDbContext db,
        int ticketId,
        int clientId,
        CancellationToken cancellationToken) =>
        await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId && t.ClientId == clientId && !t.IsDeleted, cancellationToken)
        ?? throw new NotFoundException("Ticket was not found");

    public static readonly TicketStatus[] OpenStatuses =
    [
        TicketStatus.Open,
        TicketStatus.InProgress,
        TicketStatus.PendingRequesterResponse
    ];

    public static PagedResponse<T> Page<T>(IReadOnlyList<T> items, int pageNumber, int pageSize, int total) =>
        new(items, pageNumber, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
}
