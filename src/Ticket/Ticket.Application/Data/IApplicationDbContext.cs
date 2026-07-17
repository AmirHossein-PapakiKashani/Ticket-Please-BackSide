using Microsoft.EntityFrameworkCore;
using Ticket.Domain;
using Ticket.Domain.Enums;

namespace Ticket.Application.Data;

/// <summary>Abstraction over EF Core persistence for Application handlers.</summary>
public interface IApplicationDbContext
{
    DbSet<Role> Roles { get; }
    DbSet<User> Users { get; }
    DbSet<Provider> Providers { get; }
    DbSet<Client> Clients { get; }
    DbSet<Plan> Plans { get; }
    DbSet<ProviderSubscription> ProviderSubscriptions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<Notification> Notifications { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
