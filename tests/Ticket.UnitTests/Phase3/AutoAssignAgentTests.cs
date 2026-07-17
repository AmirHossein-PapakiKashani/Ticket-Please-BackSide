using Ticket.Domain.Services;
using Xunit;

namespace Ticket.UnitTests.Phase3;

public sealed class AutoAssignAgentTests
{
    [Fact]
    public void SelectAgent_NoActiveAgents_ReturnsNull()
    {
        Assert.Null(AutoAssignAgent.SelectAgentId([]));
    }

    [Fact]
    public void SelectAgent_PrefersZeroOpen_AndNullLastAssignedWins()
    {
        var selected = AutoAssignAgent.SelectAgentId(
        [
            new(1, 3, new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc)),
            new(2, 0, new DateTime(2026, 1, 1, 9, 30, 0, DateTimeKind.Utc)),
            new(3, 0, null)
        ]);
        Assert.Equal(3, selected);
    }

    [Fact]
    public void SelectAgent_UsesLowestOpenCount_ThenOldestLastAssigned()
    {
        var selected = AutoAssignAgent.SelectAgentId(
        [
            new(1, 2, new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc)),
            new(2, 1, new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc)),
            new(3, 1, new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc))
        ]);
        Assert.Equal(3, selected);
    }
}
