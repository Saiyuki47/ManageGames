using System.Buffers.Binary;
using System.Text;
using ManageGames.Services.Covers;
using ManageGames.Tests.Infrastructure;
using SkiaSharp;

namespace ManageGames.Tests;

public class CoverThumbnailTests
{
    [Theory]
    [InlineData(600, 900, 85, 128)]
    [InlineData(900, 300, 90, 30)]
    [InlineData(40, 60, 40, 60)]
    public void Create_ScalesDownToFit_KeepingTheAspectRatio(int width, int height, int expectedWidth, int expectedHeight)
    {
        var thumbnail = CoverThumbnail.Create(TestImages.Create(width, height));

        Assert.NotNull(thumbnail);
        Assert.Equal("image/webp", CoverImage.DetectContentType(thumbnail));
        Assert.Equal((expectedWidth, expectedHeight), Size(thumbnail));
    }

    [Fact]
    public void Create_ReadsJpegAndWebp()
    {
        Assert.Equal((85, 128), Size(CoverThumbnail.Create(TestImages.Create(600, 900, SKEncodedImageFormat.Jpeg))!));
        Assert.Equal((85, 128), Size(CoverThumbnail.Create(TestImages.Create(600, 900, SKEncodedImageFormat.Webp))!));
    }

    [Fact]
    public void Create_IsMuchSmallerThanTheCover()
    {
        var cover = TestImages.Create(600, 900);

        Assert.True(CoverThumbnail.Create(cover)!.Length * 5 < cover.Length);
    }

    [Fact]
    public void Create_ReturnsNull_ForDataThatIsNoImage()
    {
        Assert.Null(CoverThumbnail.Create(TestImages.Jpeg));
        Assert.Null(CoverThumbnail.Create("<html></html>"u8.ToArray()));
        Assert.Null(CoverThumbnail.Create([]));
    }

    [Fact]
    public void Create_RefusesHugeDimensions_BeforeDecoding()
    {
        // A valid PNG header announcing 10,000 × 10,000 pixels (400 MB decoded), followed by no image data.
        Assert.Null(CoverThumbnail.Create(PngHeader(10_000, 10_000)));

        // The limit itself, with a small image: 20 × 20 = 400 pixels.
        Assert.Null(CoverThumbnail.Create(TestImages.Create(20, 20), maxPixels: 399));
        Assert.NotNull(CoverThumbnail.Create(TestImages.Create(20, 20), maxPixels: 400));
        Assert.Equal(40_000_000, CoverThumbnail.MaxPixels);
    }

    private static (int Width, int Height) Size(byte[] image)
    {
        using var codec = SKCodec.Create(new MemoryStream(image));
        return (codec.Info.Width, codec.Info.Height);
    }

    private static byte[] PngHeader(int width, int height)
    {
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // bit depth
        header[9] = 6; // RGBA
        var chunk = Encoding.ASCII.GetBytes("IHDR").Concat(header).ToArray();
        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(chunk));
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, header.Length);
        return [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. length, .. chunk, .. crc];
    }

    // The CRC-32 PNG chunks carry, so the header is accepted as valid.
    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }
        return ~crc;
    }
}
