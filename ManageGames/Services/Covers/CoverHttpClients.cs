namespace ManageGames.Services.Covers;

/// <summary>Names of the HTTP clients of the cover search (see <see cref="CoverServiceCollectionExtensions"/>).</summary>
public static class CoverHttpClients
{
    /// <summary>For the sources' APIs.</summary>
    public const string Api = "covers-api";

    /// <summary>For downloading images; doesn't follow redirects, so only the checked address is ever fetched.</summary>
    public const string Images = "covers-images";

    public const string UserAgent = "ManageGames/1.0 (self-hosted game collection)";
}
