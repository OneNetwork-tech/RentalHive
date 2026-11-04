using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using RentalHive.Application.Contracts;
using RentalHive.Domain.Common;

namespace RentalHive.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string Section = "Storage";
    public string ImageDirectory { get; set; } = "data/images";
    public long MaxImageBytes { get; set; } = 5 * 1024 * 1024;
}

/// <summary>
/// Stores listing photos on disk under random names. The file type is detected
/// from magic bytes (JPEG, PNG, WebP); the client's file name and MIME type are ignored.
/// </summary>
public sealed class LocalImageStore : IImageStore
{
    private readonly string _root;
    private readonly long _maxBytes;

    public LocalImageStore(IOptions<StorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.ImageDirectory);
        _maxBytes = options.Value.MaxImageBytes;
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredImage> SaveAsync(Stream content, long length, CancellationToken ct = default)
    {
        if (length <= 0) throw new DomainException("The image is empty.");
        if (length > _maxBytes) throw new DomainException($"Images can be at most {_maxBytes / 1024 / 1024} MB.");

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > _maxBytes) throw new DomainException($"Images can be at most {_maxBytes / 1024 / 1024} MB.");
            buffer.Write(chunk, 0, read);
        }
        var bytes = buffer.ToArray();
        var (contentType, extension) = Sniff(bytes) ?? throw new DomainException("Only JPEG, PNG and WebP images are accepted.");
        var fileName = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant() + extension;
        await File.WriteAllBytesAsync(Path.Combine(_root, fileName), bytes, ct).ConfigureAwait(false);
        return new StoredImage(fileName, contentType);
    }

    public void Delete(string fileName)
    {
        var path = ResolvePath(fileName);
        if (path is not null && File.Exists(path)) File.Delete(path);
    }

    /// <summary>Full path for a stored file name, or null if the name is not one we generate.</summary>
    public string? ResolvePath(string fileName)
    {
        if (!IsValidName(fileName)) return null;
        return Path.Combine(_root, fileName);
    }

    /// <summary>Only names this store generates: 32 hex chars + a known extension.</summary>
    public static bool IsValidName(string fileName) =>
        System.Text.RegularExpressions.Regex.IsMatch(fileName, "^[a-f0-9]{32}\\.(jpg|png|webp)$");

    public static (string ContentType, string Extension)? Sniff(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ("image/jpeg", ".jpg");
        if (b.Length >= 8 && b[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return ("image/png", ".png");
        if (b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8) && b[8..12].SequenceEqual("WEBP"u8)) return ("image/webp", ".webp");
        return null;
    }
}
