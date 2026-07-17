using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Application.DTOs;
using Ticket.Domain;

namespace Ticket.Application.Features.Pm.Queries;

public sealed record GetClientsQuery(string? Search, int PageNumber, int PageSize) : IRequest<PagedResponse<ClientListItem>>;

public sealed class GetClientsQueryValidator : AbstractValidator<GetClientsQuery>
{
    public GetClientsQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetClientsQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetClientsQuery, PagedResponse<ClientListItem>>
{
    public async Task<PagedResponse<ClientListItem>> Handle(GetClientsQuery request, CancellationToken cancellationToken)
    {
        var providerId = PmHelpers.RequireProviderId(current);
        var query = db.Clients.AsNoTracking().Where(c => c.ProviderId == providerId && !c.IsDeleted);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(c => c.Name.Contains(s) || c.Email.Contains(s));
        }

        var total = await query.CountAsync(cancellationToken);
        var clients = await query.OrderBy(c => c.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = clients.Select(c => new ClientListItem(c.Id, c.Name, c.Email, c.IsActive, 0)).ToList();
        return PmHelpers.Page(items, request.PageNumber, request.PageSize, total);
    }
}

public sealed record GetClientQuery(int ClientId) : IRequest<ClientDetail>;

public sealed class GetClientQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetClientQuery, ClientDetail>
{
    public async Task<ClientDetail> Handle(GetClientQuery request, CancellationToken cancellationToken)
    {
        var providerId = PmHelpers.RequireProviderId(current);
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(
            c => c.Id == request.ClientId && c.ProviderId == providerId && !c.IsDeleted,
            cancellationToken) ?? throw new NotFoundException("Client was not found");

        var requesterRoleId = await PmHelpers.RequesterRoleIdAsync(db, cancellationToken);
        var requesterCount = await db.Users.CountAsync(
            u => u.ClientId == client.Id && u.RoleId == requesterRoleId && !u.IsDeleted,
            cancellationToken);

        return new ClientDetail(
            client.Name,
            client.Email,
            client.PhoneNumber,
            client.IsActive,
            requesterCount,
            new ClientTicketStats(0, 0));
    }
}
