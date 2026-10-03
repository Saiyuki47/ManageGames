namespace ManageGames.Services.Covers;

/// <summary>A source of cover images, such as IGDB. One implementation per website.</summary>
public interface ICoverProvider
{
    /// <summary>Shown to users ("Cover from IGDB") and used in the <see cref="CoverOptions.Providers"/> setting.</summary>
    string Name { get; }

    /// <summary>False while the source's API keys are missing; the source is skipped then.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Whether the address points to this source's image servers. Images are only ever downloaded from
    /// there, so a crafted form can't make the app fetch arbitrary addresses.
    /// </summary>
    bool IsOwnImage(Uri url);

    /// <summary>Covers of the games whose title resembles <paramref name="title"/>, best matches first.</summary>
    Task<IReadOnlyList<CoverCandidate>> SearchAsync(string title, CancellationToken cancellationToken);

    /// <summary>
    /// The address actually requested for one of this source's images. Sources that need credentials in the
    /// address add them only here, so they never appear in pages, forms or logs.
    /// </summary>
    Uri GetDownloadUrl(Uri imageUrl)
    {
        return imageUrl;
    }

    /// <summary>How many of this source's images may be downloaded at once; some sources limit parallel requests.</summary>
    int MaxParallelDownloads => 8;
}

/// <summary>A cover image a source offers for a game.</summary>
/// <param name="Provider">The <see cref="ICoverProvider.Name"/> of the source.</param>
/// <param name="Title">The game's title at the source.</param>
/// <param name="AlternativeTitles">Other titles of the game there, e.g. regional ones; used for matching.</param>
/// <param name="Platforms">The platforms the source lists for the game, for display; empty when it doesn't know.</param>
/// <param name="PlatformAliases">Further names of those platforms (abbreviations and the like); used for matching.</param>
/// <param name="Year">The year of the first release, if known.</param>
/// <param name="ImageUrl">The full-size image.</param>
/// <param name="PreviewUrl">A small version of it for the selection page.</param>
public sealed record CoverCandidate(
    string Provider,
    string Title,
    IReadOnlyList<string> AlternativeTitles,
    IReadOnlyList<string> Platforms,
    IReadOnlyList<string> PlatformAliases,
    int? Year,
    Uri ImageUrl,
    Uri PreviewUrl)
{
    /// <summary>The region of the box art in ScreenScraper's short names (de, eu, us, jp, ...); null when unknown.</summary>
    public string? Region { get; init; }
}
