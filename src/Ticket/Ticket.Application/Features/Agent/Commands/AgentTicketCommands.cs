using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Application.DTOs;
using Ticket.Application.Features.Requester.Commands;
using Ticket.Domain;
using Ticket.Domain.Enums;

namespace Ticket.Application.Features.AgentArea.Commands;

public sealed record SendAgentMessageCommand(
    int TicketId,
    string? Text,
    IReadOnlyList<UploadFilePart>? Attachments) : IRequest<CreatedMessageResponse>;

public sealed class SendAgentMessageCommandValidator : AbstractValidator<SendAgentMessageCommand>
{
    public SendAgentMessageCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Text) || (x.Attachments?.Count ?? 0) > 0)
            .WithMessage("Message text or attachment is required")
            .WithName("text");
    }
}

public sealed class SendAgentMessageCommandHandler(
    IApplicationDbContext db,
    ICurrentUser current,
    IFileStorage files) : IRequestHandler<SendAgentMessageCommand, CreatedMessageResponse>
{
    public async Task<CreatedMessageResponse> Handle(SendAgentMessageCommand command, CancellationToken cancellationToken)
    {
        var providerId = AgentHelpers.RequireProviderId(current);
        var ticket = await AgentHelpers.RequireProviderTicketAsync(db, command.TicketId, providerId, cancellationToken);
        if (ticket.AssignedAgentId != current.UserId)
            throw new ForbiddenAppException("You are no longer responsible for this ticket");

        var message = new TicketMessage
        {
            TicketId = ticket.Id,
            SenderId = current.UserId,
            Text = command.Text?.Trim() ?? string.Empty
        };
        db.TicketMessages.Add(message);
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
                        MessageId = message.Id,
                        FileUrl = stored.FileUrl,
                        FileName = stored.FileName,
                        FileType = stored.FileType,
                        FileSizeKB = stored.FileSizeKB
                    });
                }
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        db.Notifications.Add(new Notification
        {
            UserId = ticket.RequesterId,
            Type = NotificationType.NewMessage,
            RefId = ticket.Id,
            Title = "New message",
            Body = ticket.Topic
        });
        await db.SaveChangesAsync(cancellationToken);
        return new CreatedMessageResponse(message.Id);
    }
}

public sealed record MarkTicketSeenCommand(int TicketId) : IRequest<SuccessResponse>;

public sealed class MarkTicketSeenCommandHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<MarkTicketSeenCommand, SuccessResponse>
{
    public async Task<SuccessResponse> Handle(MarkTicketSeenCommand command, CancellationToken cancellationToken)
    {
        var providerId = AgentHelpers.RequireProviderId(current);
        var ticket = await AgentHelpers.RequireProviderTicketAsync(db, command.TicketId, providerId, cancellationToken);
        ticket.SeenByAgentDate = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }
}

public sealed record ReassignTicketCommand(int TicketId, int NewAgentId) : IRequest<SuccessResponse>;

public sealed class ReassignTicketCommandHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<ReassignTicketCommand, SuccessResponse>
{
    public async Task<SuccessResponse> Handle(ReassignTicketCommand command, CancellationToken cancellationToken)
    {
        var providerId = AgentHelpers.RequireProviderId(current);
        var ticket = await AgentHelpers.RequireProviderTicketAsync(db, command.TicketId, providerId, cancellationToken);
        if (ticket.AssignedAgentId != current.UserId)
            throw new ForbiddenAppException("You do not have permission to reassign this ticket");

        var agentRoleId = await db.Roles.Where(r => r.Name == RoleNames.Agent).Select(r => r.Id).FirstAsync(cancellationToken);
        var newAgent = await db.Users.FirstOrDefaultAsync(u => u.Id == command.NewAgentId, cancellationToken)
            ?? throw new NotFoundException("Selected agent was not found");
        if (!newAgent.IsActive || newAgent.IsDeleted || newAgent.ProviderId != providerId || newAgent.RoleId != agentRoleId)
            throw new BadRequestAppException("Selected agent is not valid");

        ticket.AssignedAgentId = newAgent.Id;
        ticket.SeenByAgentDate = null;
        newAgent.LastAssignedAt = DateTime.UtcNow;
        db.Notifications.Add(new Notification
        {
            UserId = newAgent.Id,
            Type = NotificationType.TicketReassigned,
            RefId = ticket.Id,
            Title = "Ticket reassigned",
            Body = ticket.Topic
        });
        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }
}

