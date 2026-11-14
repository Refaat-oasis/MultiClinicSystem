using System.Collections.Concurrent;
using BuildingBlocks.Application;

namespace BuildingBlocks.Infrastructure;

/// <summary>
/// Local disk + in-memory storage backend for development and automated integration testing.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _memoryCache = new(StringComparer.Ordinal);
    private readonly string _rootDirectory;

    public LocalFileStorage()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "ClinicPlatformStorage");
    }

    public async Task<string> SaveAsync(
        string storageKey,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentNullException.ThrowIfNull(content);

        string sanitizedKey = storageKey.Replace('\\', '/').TrimStart('/');
        byte[] copy = (byte[])content.Clone();
        _memoryCache[sanitizedKey] = copy;

        string fullPath = ResolveSafePath(sanitizedKey);
        string? parentDir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        await File.WriteAllBytesAsync(fullPath, copy, cancellationToken);
        return sanitizedKey;
    }

    public async Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        string sanitizedKey = storageKey.Replace('\\', '/').TrimStart('/');

        if (_memoryCache.TryGetValue(sanitizedKey, out byte[]? cached))
        {
            return (byte[])cached.Clone();
        }

        string fullPath = ResolveSafePath(sanitizedKey);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        byte[] loaded = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        _memoryCache[sanitizedKey] = loaded;
        return (byte[])loaded.Clone();
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        string sanitizedKey = storageKey.Replace('\\', '/').TrimStart('/');
        _memoryCache.TryRemove(sanitizedKey, out _);

        string fullPath = ResolveSafePath(sanitizedKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string ResolveSafePath(string sanitizedKey)
    {
        string combined = Path.GetFullPath(Path.Combine(_rootDirectory, sanitizedKey));
        string rootFull = Path.GetFullPath(_rootDirectory);
        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid storage key path traversal detected.");
        }

        return combined;
    }
}

