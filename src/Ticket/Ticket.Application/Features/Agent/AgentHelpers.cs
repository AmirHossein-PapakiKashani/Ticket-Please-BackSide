using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Domain.Enums;
using TicketEntity = Ticket.Domain.Ticket;

namespace Ticket.Application.Features.AgentArea;

internal static class AgentHelpers
{
    public static int RequireProviderId(ICurrentUser current) =>
        current.ProviderId ?? throw new ForbiddenAppException("Provider context is required.");

    public static async Task<TicketEntity> RequireProviderTicketAsync(
        IApplicationDbContext db,
        int ticketId,
        int providerId,
        CancellationToken cancellationToken) =>
        await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId && t.ProviderId == providerId && !t.IsDeleted, cancellationToken)
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
