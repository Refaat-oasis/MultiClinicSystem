namespace BuildingBlocks.Application;

/// <summary>
/// Abstraction for storing and retrieving binary assets such as clinic logos, avatars, and clinical attachments.
/// </summary>
public interface IFileStorage
{
    Task<string> SaveAsync(string storageKey, byte[] content, string contentType, CancellationToken cancellationToken = default);
    Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}

