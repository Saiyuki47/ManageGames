using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ManageGames.Services.Covers;

/// <summary>A search result with how well it fits the game.</summary>
/// <param name="Candidate">The result.</param>
/// <param name="Score">Title similarity from 0 to 1 (see <see cref="TitleMatcher"/>).</param>
/// <param name="PlatformMatch">Whether it is for the game's console; null when unknown.</param>
/// <param name="MainTitleScore">
/// Similarity to the result's main title only. Breaks ties: for "Wii Fit", the game "Wii Fit" beats
/// "Wii Fit Plus", which is also known as "Wii Fit +".
/// </param>
public sealed record RankedCover(CoverCandidate Candidate, double Score, bool? PlatformMatch, double MainTitleScore)
{
    /// <summary>Good enough to be taken without asking: a similar title and not for another console.</summary>
    public bool IsConfident => Score >= TitleMatcher.AcceptThreshold && PlatformMatch != false;
}

/// <summary>The results of all sources for the selection page.</summary>
/// <param name="Covers">All results, the best-fitting first.</param>
/// <param name="FailedProviders">Sources that couldn't be reached or answered with an error.</param>
public sealed record CoverSearchResult(IReadOnlyList<RankedCover> Covers, IReadOnlyList<string> FailedProviders);

/// <summary>A cover the automatic search found and downloaded.</summary>
public sealed record FoundCover(CoverImage Image, string Provider, string MatchedTitle);

/// <summary>
/// Searches the configured cover sources (<see cref="ICoverProvider"/>) and downloads their images. A source that
/// fails is logged and skipped, so one being down never breaks adding a game or the others' results.
/// </summary>
public sealed class CoverScraper(
    IEnumerable<ICoverProvider> providers,
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<CoverOptions> options,
    ILogger<CoverScraper> logger)
{
    // How many confident results the automatic search tries to download before it moves on to the next source.
    private const int DownloadAttempts = 3;

    /// <summary>The sources with API keys, in the configured order.</summary>
    public IReadOnlyList<ICoverProvider> GetActiveProviders()
    {
        return options.CurrentValue.Providers
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => providers.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            .OfType<ICoverProvider>()
            .Where(p => p.IsConfigured)
            .Distinct()
            .ToList();
    }

    /// <summary>The active source with that name, or null.</summary>
    public ICoverProvider? FindProvider(string? name)
    {
        return GetActiveProviders().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether an active source serves this image (the preview proxy only fetches those).</summary>
    public bool IsKnownImage(Uri url)
    {
        return GetActiveProviders().Any(p => p.IsOwnImage(url));
    }

    /// <summary>Asks all active sources at once, for the user to pick a cover from all their results.</summary>
    public async Task<CoverSearchResult> SearchAsync(string title, string? console, CancellationToken cancellationToken)
    {
        var active = GetActiveProviders();
        var results = await Task.WhenAll(active.Select(p => SearchProviderAsync(p, title, cancellationToken)));

        var covers = results
            .SelectMany((candidates, source) => (candidates ?? []).Select((candidate, position) => (Cover: Rank(candidate, title, console), source, position)))
            // Fitting titles first; the console moves results up or down a bit. Ties keep the configured
            // source order and each source's own order.
            .OrderByDescending(r => r.Cover.Score + r.Cover.PlatformMatch switch { true => 0.2, false => -0.2, null => 0 })
            .ThenByDescending(r => r.Cover.MainTitleScore)
            .ThenBy(r => r.source)
            .ThenBy(r => r.position)
            .Select(r => r.Cover)
            .ToList();
        var failed = active.Where((_, i) => results[i] == null).Select(p => p.Name).ToList();
        return new CoverSearchResult(covers, failed);
    }

    /// <summary>
    /// The automatic search: tries the sources one after the other and takes the first cover it is confident
    /// about. Null when none fits well enough; a wrong cover would be worse than none.
    /// </summary>
    public async Task<FoundCover?> FindCoverAsync(string title, string? console, CancellationToken cancellationToken)
    {
        foreach (var provider in GetActiveProviders())
        {
            var candidates = await SearchProviderAsync(provider, title, cancellationToken);
            var confident = (candidates ?? [])
                .Select((candidate, position) => (Cover: Rank(candidate, title, console), position))
                .Where(r => r.Cover.IsConfident)
                .OrderByDescending(r => r.Cover.PlatformMatch == true)
                .ThenByDescending(r => r.Cover.Score)
                .ThenByDescending(r => r.Cover.MainTitleScore)
                .ThenBy(r => r.position)
                .Take(DownloadAttempts);
            foreach (var (cover, _) in confident)
            {
                if (await DownloadAsync(cover.Candidate.ImageUrl, cancellationToken) is { } image)
                {
                    return new FoundCover(image, provider.Name, cover.Candidate.Title);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Downloads an image, or returns null (and logs why) when that fails or the file is no supported image.
    /// Callers check the address first (<see cref="ICoverProvider.IsOwnImage"/>); redirects are not followed.
    /// </summary>
    public async Task<CoverImage?> DownloadAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using var client = httpClientFactory.CreateClient(CoverHttpClients.Images);
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.CoverDownloadFailed(url, $"HTTP {(int)response.StatusCode}");
                return null;
            }
            if (response.Content.Headers.ContentLength > CoverImage.MaxBytes)
            {
                logger.CoverDownloadFailed(url, "the file is too large");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var data = await ReadAtMostAsync(stream, CoverImage.MaxBytes, cancellationToken);
            var image = data == null ? null : CoverImage.FromBytes(data);
            if (image == null)
            {
                logger.CoverDownloadFailed(url, data == null ? "the file is too large" : "the file is no supported image");
            }
            return image;
        }
        catch (HttpRequestException exception)
        {
            logger.CoverDownloadFailed(url, exception.Message);
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.CoverDownloadFailed(url, "the server took too long to answer");
            return null;
        }
    }

    private async Task<IReadOnlyList<CoverCandidate>?> SearchProviderAsync(ICoverProvider provider, string title, CancellationToken cancellationToken)
    {
        try
        {
            return await provider.SearchAsync(title, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.CoverSearchFailed(provider.Name, exception);
            return null;
        }
    }

    private static RankedCover Rank(CoverCandidate candidate, string title, string? console)
    {
        return new RankedCover(
            candidate,
            TitleMatcher.Similarity(title, candidate),
            TitleMatcher.PlatformMatches(console, candidate),
            TitleMatcher.Similarity(title, candidate.Title));
    }

    // Stops reading as soon as the limit is exceeded, whatever the server claimed beforehand.
    private static async Task<byte[]?> ReadAtMostAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        return buffer.ToArray();
    }
}
