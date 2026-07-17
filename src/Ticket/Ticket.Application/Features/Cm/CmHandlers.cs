using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Application.DTOs;
using Ticket.Domain;
using Ticket.Domain.Enums;

namespace Ticket.Application.Features.Cm;

internal static class CmHelpers
{
    public static int RequireClientId(ICurrentUser current) =>
        current.ClientId ?? throw new ForbiddenAppException("Client context is required.");

    public static PagedResponse<T> Page<T>(IReadOnlyList<T> items, int pageNumber, int pageSize, int total) =>
        new(items, pageNumber, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));

    public static readonly TicketStatus[] OpenStatuses =
    [
        TicketStatus.Open,
        TicketStatus.InProgress,
        TicketStatus.PendingRequesterResponse
    ];
}

public sealed record GetCmDashboardStatsQuery : IRequest<ClientDashboardStats>;

public sealed class GetCmDashboardStatsQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetCmDashboardStatsQuery, ClientDashboardStats>
{
    public async Task<ClientDashboardStats> Handle(GetCmDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        var clientId = CmHelpers.RequireClientId(current);
        var requesterRoleId = await db.Roles.Where(r => r.Name == RoleNames.Requester).Select(r => r.Id).FirstAsync(cancellationToken);
        var totalRequesters = await db.Users.CountAsync(
            u => u.ClientId == clientId && u.RoleId == requesterRoleId && !u.IsDeleted, cancellationToken);
        var tickets = db.Tickets.AsNoTracking().Where(t => t.ClientId == clientId && !t.IsDeleted);
        var open = await tickets.CountAsync(t => CmHelpers.OpenStatuses.Contains(t.Status), cancellationToken);
        var closed = await tickets.CountAsync(
            t => t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed, cancellationToken);
        return new ClientDashboardStats(totalRequesters, open, closed);
    }
}

public sealed record GetRequestersQuery(string? Search, int PageNumber, int PageSize)
    : IRequest<PagedResponse<RequesterListItem>>;

