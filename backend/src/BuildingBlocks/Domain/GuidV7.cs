namespace BuildingBlocks.Domain;

/// <summary>
/// Provides utility methods for generating and inspecting sequential UUID version 7 identifiers.
/// </summary>
public static class GuidV7
{
    private static readonly DateTimeOffset UnixEpoch = DateTimeOffset.UnixEpoch;

    /// <summary>
    /// Creates a new time-ordered UUID version 7.
    /// </summary>
    public static Guid NewGuid()
    {
        return Guid.CreateVersion7();
    }

    /// <summary>
    /// Creates a new time-ordered UUID version 7 with an explicit timestamp.
    /// </summary>
    public static Guid NewGuid(DateTimeOffset timestamp)
    {
        return Guid.CreateVersion7(timestamp);
    }

    /// <summary>
    /// Compares two UUID version 7 values in big-endian lexicographical order.
    /// </summary>
    public static int Compare(Guid left, Guid right)
    {
        Span<byte> leftBytes = stackalloc byte[16];
        Span<byte> rightBytes = stackalloc byte[16];

        left.TryWriteBytes(leftBytes, bigEndian: true, out _);
        right.TryWriteBytes(rightBytes, bigEndian: true, out _);

        return leftBytes.SequenceCompareTo(rightBytes);
    }

    /// <summary>
    /// Extracts the UTC timestamp from a UUID version 7.
    /// </summary>
    public static DateTimeOffset GetTimestamp(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes, bigEndian: true, out _);

        long timestampMs = ((long)bytes[0] << 40)
                         | ((long)bytes[1] << 32)
                         | ((long)bytes[2] << 24)
                         | ((long)bytes[3] << 16)
                         | ((long)bytes[4] << 8)
                         | bytes[5];

        return UnixEpoch.AddMilliseconds(timestampMs);
    }
}
