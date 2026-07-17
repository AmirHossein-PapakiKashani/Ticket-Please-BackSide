using Ticket.Application.Abstractions;

namespace Ticket.Infrastructure.Services;

/// <summary>Writes attachments under ./wwwroot/uploads (MVP local stub).</summary>
public sealed class LocalFileStorage : IFileStorage
{
    public async Task<StoredFile> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "file.bin";
        var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(folder);
        var storedName = $"{Guid.NewGuid():N}_{safeName}";
        var fullPath = Path.Combine(folder, storedName);
        await using var fs = File.Create(fullPath);
        await content.CopyToAsync(fs, cancellationToken);
        var sizeKb = (int)Math.Max(1, Math.Ceiling(fs.Length / 1024.0));
        return new StoredFile($"/uploads/{storedName}", safeName, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType, sizeKb);
    }
}
