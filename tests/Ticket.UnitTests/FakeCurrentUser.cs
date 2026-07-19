using Ticket.Application.Abstractions;

namespace Ticket.UnitTests;

internal sealed class FakeCurrentUser : ICurrentUser
{
    public int UserId { get; init; } = 1;
    public string RoleName { get; init; } = "ProviderManager";
    public int? ProviderId { get; init; }
    public int? ClientId { get; init; }
}
