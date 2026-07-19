namespace Ticket.Application.Abstractions;

/// <summary>Issues and validates JWT access tokens and refresh tokens.</summary>
public interface IJwtTokenService
{
    string CreateAccessToken(int userId, string roleName, int? providerId, int? clientId);
    string CreateRefreshTokenValue();
    string HashToken(string rawToken);
}

/// <summary>Current authenticated user from JWT claims.</summary>
public interface ICurrentUser
{
    int UserId { get; }
    string RoleName { get; }
    int? ProviderId { get; }
    int? ClientId { get; }
}

/// <summary>Password hashing abstraction.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hashedPassword, string providedPassword);
}

/// <summary>Stores uploaded ticket attachments (local stub for MVP).</summary>
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken);
}

public sealed record StoredFile(string FileUrl, string FileName, string FileType, int FileSizeKB);
