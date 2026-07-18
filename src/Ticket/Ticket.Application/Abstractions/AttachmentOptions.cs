namespace Ticket.Application.Abstractions;

/// <summary>Phase 6 attachment limits published for FE interceptors.</summary>
public sealed class AttachmentOptions
{
    public const string SectionName = "Attachments";
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
    public string[] AllowedContentTypes { get; set; } =
    [
        "image/png",
        "image/jpeg",
        "application/pdf",
        "text/plain"
    ];
}
