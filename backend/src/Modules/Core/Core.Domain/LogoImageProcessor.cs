using System.Buffers.Binary;
using System.Text;
using BuildingBlocks.Domain;

namespace Core.Domain;

/// <summary>
/// Validates image uploads (PNG/JPG/WebP up to 2MB, rejects SVG/polyglots) and strips metadata segments.
/// </summary>
public static class LogoImageProcessor
{
    public const int MaxSizeBytes = 2 * 1024 * 1024;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static Result<LogoSanitizeResult> ProcessAndSanitize(byte[]? rawBytes, string? declaredContentType)
    {
        if (rawBytes == null || rawBytes.Length == 0)
        {
            return Result<LogoSanitizeResult>.Failure(Error.Validation("LOGO_EMPTY", "Logo file cannot be empty."));
        }

        if (rawBytes.Length > MaxSizeBytes)
        {
            return Result<LogoSanitizeResult>.Failure(Error.Validation("LOGO_TOO_LARGE", "Logo file exceeds the 2 MB maximum limit."));
        }

        if (!string.IsNullOrWhiteSpace(declaredContentType) &&
            declaredContentType.Contains("svg", StringComparison.OrdinalIgnoreCase))
        {
            return Result<LogoSanitizeResult>.Failure(Error.Validation("LOGO_SVG_REJECTED", "SVG logos are not permitted."));
        }

        int inspectLen = Math.Min(rawBytes.Length, 512);
        string headText = Encoding.UTF8.GetString(rawBytes, 0, inspectLen);
        if (headText.Contains("<svg", StringComparison.OrdinalIgnoreCase) ||
            headText.Contains("<!doctype svg", StringComparison.OrdinalIgnoreCase) ||
            (headText.Contains("<?xml", StringComparison.OrdinalIgnoreCase) &&
             headText.Contains("svg", StringComparison.OrdinalIgnoreCase)))
        {
            return Result<LogoSanitizeResult>.Failure(Error.Validation("LOGO_SVG_REJECTED", "SVG or XML logos are not permitted."));
        }

        if (IsPng(rawBytes))
        {
            byte[] cleanedPng = SanitizePng(rawBytes);
            if (cleanedPng.Length == 0)
            {
                return Result<LogoSanitizeResult>.Failure(Error.Validation("LOGO_INVALID_TYPE", "Corrupt PNG image."));
            }

            return Result<LogoSanitizeResult>.Success(new LogoSanitizeResult("image/png", cleanedPng, cleanedPng));
        }

        if (IsJpeg(rawBytes))
        {
            byte[] cleanedJpeg = SanitizeJpeg(rawBytes);
            if (cleanedJpeg.Length == 0)
            {
                return Result<LogoSanitizeResult>.Failure(Error.Validation("LOGO_INVALID_TYPE", "Corrupt JPEG image."));
            }

            return Result<LogoSanitizeResult>.Success(new LogoSanitizeResult("image/jpeg", cleanedJpeg, cleanedJpeg));
        }

        if (IsWebP(rawBytes))
        {
            byte[] cleanedWebp = SanitizeWebP(rawBytes);
            if (cleanedWebp.Length == 0)
            {
                return Result<LogoSanitizeResult>.Failure(Error.Validation("LOGO_INVALID_TYPE", "Corrupt WebP image."));
            }

            return Result<LogoSanitizeResult>.Success(new LogoSanitizeResult("image/webp", cleanedWebp, cleanedWebp));
        }

        return Result<LogoSanitizeResult>.Failure(Error.Validation(
            "LOGO_INVALID_TYPE",
            "Only PNG, JPEG, or WebP images up to 2 MB are supported."));
    }

