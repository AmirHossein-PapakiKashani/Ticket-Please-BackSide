using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Application.DTOs;
using Ticket.Domain;
using Ticket.Domain.Enums;
using Ticket.Domain.Services;
using TicketEntity = Ticket.Domain.Ticket;

namespace Ticket.Application.Features.Requester.Commands;

public sealed record UploadFilePart(Stream Content, string FileName, string ContentType);

public sealed record CreateTicketCommand(
    string Topic,
    string Text,
    TicketPriority? Priority,
    IReadOnlyList<UploadFilePart>? Attachments) : IRequest<CreatedTicketResponse>;

public sealed class CreateTicketCommandValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketCommandValidator()
    {
        RuleFor(x => x.Topic).NotEmpty().MinimumLength(3).MaximumLength(300);
        RuleFor(x => x.Text).NotEmpty();
        RuleFor(x => x.Priority).IsInEnum().When(x => x.Priority.HasValue);
    }
}

public sealed class CreateTicketCommandHandler(
    IApplicationDbContext db,
    ICurrentUser current,
    IFileStorage files) : IRequestHandler<CreateTicketCommand, CreatedTicketResponse>
{
    public async Task<CreatedTicketResponse> Handle(CreateTicketCommand command, CancellationToken cancellationToken)
    {
        var clientId = RequesterHelpers.RequireClientId(current);
        var client = await db.Clients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clientId && c.IsActive && !c.IsDeleted, cancellationToken)
            ?? throw new ForbiddenAppException("Client account is not active");

        var ticket = new TicketEntity
        {
            ClientId = clientId,
            ProviderId = client.ProviderId,
            RequesterId = current.UserId,
            Topic = command.Topic.Trim(),
            Status = TicketStatus.Open,
            Priority = command.Priority ?? TicketPriority.Medium,
            OpenDate = DateTime.UtcNow
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);

        var agentRoleId = await db.Roles.Where(r => r.Name == RoleNames.Agent).Select(r => r.Id).FirstAsync(cancellationToken);
        var agents = await db.Users.Where(u =>
                u.ProviderId == client.ProviderId &&
                u.RoleId == agentRoleId &&
                u.IsActive &&
                !u.IsDeleted)
            .ToListAsync(cancellationToken);

        var loads = new List<AutoAssignAgent.AgentLoad>();
        foreach (var agent in agents)
        {
            var openCount = await db.Tickets.CountAsync(t =>
                t.AssignedAgentId == agent.Id &&
                !t.IsDeleted &&
                RequesterHelpers.OpenStatuses.Contains(t.Status),
                cancellationToken);
            loads.Add(new AutoAssignAgent.AgentLoad(agent.Id, openCount, agent.LastAssignedAt));
        }

        var selectedId = AutoAssignAgent.SelectAgentId(loads);
        if (selectedId is int agentId)
        {
            ticket.AssignedAgentId = agentId;
            var selected = agents.First(a => a.Id == agentId);
            selected.LastAssignedAt = DateTime.UtcNow;
            db.Notifications.Add(new Notification
            {
                UserId = agentId,
                Type = NotificationType.NewTicketAssigned,
                RefId = ticket.Id,
                Title = "New ticket assigned",
                Body = ticket.Topic
            });
        }

        var firstMessage = new TicketMessage
        {
            TicketId = ticket.Id,
            SenderId = current.UserId,
            Text = command.Text.Trim()
        };
        db.TicketMessages.Add(firstMessage);
        await db.SaveChangesAsync(cancellationToken);

        if (command.Attachments is { Count: > 0 })
        {
            foreach (var file in command.Attachments)
            {
                await using (file.Content)
                {
                    var stored = await files.SaveAsync(file.Content, file.FileName, file.ContentType, cancellationToken);
                    db.Attachments.Add(new Attachment
                    {
                        MessageId = firstMessage.Id,
                        FileUrl = stored.FileUrl,
                        FileName = stored.FileName,
                        FileType = stored.FileType,
                        FileSizeKB = stored.FileSizeKB
                    });
                }
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        return new CreatedTicketResponse(ticket.Id);
    }
}
