using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Application.DTOs;
using Ticket.Domain;

namespace Ticket.Application.Features.Auth.Commands;

public sealed record LoginCommand(LoginRequest Request) : IRequest<LoginResponse>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Request.Username).NotEmpty().MinimumLength(3).MaximumLength(100);
        RuleFor(x => x.Request.Password).NotEmpty().MinimumLength(6);
    }
}

public sealed class LoginCommandHandler(
    IApplicationDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwt,
    Microsoft.Extensions.Configuration.IConfiguration config) : IRequestHandler<LoginCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var username = command.Request.Username.Trim();
        var user = await db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

        if (user is null || !passwordHasher.Verify(user.PassHash, command.Request.Password))
            throw new UnauthorizedAppException("Incorrect username or password");

        if (!user.IsActive || user.IsDeleted)
            throw new ForbiddenAppException("Account has been deactivated");

        if (user.ProviderId is int providerId)
        {
            var provider = await db.Providers.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == providerId && !p.IsDeleted, cancellationToken);
            if (provider is null || !provider.IsActive)
                throw new ForbiddenAppException("Account has been deactivated");
        }

        if (user.ClientId is int clientId)
        {
            var client = await db.Clients.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == clientId && !c.IsDeleted, cancellationToken);
            if (client is null || !client.IsActive)
                throw new ForbiddenAppException("Account has been deactivated");
        }

        var access = jwt.CreateAccessToken(user.Id, user.Role!.Name, user.ProviderId, user.ClientId);
        var refreshRaw = jwt.CreateRefreshTokenValue();
        var days = int.TryParse(config["Jwt:RefreshTokenDays"], out var d) ? d : 7;
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = jwt.HashToken(refreshRaw),
            ExpiresAt = DateTime.UtcNow.AddDays(days)
        });
        await db.SaveChangesAsync(cancellationToken);
        return new LoginResponse(access, refreshRaw, user.Id, user.Role.Name, user.FullName);
    }
}
