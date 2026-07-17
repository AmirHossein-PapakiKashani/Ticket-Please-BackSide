using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ticket.Application;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Application.DTOs;
using Ticket.Domain;

namespace Ticket.Application.Features.Profile.Commands;

public sealed record GetProfileQuery : IRequest<UserProfile>;

public sealed class GetProfileQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetProfileQuery, UserProfile>
{
    public async Task<UserProfile> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == current.UserId && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("User was not found");

        string? orgName = null;
        if (user.ProviderId is int pid)
            orgName = await db.Providers.AsNoTracking().Where(p => p.Id == pid).Select(p => p.Name).FirstOrDefaultAsync(cancellationToken);
        else if (user.ClientId is int cid)
            orgName = await db.Clients.AsNoTracking().Where(c => c.Id == cid).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken);

        return new UserProfile(user.FullName, user.Username, user.PhoneNumber, user.Role!.Name, orgName);
    }
}

public sealed record UpdateProfileCommand(UpdateProfileRequest Request) : IRequest<SuccessResponse>;

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.Request.FullName).NotEmpty().MinimumLength(2).MaximumLength(150);
        RuleFor(x => x.Request.PhoneNumber).NotEmpty().Matches(@"^09\d{9}$")
            .WithMessage("Phone number must be a valid Iranian mobile number.");
    }
}

public sealed class UpdateProfileCommandHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<UpdateProfileCommand, SuccessResponse>
{
    public async Task<SuccessResponse> Handle(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == current.UserId && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("User was not found");
        user.FullName = command.Request.FullName.Trim();
        user.PhoneNumber = command.Request.PhoneNumber.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }
}

public sealed record ChangePasswordCommand(ChangePasswordRequest Request) : IRequest<SuccessResponse>;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.Request.CurrentPassword).NotEmpty();
        RuleFor(x => x.Request.NewPassword).NotEmpty().MinimumLength(6);
    }
}

public sealed class ChangePasswordCommandHandler(IApplicationDbContext db, ICurrentUser current, IPasswordHasher hasher)
    : IRequestHandler<ChangePasswordCommand, SuccessResponse>
{
    public async Task<SuccessResponse> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == current.UserId && !u.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("User was not found");
        if (!hasher.Verify(user.PassHash, command.Request.CurrentPassword))
            throw new UnauthorizedAppException("Current password is incorrect");
        user.PassHash = hasher.Hash(command.Request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }
}

public sealed record GetSubscriptionQuery : IRequest<SubscriptionInfo>;

public sealed class GetSubscriptionQueryHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<GetSubscriptionQuery, SubscriptionInfo>
{
    public async Task<SubscriptionInfo> Handle(GetSubscriptionQuery request, CancellationToken cancellationToken)
    {
        if (current.RoleName != RoleNames.ProviderManager)
            throw new ForbiddenAppException("Only ProviderManager can access subscription info.");
        if (current.ProviderId is not int providerId)
            throw new NotFoundException("No active subscription found");

        var sub = await db.ProviderSubscriptions.AsNoTracking()
            .Where(s => s.ProviderId == providerId && s.IsActive)
            .OrderByDescending(s => s.ExpireDate)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("No active subscription found");

        var plan = await db.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == sub.PlanId, cancellationToken)
            ?? throw new NotFoundException("No active subscription found");

        var remaining = Math.Max(0, (int)Math.Ceiling((sub.ExpireDate - DateTime.UtcNow).TotalDays));
        return new SubscriptionInfo(plan.Name, sub.ExpireDate, remaining);
    }
}
