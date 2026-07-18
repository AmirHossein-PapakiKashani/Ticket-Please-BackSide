using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Application.Data;
using Ticket.Application.DTOs;
using Ticket.Domain;
using Ticket.Domain.Enums;

namespace Ticket.Application.Features.Requester.Commands;

public sealed record SendRequesterMessageCommand(
    int TicketId,
    string? Text,
    IReadOnlyList<UploadFilePart>? Attachments) : IRequest<CreatedMessageResponse>;

public sealed class SendRequesterMessageCommandValidator : AbstractValidator<SendRequesterMessageCommand>
{
    public SendRequesterMessageCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Text) || (x.Attachments?.Count ?? 0) > 0)
            .WithMessage("Message text or attachment is required")
            .WithName("text");
    }
}

public sealed class SendRequesterMessageCommandHandler(
    IApplicationDbContext db,
    ICurrentUser current,
    IFileStorage files) : IRequestHandler<SendRequesterMessageCommand, CreatedMessageResponse>
{
    public async Task<CreatedMessageResponse> Handle(SendRequesterMessageCommand command, CancellationToken cancellationToken)
    {
        var clientId = RequesterHelpers.RequireClientId(current);
        var ticket = await RequesterHelpers.RequireClientTicketAsync(db, command.TicketId, clientId, cancellationToken);
        if (ticket.RequesterId != current.UserId)
            throw new ForbiddenAppException("Only the ticket creator can send messages");

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

        if (ticket.AssignedAgentId is int agentId)
        {
            db.Notifications.Add(new Notification
            {
                UserId = agentId,
                Type = NotificationType.NewMessage,
                RefId = ticket.Id,
                Title = "New message",
                Body = ticket.Topic
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        return new CreatedMessageResponse(message.Id);
    }
}

public sealed record ReopenTicketCommand(int TicketId) : IRequest<SuccessResponse>;

public sealed class ReopenTicketCommandHandler(IApplicationDbContext db, ICurrentUser current)
    : IRequestHandler<ReopenTicketCommand, SuccessResponse>
{
    public async Task<SuccessResponse> Handle(ReopenTicketCommand command, CancellationToken cancellationToken)
    {
        var clientId = RequesterHelpers.RequireClientId(current);
        var ticket = await RequesterHelpers.RequireClientTicketAsync(db, command.TicketId, clientId, cancellationToken);
        if (ticket.RequesterId != current.UserId)
            throw new ForbiddenAppException("Only the ticket creator can reopen this ticket");
        if (ticket.Status is not (TicketStatus.Resolved or TicketStatus.Closed))
            throw new ConflictException("Only resolved/closed tickets can be reopened");

        var previousAgentId = ticket.AssignedAgentId;
        ticket.Status = TicketStatus.Open;
        ticket.ReopenCount += 1;
        ticket.CloseDate = null;
        ticket.SeenByAgentDate = null;
        // Keep AssignedAgentId — do not re-run Auto-Assign (DESIGN §8).

        if (previousAgentId is int agentId)
        {
            db.Notifications.Add(new Notification
            {
                UserId = agentId,
                Type = NotificationType.TicketReopened,
                RefId = ticket.Id,
                Title = "Ticket reopened",
                Body = ticket.Topic
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return new SuccessResponse();
    }
}
