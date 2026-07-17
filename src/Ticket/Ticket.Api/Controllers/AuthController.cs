using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticket.Application;
using Ticket.Application.DTOs;
using Ticket.Application.Features.Auth.Commands;

namespace Ticket.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public Task<LoginResponse> Login([FromBody] LoginRequest request, CancellationToken cancellationToken) =>
        sender.Send(new LoginCommand(request), cancellationToken);

    [AllowAnonymous]
    [HttpPost("refresh-token")]
    public Task<TokenResponse> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken) =>
        sender.Send(new RefreshTokenCommand(request), cancellationToken);

    [Authorize]
    [HttpPost("logout")]
    public Task<SuccessResponse> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken) =>
        sender.Send(new LogoutCommand(request), cancellationToken);

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public Task<SuccessResponse> Forgot([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken) =>
        sender.Send(new ForgotPasswordCommand(request), cancellationToken);

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public Task<SuccessResponse> Reset([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken) =>
        sender.Send(new ResetPasswordCommand(request), cancellationToken);
}
