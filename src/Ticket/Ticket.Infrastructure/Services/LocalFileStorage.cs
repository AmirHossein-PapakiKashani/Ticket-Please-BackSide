using Microsoft.Extensions.Options;
using Ticket.Application;
using Ticket.Application.Abstractions;

namespace Ticket.Infrastructure.Services;

/// <summary>Writes attachments under ./wwwroot/uploads with Phase 6 size/MIME checks.</summary>
public sealed class LocalFileStorage(IOptions<AttachmentOptions> options) : IFileStorage
{
    public async Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken)
    {
        var opts = options.Value;
        var type = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        if (!opts.AllowedContentTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
            throw new BadRequestAppException($"File type '{type}' is not allowed.");

        if (content.CanSeek)
        {
            if (content.Length > opts.MaxFileSizeBytes)
                throw new BadRequestAppException($"File exceeds maximum size of {opts.MaxFileSizeBytes} bytes.");
        }

        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "file.bin";
        var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(folder);
        var storedName = $"{Guid.NewGuid():N}_{safeName}";
        var fullPath = Path.Combine(folder, storedName);

        await using var fs = File.Create(fullPath);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            total += read;
            if (total > opts.MaxFileSizeBytes)
            {
                await fs.DisposeAsync();
                File.Delete(fullPath);
                throw new BadRequestAppException($"File exceeds maximum size of {opts.MaxFileSizeBytes} bytes.");
            }
            await fs.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        var sizeKb = (int)Math.Max(1, Math.Ceiling(total / 1024.0));
        return new StoredFile($"/uploads/{storedName}", safeName, type, sizeKb);
    }
}
