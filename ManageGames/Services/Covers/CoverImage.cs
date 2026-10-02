namespace ManageGames.Services.Covers;

/// <summary>An image file that was checked to be a JPEG, PNG, WebP or GIF of acceptable size.</summary>
public sealed record CoverImage(byte[] Data, string ContentType)
{
    /// <summary>Upper limit for uploads and downloads; real covers are far smaller.</summary>
    public const int MaxBytes = 5 * 1024 * 1024;

    /// <summary>For the file picker's accept attribute.</summary>
    public const string AcceptedTypes = "image/jpeg,image/png,image/webp,image/gif";

    /// <summary>
    /// Returns the image, or null when the data is too large or no supported image. The format is recognized
    /// from the file's first bytes: file names and Content-Type headers can lie, and the browser must never get
    /// a file labeled as an image that is really something else (e.g. HTML or SVG with scripts).
    /// </summary>
    public static CoverImage? FromBytes(byte[] data)
    {
        if (data.Length > MaxBytes)
        {
            return null;
        }

        var contentType = DetectContentType(data);
        return contentType == null ? null : new CoverImage(data, contentType);
    }

    public static string? DetectContentType(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }
        if (data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }
        if (data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8))
        {
            return "image/gif";
        }
        return null;
    }
}
