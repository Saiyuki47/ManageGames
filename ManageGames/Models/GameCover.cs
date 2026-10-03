namespace ManageGames.Models;

/// <summary>
/// The cover image of a game, stored in the database next to it, so backups contain it and every instance of
/// the app serves it. Kept in its own table so the game lists never load the image data.
/// </summary>
public class GameCover : ITimestamped
{
    public int GameId { get; set; }
    public Game? Game { get; set; }

    /// <summary>Changes with every new image; part of the image URL, so browsers may cache each version forever.</summary>
    public Guid Version { get; set; }

    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];

    /// <summary>
    /// A small WebP version for the game lists (see <see cref="Services.Covers.CoverThumbnail"/>). Null until it is
    /// made; empty when the image can't be decoded, so the lists fall back to the full image.
    /// </summary>
    public byte[]? Thumbnail { get; set; }

    /// <summary>Where the image came from: a cover source such as "IGDB", or <see cref="UploadSource"/>.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// True when the automatic search picked it; false when the user chose or uploaded it. Only automatic covers
    /// are replaced by <c>scrape-covers --refresh</c>.
    /// </summary>
    public bool IsAutomatic { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public const string UploadSource = "Upload";
}
