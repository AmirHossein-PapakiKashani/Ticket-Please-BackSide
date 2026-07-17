using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Ticket.Application;
using Ticket.Application.Abstractions;
using Ticket.Domain;

namespace Ticket.Infrastructure.Services;

public sealed class JwtTokenService(IConfiguration config) : IJwtTokenService
{
    public string CreateAccessToken(int userId, string roleName, int? providerId, int? clientId)
    {
        var jwt = config.GetSection("Jwt");
        var key = jwt["SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
        var minutes = int.TryParse(jwt["AccessTokenMinutes"], out var m) ? m : 15;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("userId", userId.ToString()),
            new(ClaimTypes.Role, roleName),
            new("roleName", roleName)
        };
        if (providerId is int p) claims.Add(new Claim("providerId", p.ToString()));
        if (clientId is int c) claims.Add(new Claim("clientId", c.ToString()));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(minutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshTokenValue() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}

public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public string Hash(string password) => _inner.HashPassword(new User
    {
        Username = "_",
        FullName = "_",
        PassHash = "_"
    }, password);

    public bool Verify(string hashedPassword, string providedPassword)
    {
        var result = _inner.VerifyHashedPassword(new User
        {
            Username = "_",
            FullName = "_",
            PassHash = hashedPassword
        }, hashedPassword, providedPassword);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal User => accessor.HttpContext?.User
        ?? throw new UnauthorizedAppException("Authentication required.");

    public int UserId
    {
        get
        {
            var raw = User.FindFirstValue("userId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return int.TryParse(raw, out var id) ? id : throw new UnauthorizedAppException("Invalid user claim.");
        }
    }

    public string RoleName =>
        User.FindFirstValue("roleName")
        ?? User.FindFirstValue(ClaimTypes.Role)
        ?? throw new UnauthorizedAppException("Invalid role claim.");

    public int? ProviderId => int.TryParse(User.FindFirstValue("providerId"), out var id) ? id : null;
    public int? ClientId => int.TryParse(User.FindFirstValue("clientId"), out var id) ? id : null;
}