public sealed record UpdateTicketStatusCommand(int TicketId, TicketStatus NewStatus) : IRequest<SuccessResponse>;

public sealed class UpdateTicketStatusCommandValidator : AbstractValidator<UpdateTicketStatusCommand>
{
    public UpdateTicketStatusCommandValidator() => RuleFor(x => x.NewStatus).IsInEnum();
}

public sealed class UpdateTicketStatusCommandHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<UpdateTicketStatusCommand, SuccessResponse>
{
    public async Task<SuccessResponse> Handle(UpdateTicketStatusCommand command, CancellationToken cancellationToken)
    {
        var providerId = AgentHelpers.RequireProviderId(current);
        var ticket = await AgentHelpers.RequireProviderTicketAsync(db, command.TicketId, providerId, cancellationToken);
        if (ticket.AssignedAgentId != current.UserId)
            throw new ForbiddenAppException("You are no longer responsible for this ticket");

        ticket.Status = command.NewStatus;
        if (command.NewStatus is TicketStatus.Resolved or TicketStatus.Closed)
        {
            ticket.CloseDate = DateTime.UtcNow;
            db.Notifications.Add(new Notification
            {
                UserId = ticket.RequesterId,
                Type = command.NewStatus == TicketStatus.Resolved ? NotificationType.TicketResolved : NotificationType.TicketClosed,
                RefId = ticket.Id,
                Title = command.NewStatus == TicketStatus.Resolved ? "Ticket resolved" : "Ticket closed",
                Body = ticket.Topic
            });
        }
        else
        {
            ticket.CloseDate = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }
}

public sealed record UpdateTicketPriorityCommand(int TicketId, TicketPriority Priority) : IRequest<SuccessResponse>;

public sealed class UpdateTicketPriorityCommandValidator : AbstractValidator<UpdateTicketPriorityCommand>
{
    public UpdateTicketPriorityCommandValidator() => RuleFor(x => x.Priority).IsInEnum();
}

public sealed class UpdateTicketPriorityCommandHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<UpdateTicketPriorityCommand, SuccessResponse>
{
    public async Task<SuccessResponse> Handle(UpdateTicketPriorityCommand command, CancellationToken cancellationToken)
    {
        var providerId = AgentHelpers.RequireProviderId(current);
        var ticket = await AgentHelpers.RequireProviderTicketAsync(db, command.TicketId, providerId, cancellationToken);
        ticket.Priority = command.Priority;
        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }
}

public sealed record CreateTicketNoteCommand(int TicketId, string Text) : IRequest<CreatedNoteResponse>;

public sealed class CreateTicketNoteCommandValidator : AbstractValidator<CreateTicketNoteCommand>
{
    public CreateTicketNoteCommandValidator() => RuleFor(x => x.Text).NotEmpty();
}

public sealed class CreateTicketNoteCommandHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<CreateTicketNoteCommand, CreatedNoteResponse>
{
    public async Task<CreatedNoteResponse> Handle(CreateTicketNoteCommand command, CancellationToken cancellationToken)
    {
        var providerId = AgentHelpers.RequireProviderId(current);
        _ = await AgentHelpers.RequireProviderTicketAsync(db, command.TicketId, providerId, cancellationToken);
        var note = new TicketNote
        {
            TicketId = command.TicketId,
            AuthorId = current.UserId,
            Text = command.Text.Trim()
        };
        db.TicketNotes.Add(note);
        await db.SaveChangesAsync(cancellationToken);
        return new CreatedNoteResponse(note.Id);
    }
}
