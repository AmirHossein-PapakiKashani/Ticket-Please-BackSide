using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Application.DTOs;
using Ticket.Domain;

namespace Ticket.Application.Features.Pm.Commands;

public sealed record CreateClientCommand(CreateClientRequest Request) : IRequest<CreatedClientResponse>;

public sealed class CreateClientCommandValidator : AbstractValidator<CreateClientCommand>
{
    public CreateClientCommandValidator()
    {
        RuleFor(x => x.Request.Name).NotEmpty().MinimumLength(2).MaximumLength(200);
        RuleFor(x => x.Request.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Request.PhoneNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Request.ManagerUsername).NotEmpty().MinimumLength(3).MaximumLength(100);
        RuleFor(x => x.Request.ManagerFullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Request.ManagerPassword).NotEmpty().MinimumLength(6);
    }
}

public sealed class CreateClientCommandHandler(
    IApplicationDbContext db,
    ICurrentUser current,
    IPasswordHasher passwordHasher) : IRequestHandler<CreateClientCommand, CreatedClientResponse>
{
    public async Task<CreatedClientResponse> Handle(CreateClientCommand command, CancellationToken cancellationToken)
    {
        var providerId = PmHelpers.RequireProviderId(current);
        var managerUsername = command.Request.ManagerUsername.Trim();
        if (await db.Users.AnyAsync(u => u.Username == managerUsername, cancellationToken))
            throw new ConflictException("This username is already in use");

        var plan = await PmHelpers.RequireActivePlanAsync(db, providerId, cancellationToken);
        var activeClients = await db.Clients.CountAsync(
            c => c.ProviderId == providerId && c.IsActive && !c.IsDeleted,
            cancellationToken);
        if (activeClients >= plan.MaxClientCount)
            throw new BadRequestAppException("You have reached the maximum number of clients allowed by your plan");

        var cmRoleId = await PmHelpers.ClientManagerRoleIdAsync(db, cancellationToken);
        var client = new Client
        {
            ProviderId = providerId,
            Name = command.Request.Name.Trim(),
            Email = command.Request.Email.Trim(),
            PhoneNumber = command.Request.PhoneNumber.Trim(),
            IsActive = true
        };
        db.Clients.Add(client);
        db.Users.Add(new User
        {
            FullName = command.Request.ManagerFullName.Trim(),
            Username = managerUsername,
            PhoneNumber = command.Request.PhoneNumber.Trim(),
            PassHash = passwordHasher.Hash(command.Request.ManagerPassword),
            RoleId = cmRoleId,
            Client = client,
            IsActive = true
        });
        await db.SaveChangesAsync(cancellationToken);
        return new CreatedClientResponse(client.Id);
    }
}

public sealed record UpdateClientCommand(int ClientId, UpdateClientRequest Request) : IRequest<SuccessResponse>;

public sealed class UpdateClientCommandValidator : AbstractValidator<UpdateClientCommand>
{
    public UpdateClientCommandValidator()
    {
        RuleFor(x => x.Request.Name).NotEmpty().MinimumLength(2).MaximumLength(200);
        RuleFor(x => x.Request.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Request.PhoneNumber).NotEmpty().MaximumLength(20);
    }
}

public sealed class UpdateClientCommandHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<UpdateClientCommand, SuccessResponse>
{
    public async Task<SuccessResponse> Handle(UpdateClientCommand command, CancellationToken cancellationToken)
    {
        var providerId = PmHelpers.RequireProviderId(current);
        var client = await db.Clients.FirstOrDefaultAsync(
            c => c.Id == command.ClientId && c.ProviderId == providerId && !c.IsDeleted,
            cancellationToken) ?? throw new NotFoundException("Client was not found");

        client.Name = command.Request.Name.Trim();
        client.Email = command.Request.Email.Trim();
        client.PhoneNumber = command.Request.PhoneNumber.Trim();
        client.IsActive = command.Request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }
}

public sealed record DeactivateClientCommand(int ClientId) : IRequest<SuccessResponse>;

public sealed class DeactivateClientCommandHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<DeactivateClientCommand, SuccessResponse>
{
    public async Task<SuccessResponse> Handle(DeactivateClientCommand command, CancellationToken cancellationToken)
    {
        var providerId = PmHelpers.RequireProviderId(current);
        var client = await db.Clients.FirstOrDefaultAsync(
            c => c.Id == command.ClientId && c.ProviderId == providerId && !c.IsDeleted,
            cancellationToken) ?? throw new NotFoundException("Client was not found");

        client.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }
}