public sealed class GetRequestersQueryValidator : AbstractValidator<GetRequestersQuery>
{
    public GetRequestersQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetRequestersQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetRequestersQuery, PagedResponse<RequesterListItem>>
{
    public async Task<PagedResponse<RequesterListItem>> Handle(GetRequestersQuery request, CancellationToken cancellationToken)
    {
        var clientId = CmHelpers.RequireClientId(current);
        var requesterRoleId = await db.Roles.Where(r => r.Name == RoleNames.Requester).Select(r => r.Id).FirstAsync(cancellationToken);
        var query = db.Users.AsNoTracking()
            .Where(u => u.ClientId == clientId && u.RoleId == requesterRoleId && !u.IsDeleted);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(u => u.FullName.Contains(s) || u.Username.Contains(s));
        }
        var total = await query.CountAsync(cancellationToken);
        var users = await query.OrderBy(u => u.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var items = new List<RequesterListItem>();
        foreach (var u in users)
        {
            var count = await db.Tickets.CountAsync(t => t.RequesterId == u.Id && !t.IsDeleted, cancellationToken);
            items.Add(new RequesterListItem(u.Id, u.FullName, u.Username, u.IsActive, count));
        }
        return CmHelpers.Page(items, request.PageNumber, request.PageSize, total);
    }
}

public sealed record CreateRequesterCommand(CreateRequesterRequest Request) : IRequest<CreatedRequesterResponse>;

public sealed class CreateRequesterCommandValidator : AbstractValidator<CreateRequesterCommand>
{
    public CreateRequesterCommandValidator()
    {
        RuleFor(x => x.Request.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Request.Username).NotEmpty().MinimumLength(3).MaximumLength(100);
        RuleFor(x => x.Request.PhoneNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Request.Password).NotEmpty().MinimumLength(6);
    }
}

public sealed class CreateRequesterCommandHandler(
    IApplicationDbContext db,
    ICurrentUser current,
    IPasswordHasher passwordHasher) : IRequestHandler<CreateRequesterCommand, CreatedRequesterResponse>
{
    public async Task<CreatedRequesterResponse> Handle(CreateRequesterCommand command, CancellationToken cancellationToken)
    {
        var clientId = CmHelpers.RequireClientId(current);
        var username = command.Request.Username.Trim();
        if (await db.Users.AnyAsync(u => u.Username == username, cancellationToken))
            throw new ConflictException("This username is already in use");

        var requesterRoleId = await db.Roles.Where(r => r.Name == RoleNames.Requester).Select(r => r.Id).FirstAsync(cancellationToken);
        var user = new User
        {
            FullName = command.Request.FullName.Trim(),
            Username = username,
            PhoneNumber = command.Request.PhoneNumber.Trim(),
            PassHash = passwordHasher.Hash(command.Request.Password),
            RoleId = requesterRoleId,
            ClientId = clientId,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return new CreatedRequesterResponse(user.Id);
    }
}

public sealed record DeactivateRequesterCommand(int UserId) : IRequest<SuccessResponse>;

public sealed class DeactivateRequesterCommandHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<DeactivateRequesterCommand, SuccessResponse>
{
    public async Task<SuccessResponse> Handle(DeactivateRequesterCommand command, CancellationToken cancellationToken)
    {
        var clientId = CmHelpers.RequireClientId(current);
        var requesterRoleId = await db.Roles.Where(r => r.Name == RoleNames.Requester).Select(r => r.Id).FirstAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(
            u => u.Id == command.UserId && u.ClientId == clientId && u.RoleId == requesterRoleId && !u.IsDeleted,
            cancellationToken) ?? throw new NotFoundException("Requester was not found");
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }
}

public sealed record GetRequesterDetailQuery(int UserId) : IRequest<RequesterDetailDto>;

public sealed class GetRequesterDetailQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetRequesterDetailQuery, RequesterDetailDto>
{
    public async Task<RequesterDetailDto> Handle(GetRequesterDetailQuery request, CancellationToken cancellationToken)
    {
        var clientId = CmHelpers.RequireClientId(current);
        var requesterRoleId = await db.Roles.Where(r => r.Name == RoleNames.Requester).Select(r => r.Id).FirstAsync(cancellationToken);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(
            u => u.Id == request.UserId && u.ClientId == clientId && u.RoleId == requesterRoleId && !u.IsDeleted,
            cancellationToken) ?? throw new NotFoundException("Requester was not found");

        var open = await db.Tickets.CountAsync(t =>
            t.RequesterId == user.Id && !t.IsDeleted && CmHelpers.OpenStatuses.Contains(t.Status), cancellationToken);
        var closed = await db.Tickets.CountAsync(t =>
            t.RequesterId == user.Id && !t.IsDeleted &&
            (t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed), cancellationToken);

        return new RequesterDetailDto(
            user.FullName,
            user.Username,
            user.PhoneNumber,
            user.IsActive,
            new RequesterTicketStatsDto(open, closed));
    }
}

public sealed record UpdateRequesterCommand(int UserId, UpdateRequesterRequest Request) : IRequest<SuccessResponse>;

public sealed class UpdateRequesterCommandValidator : AbstractValidator<UpdateRequesterCommand>
{
    public UpdateRequesterCommandValidator()
    {
        RuleFor(x => x.Request.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Request.PhoneNumber).NotEmpty().MaximumLength(20);
    }
}

public sealed class UpdateRequesterCommandHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<UpdateRequesterCommand, SuccessResponse>
{
    public async Task<SuccessResponse> Handle(UpdateRequesterCommand command, CancellationToken cancellationToken)
    {
        var clientId = CmHelpers.RequireClientId(current);
        var requesterRoleId = await db.Roles.Where(r => r.Name == RoleNames.Requester).Select(r => r.Id).FirstAsync(cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(
            u => u.Id == command.UserId && u.ClientId == clientId && u.RoleId == requesterRoleId && !u.IsDeleted,
            cancellationToken) ?? throw new NotFoundException("Requester was not found");
        user.FullName = command.Request.FullName.Trim();
        user.PhoneNumber = command.Request.PhoneNumber.Trim();
        user.IsActive = command.Request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }
}

public sealed record GetCmTicketsQuery(string? Search, TicketStatus? Status, int PageNumber, int PageSize)
    : IRequest<PagedResponse<ClientTicketListItem>>;

public sealed class GetCmTicketsQueryValidator : AbstractValidator<GetCmTicketsQuery>
{
    public GetCmTicketsQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
    }
}

public sealed class GetCmTicketsQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetCmTicketsQuery, PagedResponse<ClientTicketListItem>>
{
    public async Task<PagedResponse<ClientTicketListItem>> Handle(GetCmTicketsQuery request, CancellationToken cancellationToken)
    {
        var clientId = CmHelpers.RequireClientId(current);
        var query = db.Tickets.AsNoTracking().Where(t => t.ClientId == clientId && !t.IsDeleted);
        if (request.Status is TicketStatus status) query = query.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(t => t.Topic.Contains(s));
        }
        var total = await query.CountAsync(cancellationToken);
        var tickets = await query.OrderByDescending(t => t.OpenDate)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var requesterIds = tickets.Select(t => t.RequesterId).Distinct().ToList();
        var agentIds = tickets.Where(t => t.AssignedAgentId.HasValue).Select(t => t.AssignedAgentId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking()
            .Where(u => requesterIds.Contains(u.Id) || agentIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var items = tickets.Select(t => new ClientTicketListItem(
            t.Id,
            t.Topic,
            names.GetValueOrDefault(t.RequesterId, string.Empty),
            t.AssignedAgentId is int aid ? names.GetValueOrDefault(aid) : null,
            t.Status,
            t.Priority)).ToList();

        return CmHelpers.Page(items, request.PageNumber, request.PageSize, total);
    }
}