    private static bool IsPng(byte[] bytes)
    {
        return bytes.Length >= PngSignature.Length &&
               bytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature);
    }

    private static bool IsJpeg(byte[] bytes)
    {
        return bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
    }

    private static bool IsWebP(byte[] bytes)
    {
        return bytes.Length >= 12 &&
               bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
               bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P';
    }

    private static byte[] SanitizePng(byte[] bytes)
    {
        using var output = new MemoryStream();
        output.Write(PngSignature, 0, PngSignature.Length);

        int offset = PngSignature.Length;
        bool sawIend = false;
        while (offset + 12 <= bytes.Length)
        {
            uint chunkLength = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
            if (chunkLength > int.MaxValue || offset + 12 + (long)chunkLength > bytes.Length)
            {
                return Array.Empty<byte>();
            }

            string chunkType = Encoding.ASCII.GetString(bytes, offset + 4, 4);
            int totalChunkBytes = 12 + (int)chunkLength;

            if (chunkType is "IHDR" or "PLTE" or "tRNS" or "IDAT" or "IEND")
            {
                output.Write(bytes, offset, totalChunkBytes);
            }

            offset += totalChunkBytes;
            if (chunkType == "IEND")
            {
                sawIend = true;
                break;
            }
        }

        return sawIend ? output.ToArray() : Array.Empty<byte>();
    }

    private static byte[] SanitizeJpeg(byte[] bytes)
    {
        using var output = new MemoryStream();
        output.WriteByte(0xFF);
        output.WriteByte(0xD8);

        int offset = 2;
        bool sawEoi = false;

        while (offset + 2 <= bytes.Length)
        {
            if (bytes[offset] != 0xFF)
            {
                break;
            }

            byte marker = bytes[offset + 1];
            offset += 2;

            if (marker == 0xD9)
            {
                output.WriteByte(0xFF);
                output.WriteByte(0xD9);
                sawEoi = true;
                break;
            }

            if (marker == 0xDA)
            {
                // Start of Scan: copy remaining bytes up to EOI (0xFF 0xD9)
                output.WriteByte(0xFF);
                output.WriteByte(marker);
                int eoiIndex = FindJpegEoi(bytes, offset);
                if (eoiIndex >= offset)
                {
                    output.Write(bytes, offset, eoiIndex - offset + 2);
                    sawEoi = true;
                }
                else
                {
                    output.Write(bytes, offset, bytes.Length - offset);
                }

                break;
            }

            if (marker is >= 0xD0 and <= 0xD7 or 0x01)
            {
                output.WriteByte(0xFF);
                output.WriteByte(marker);
                continue;
            }

            if (offset + 2 > bytes.Length)
            {
                return Array.Empty<byte>();
            }

            ushort segmentLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
            if (segmentLength < 2 || offset + segmentLength > bytes.Length)
            {
                return Array.Empty<byte>();
            }

            bool isMetadataSegment = (marker >= 0xE1 && marker <= 0xEF) || marker == 0xFE;
            if (!isMetadataSegment)
            {
                output.WriteByte(0xFF);
                output.WriteByte(marker);
                output.Write(bytes, offset, segmentLength);
            }

            offset += segmentLength;
        }

        return sawEoi || output.Length > 2 ? output.ToArray() : Array.Empty<byte>();
    }

    private static int FindJpegEoi(byte[] bytes, int startOffset)
    {
        for (int i = bytes.Length - 2; i >= startOffset; i--)
        {
            if (bytes[i] == 0xFF && bytes[i + 1] == 0xD9)
            {
                return i;
            }
        }

        return -1;
    }

    private static byte[] SanitizeWebP(byte[] bytes)
    {
        if (bytes.Length < 12)
        {
            return Array.Empty<byte>();
        }

        using var payload = new MemoryStream();
        int offset = 12;
        while (offset + 8 <= bytes.Length)
        {
            string fourCc = Encoding.ASCII.GetString(bytes, offset, 4);
            uint chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4, 4));
            int paddedSize = (int)chunkSize + ((int)chunkSize % 2);
            if (offset + 8 + paddedSize > bytes.Length)
            {
                break;
            }

            if (fourCc is not ("EXIF" or "XMP "))
            {
                payload.Write(bytes, offset, 8 + paddedSize);
            }

            offset += 8 + paddedSize;
        }

        byte[] inner = payload.ToArray();
        if (inner.Length == 0)
        {
            // Minimal valid RIFF header truncation
            int declaredSize = Math.Min(bytes.Length, (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4)) + 8);
            byte[] slice = new byte[declaredSize];
            Buffer.BlockCopy(bytes, 0, slice, 0, declaredSize);
            return slice;
        }

        byte[] result = new byte[12 + inner.Length];
        Encoding.ASCII.GetBytes("RIFF", 0, 4, result, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4, 4), (uint)(4 + inner.Length));
        Encoding.ASCII.GetBytes("WEBP", 0, 4, result, 8);
        Buffer.BlockCopy(inner, 0, result, 12, inner.Length);
        return result;
    }
}

