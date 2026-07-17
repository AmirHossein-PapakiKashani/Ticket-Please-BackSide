using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Application.DTOs;
using Ticket.Domain;

namespace Ticket.Application.Features.Auth.Commands;

public sealed record RefreshTokenCommand(RefreshTokenRequest Request) : IRequest<TokenResponse>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.Request.RefreshToken).NotEmpty();
    }
}

public sealed class RefreshTokenCommandHandler(
    IApplicationDbContext db,
    IJwtTokenService jwt,
    IConfiguration config) : IRequestHandler<RefreshTokenCommand, TokenResponse>
{
    public async Task<TokenResponse> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var hash = jwt.HashToken(command.Request.RefreshToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (existing is null || existing.IsRevoked || existing.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAppException("Your session has expired, please log in again");

        var user = await db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == existing.UserId && u.IsActive && !u.IsDeleted, cancellationToken)
            ?? throw new UnauthorizedAppException("Your session has expired, please log in again");

        existing.IsRevoked = true;
        var refreshRaw = jwt.CreateRefreshTokenValue();
        var days = int.TryParse(config["Jwt:RefreshTokenDays"], out var d) ? d : 7;
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = jwt.HashToken(refreshRaw),
            ExpiresAt = DateTime.UtcNow.AddDays(days)
        });
        await db.SaveChangesAsync(cancellationToken);
        var access = jwt.CreateAccessToken(user.Id, user.Role!.Name, user.ProviderId, user.ClientId);
        return new TokenResponse(access, refreshRaw);
    }
}

public sealed record LogoutCommand(LogoutRequest Request) : IRequest<SuccessResponse>;

public sealed class LogoutCommandHandler(IApplicationDbContext db, IJwtTokenService jwt)
    : IRequestHandler<LogoutCommand, SuccessResponse>
{
    public async Task<SuccessResponse> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.Request.RefreshToken))
        {
            var hash = jwt.HashToken(command.Request.RefreshToken);
            var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
            if (existing is not null && !existing.IsRevoked)
            {
                existing.IsRevoked = true;
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        return new SuccessResponse();
    }
}

public sealed record ForgotPasswordCommand(ForgotPasswordRequest Request) : IRequest<SuccessResponse>;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Request.Username).NotEmpty();
    }
}

public sealed class ForgotPasswordCommandHandler(IApplicationDbContext db)
    : IRequestHandler<ForgotPasswordCommand, SuccessResponse>
{
    public async Task<SuccessResponse> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        var username = command.Request.Username.Trim();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username && !u.IsDeleted, cancellationToken);
        if (user is not null)
        {
            db.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.Id,
                Token = Guid.NewGuid().ToString("N"),
                ExpireAt = DateTime.UtcNow.AddHours(1)
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        return new SuccessResponse();
    }
}

public sealed record ResetPasswordCommand(ResetPasswordRequest Request) : IRequest<SuccessResponse>;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Request.Token).NotEmpty();
        RuleFor(x => x.Request.NewPassword).NotEmpty().MinimumLength(6);
    }
}

public sealed class ResetPasswordCommandHandler(IApplicationDbContext db, IPasswordHasher passwordHasher)
    : IRequestHandler<ResetPasswordCommand, SuccessResponse>
{
    public async Task<SuccessResponse> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var token = await db.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.Token == command.Request.Token, cancellationToken);

        if (token is null || token.IsUsed)
            throw new BadRequestAppException("This link has already been used");
        if (token.ExpireAt < DateTime.UtcNow)
            throw new BadRequestAppException("Recovery link has expired");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, cancellationToken)
            ?? throw new NotFoundException("User was not found");

        user.PassHash = passwordHasher.Hash(command.Request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        token.IsUsed = true;
        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }
}
