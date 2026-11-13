using System.Buffers.Binary;
using System.Text;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public sealed class LogoImageProcessorTests
{
    [Fact]
    public void Given_ValidPngWithMetadataAndTrailingBytes_When_Processing_Then_StripsTextMetadataAndTrailingPayload()
    {
        byte[] validPngWithTextAndPolyglot = BuildPngWithTextChunkAndTrailingGarbage();

        var result = LogoImageProcessor.ProcessAndSanitize(validPngWithTextAndPolyglot, "image/png");

        result.IsSuccess.Should().BeTrue();
        result.Value.ContentType.Should().Be("image/png");
        string cleanAscii = Encoding.ASCII.GetString(result.Value.FullBytes);
        cleanAscii.Should().NotContain("tEXt");
        cleanAscii.Should().NotContain("EXIF_SECRET");
        cleanAscii.Should().NotContain("POLYGLOT");
    }

    [Fact]
    public void Given_SvgPayload_When_Processing_Then_RejectsWithLogoSvgRejected()
    {
        byte[] svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");

        var result = LogoImageProcessor.ProcessAndSanitize(svg, "image/svg+xml");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("LOGO_SVG_REJECTED");
    }

    [Fact]
    public void Given_OversizedPayload_When_Processing_Then_RejectsWithLogoTooLarge()
    {
        byte[] oversized = new byte[LogoImageProcessor.MaxSizeBytes + 10];
        oversized[0] = 0xFF;
        oversized[1] = 0xD8;
        oversized[2] = 0xFF;

        var result = LogoImageProcessor.ProcessAndSanitize(oversized, "image/jpeg");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("LOGO_TOO_LARGE");
    }

    [Fact]
    public void Given_InvalidBinaryPayload_When_Processing_Then_RejectsWithLogoInvalidType()
    {
        byte[] binary = Encoding.UTF8.GetBytes("MZ_NOT_AN_IMAGE");

        var result = LogoImageProcessor.ProcessAndSanitize(binary, "application/octet-stream");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("LOGO_INVALID_TYPE");
    }

    private static byte[] BuildPngWithTextChunkAndTrailingGarbage()
    {
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(ms, "IHDR", new byte[13]);
        WriteChunk(ms, "tEXt", Encoding.ASCII.GetBytes("Comment\0EXIF_SECRET"));
        WriteChunk(ms, "IDAT", [0x78, 0x9C, 0x63, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01]);
        WriteChunk(ms, "IEND", Array.Empty<byte>());
        byte[] polyglot = Encoding.ASCII.GetBytes("POLYGLOT_SCRIPT_PAYLOAD");
        ms.Write(polyglot, 0, polyglot.Length);
        return ms.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        byte[] lenBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lenBytes, (uint)data.Length);
        stream.Write(lenBytes, 0, 4);
        stream.Write(Encoding.ASCII.GetBytes(type), 0, 4);
        stream.Write(data, 0, data.Length);
        stream.Write(new byte[4], 0, 4); // CRC placeholder for test
    }
}

