using System.Text;
using ManageGames.Services.Covers;
using ManageGames.Tests.Infrastructure;

namespace ManageGames.Tests;

public class CoverImageTests
{
    [Fact]
    public void FromBytes_RecognizesTheSupportedFormats()
    {
        Assert.Equal("image/png", CoverImage.FromBytes(TestImages.Png)?.ContentType);
        Assert.Equal("image/jpeg", CoverImage.FromBytes(TestImages.Jpeg)?.ContentType);
        Assert.Equal("image/gif", CoverImage.FromBytes("GIF89a\x01\x00"u8.ToArray())?.ContentType);
        Assert.Equal("image/gif", CoverImage.FromBytes("GIF87a\x01\x00"u8.ToArray())?.ContentType);
        Assert.Equal("image/webp", CoverImage.FromBytes("RIFF\x10\x00\x00\x00WEBPVP8 "u8.ToArray())?.ContentType);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("<html><body>not an image</body></html>")]
    [InlineData("RIFF\u0010\u0000\u0000\u0000WAVEfmt ")]
    [InlineData("RIFF")]
    [InlineData("")]
    public void FromBytes_RejectsEverythingElse(string content)
    {
        Assert.Null(CoverImage.FromBytes(Encoding.Latin1.GetBytes(content)));
    }

    [Fact]
    public void FromBytes_RejectsTooLargeFiles()
    {
        var data = new byte[CoverImage.MaxBytes + 1];
        TestImages.Png.CopyTo(data, 0);

        Assert.Null(CoverImage.FromBytes(data));
        Assert.NotNull(CoverImage.FromBytes(data[..CoverImage.MaxBytes]));
    }
}
