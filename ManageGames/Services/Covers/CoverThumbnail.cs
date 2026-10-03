using SkiaSharp;

namespace ManageGames.Services.Covers;

/// <summary>
/// Small versions of the covers for the game lists: a few kilobytes instead of the full image, which can be
/// hundreds. Twice the displayed size (45 × 64), so they stay sharp on high-resolution screens.
/// </summary>
public static class CoverThumbnail
{
    public const int MaxWidth = 90;
    public const int MaxHeight = 128;
    public const string ContentType = "image/webp";

    /// <summary>
    /// Covers are a few hundred pixels wide; anything far larger is refused before decoding, so a small but
    /// huge-dimensioned file can't take up gigabytes of memory.
    /// </summary>
    public const long MaxPixels = 40_000_000;

    private static readonly SKSamplingOptions Sampling = new(SKCubicResampler.Mitchell);

    /// <summary>The thumbnail as WebP, or null when the data can't be decoded as an image or has more than <paramref name="maxPixels"/>.</summary>
    public static byte[]? Create(byte[] data, long maxPixels = MaxPixels)
    {
        using var encoded = SKData.CreateCopy(data);
        using var codec = SKCodec.Create(encoded);
        if (codec == null || codec.Info.Width <= 0 || codec.Info.Height <= 0 || (long)codec.Info.Width * codec.Info.Height > maxPixels)
        {
            return null;
        }

        // JPEG and WebP can decode at a reduced size right away; the resize below does the rest.
        var scale = Math.Min(1f, Math.Max(2f * MaxWidth / codec.Info.Width, 2f * MaxHeight / codec.Info.Height));
        var decodeSize = codec.GetScaledDimensions(scale);
        using var decoded = new SKBitmap(new SKImageInfo(decodeSize.Width, decodeSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
        {
            return null;
        }

        var ratio = Math.Min(1.0, Math.Min((double)MaxWidth / decoded.Width, (double)MaxHeight / decoded.Height));
        var size = new SKImageInfo(
            Math.Max(1, (int)Math.Round(decoded.Width * ratio)),
            Math.Max(1, (int)Math.Round(decoded.Height * ratio)),
            SKColorType.Rgba8888,
            SKAlphaType.Premul);
        using var resized = decoded.Resize(size, Sampling);
        using var image = resized == null ? null : SKImage.FromBitmap(resized);
        using var webp = image?.Encode(SKEncodedImageFormat.Webp, 80);
        return webp?.ToArray();
    }
}
