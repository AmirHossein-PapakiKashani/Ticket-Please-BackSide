using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Application.DTOs;
using Ticket.Domain;

namespace Ticket.Application.Features.Pm.Commands;

public sealed record CreateAgentCommand(CreateAgentRequest Request) : IRequest<CreatedAgentResponse>;

public sealed class CreateAgentCommandValidator : AbstractValidator<CreateAgentCommand>
{
    public CreateAgentCommandValidator()
    {
        RuleFor(x => x.Request.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Request.Username).NotEmpty().MinimumLength(3).MaximumLength(100);
        RuleFor(x => x.Request.PhoneNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Request.Password).NotEmpty().MinimumLength(6);
    }
}

public sealed class CreateAgentCommandHandler(
    IApplicationDbContext db,
    ICurrentUser current,
    IPasswordHasher passwordHasher) : IRequestHandler<CreateAgentCommand, CreatedAgentResponse>
{
    public async Task<CreatedAgentResponse> Handle(CreateAgentCommand command, CancellationToken cancellationToken)
    {
        var providerId = PmHelpers.RequireProviderId(current);
        var username = command.Request.Username.Trim();
        if (await db.Users.AnyAsync(u => u.Username == username, cancellationToken))
            throw new ConflictException("This username is already in use");

        var plan = await PmHelpers.RequireActivePlanAsync(db, providerId, cancellationToken);
        var agentRoleId = await PmHelpers.AgentRoleIdAsync(db, cancellationToken);
        var activeAgents = await db.Users.CountAsync(
            u => u.ProviderId == providerId && u.RoleId == agentRoleId && u.IsActive && !u.IsDeleted,
            cancellationToken);
        if (activeAgents >= plan.MaxAgentCount)
            throw new BadRequestAppException("You have reached the maximum number of agents allowed by your plan");

        var user = new User
        {
            FullName = command.Request.FullName.Trim(),
            Username = username,
            PhoneNumber = command.Request.PhoneNumber.Trim(),
            PassHash = passwordHasher.Hash(command.Request.Password),
            RoleId = agentRoleId,
            ProviderId = providerId,
            LastAssignedAt = null,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return new CreatedAgentResponse(user.Id);
    }
}

public sealed record UpdateAgentCommand(int UserId, UpdateAgentRequest Request) : IRequest<SuccessResponse>;

public sealed class UpdateAgentCommandValidator : AbstractValidator<UpdateAgentCommand>
{
    public UpdateAgentCommandValidator()
    {
        RuleFor(x => x.Request.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Request.PhoneNumber).NotEmpty().MaximumLength(20);
    }
}

public sealed class UpdateAgentCommandHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<UpdateAgentCommand, SuccessResponse>
{
    public async Task<SuccessResponse> Handle(UpdateAgentCommand command, CancellationToken cancellationToken)
    {
        var providerId = PmHelpers.RequireProviderId(current);
        var agentRoleId = await PmHelpers.AgentRoleIdAsync(db, cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(
            u => u.Id == command.UserId && u.ProviderId == providerId && u.RoleId == agentRoleId && !u.IsDeleted,
            cancellationToken) ?? throw new NotFoundException("Agent was not found");

        user.FullName = command.Request.FullName.Trim();
        user.PhoneNumber = command.Request.PhoneNumber.Trim();
        user.IsActive = command.Request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }
}

public sealed record DeactivateAgentCommand(int UserId) : IRequest<SuccessResponse>;

public sealed class DeactivateAgentCommandHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<DeactivateAgentCommand, SuccessResponse>
{
    public async Task<SuccessResponse> Handle(DeactivateAgentCommand command, CancellationToken cancellationToken)
    {
        var providerId = PmHelpers.RequireProviderId(current);
        var agentRoleId = await PmHelpers.AgentRoleIdAsync(db, cancellationToken);
        var user = await db.Users.FirstOrDefaultAsync(
            u => u.Id == command.UserId && u.ProviderId == providerId && u.RoleId == agentRoleId && !u.IsDeleted,
            cancellationToken) ?? throw new NotFoundException("Agent was not found");

        // Soft deactivate only — open tickets are NOT auto-reassigned (DESIGN / Phase 2).
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }
}
