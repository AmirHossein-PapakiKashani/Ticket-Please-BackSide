namespace Ticket.Domain.Services;

/// <summary>
/// Pure Auto-Assignment selection (DESIGN §6).
/// Open statuses: Open, InProgress, PendingRequesterResponse.
/// Tie-break: oldest LastAssignedAt; null wins (treated as DateTime.MinValue).
/// </summary>
public static class AutoAssignAgent
{
    public sealed record AgentLoad(int AgentId, int OpenTicketCount, DateTime? LastAssignedAt);

    /// <summary>Returns selected agent id, or null when no active agents.</summary>
    public static int? SelectAgentId(IReadOnlyList<AgentLoad> activeAgents)
    {
        if (activeAgents.Count == 0) return null;

        var free = activeAgents.Where(a => a.OpenTicketCount == 0).ToList();
        var candidates = free.Count > 0
            ? free
            : activeAgents.Where(a => a.OpenTicketCount == activeAgents.Min(x => x.OpenTicketCount)).ToList();

        return candidates
            .OrderBy(a => a.LastAssignedAt ?? DateTime.MinValue)
            .ThenBy(a => a.AgentId)
            .First()
            .AgentId;
    }
}
