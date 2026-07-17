namespace Ticket.Application.DTOs;

/// <summary>ProviderManager dashboard stats (openapi ProviderDashboardStats).</summary>
public sealed record ProviderDashboardStats(
    int TotalClients,
    int TotalAgents,
    int OpenTicketsTotal,
    int ClosedTicketsTotal,
    int UnassignedTicketsCount);

public sealed record AgentListItem(
    int Id,
    string FullName,
    string Username,
    bool IsActive,
    int OpenTicketsCount,
    int ClosedTicketsCount);

public sealed record CreateAgentRequest(string FullName, string Username, string PhoneNumber, string Password);
public sealed record UpdateAgentRequest(string FullName, string PhoneNumber, bool IsActive);
public sealed record CreatedAgentResponse(int UserId);
public sealed record AgentTicketStats(int OpenCount, int ClosedCount, string AvgResponseTime);
public sealed record AgentDetail(string FullName, string Username, string PhoneNumber, bool IsActive, AgentTicketStats Stats);

public sealed record ClientListItem(int Id, string Name, string Email, bool IsActive, int OpenTicketsCount);
public sealed record CreateClientRequest(
    string Name,
    string Email,
    string PhoneNumber,
    string ManagerUsername,
    string ManagerFullName,
    string ManagerPassword);
public sealed record UpdateClientRequest(string Name, string Email, string PhoneNumber, bool IsActive);
public sealed record CreatedClientResponse(int ClientId);
public sealed record ClientTicketStats(int Open, int Closed);
public sealed record ClientDetail(
    string Name,
    string Email,
    string Phone,
    bool IsActive,
    int RequesterCount,
    ClientTicketStats TicketStats);
