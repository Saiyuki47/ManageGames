using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace ManageGames.Services.Covers;

/// <summary>
/// Covers from IGDB (https://www.igdb.com), the game database run by Twitch. It knows the platforms of each
/// game, so results can be matched against the game's console. Access goes through a Twitch application:
/// its client id and secret are exchanged for an access token, which is kept until it expires.
/// </summary>
public sealed partial class IgdbCoverProvider(IHttpClientFactory httpClientFactory, IOptionsMonitor<CoverOptions> options, TimeProvider time)
    : ICoverProvider, IDisposable
{
    public const string ProviderName = "IGDB";
    public const string ImageHost = "images.igdb.com";

    private const int MaxResults = 20;
    private static readonly Uri TokenUrl = new("https://id.twitch.tv/oauth2/token");
    private static readonly Uri GamesUrl = new("https://api.igdb.com/v4/games");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private AccessToken? _token;

    public string Name => ProviderName;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Settings.ClientId) && !string.IsNullOrWhiteSpace(Settings.ClientSecret);

    private IgdbOptions Settings => options.CurrentValue.Igdb;

    public bool IsOwnImage(Uri url)
    {
        return url.IsAbsoluteUri && url.Scheme == Uri.UriSchemeHttps && url.IsDefaultPort
            && string.Equals(url.Host, ImageHost, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<CoverCandidate>> SearchAsync(string title, CancellationToken cancellationToken)
    {
        var query = BuildQuery(title);
        var games = await QueryAsync(query, await GetTokenAsync(renew: false, cancellationToken), cancellationToken)
            // The token was revoked or expired early: get a new one and try once more.
            ?? await QueryAsync(query, await GetTokenAsync(renew: true, cancellationToken), cancellationToken)
            ?? throw new HttpRequestException("IGDB rejected a new access token.", null, HttpStatusCode.Unauthorized);

        return games
            .Where(g => !string.IsNullOrWhiteSpace(g.Title) && IsValidImageId(g.Cover?.ImageId))
            .SelectMany(ToCandidates)
            .ToList();
    }

    public void Dispose()
    {
        _tokenLock.Dispose();
    }

    /// <summary>
    /// The query in IGDB's Apicalypse language: games resembling the title that have a cover, with their
    /// platforms and other titles (often including the German one) for matching, and their regional covers.
    /// </summary>
    public static string BuildQuery(string title)
    {
        // The search term is a quoted string; quotes and backslashes in it would end it early.
        var term = new string(title.Where(c => c is not ('"' or '\\') && !char.IsControl(c)).ToArray()).Trim();
        return $"search \"{term}\"; " +
            "fields name,first_release_date,cover.image_id,platforms.name,platforms.abbreviation,platforms.alternative_name," +
            "alternative_names.name,game_localizations.name,game_localizations.region.identifier," +
            "game_localizations.cover.image_id; " +
            $"where cover != null; limit {MaxResults};";
    }

    // Null when IGDB doesn't accept the token.
    private async Task<IReadOnlyList<IgdbGame>?> QueryAsync(string query, string token, CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient(CoverHttpClients.Api);
        using var request = new HttpRequestMessage(HttpMethod.Post, GamesUrl);
        request.Content = new StringContent(query, Encoding.UTF8, "text/plain");
        request.Headers.Add("Client-ID", Settings.ClientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<IgdbGame>>(Json, cancellationToken) ?? [];
    }

    private async Task<string> GetTokenAsync(bool renew, CancellationToken cancellationToken)
    {
        var settings = Settings;
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            // A token belongs to the client id it was issued for; changed settings need a new one.
            if (!renew && _token is { } cached && cached.ClientId == settings.ClientId && time.GetUtcNow() < cached.ExpiresAt)
            {
                return cached.Value;
            }

            using var client = httpClientFactory.CreateClient(CoverHttpClients.Api);
            // In the body rather than the address, so the secret can't end up in request logs.
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["client_id"] = settings.ClientId ?? string.Empty,
                ["client_secret"] = settings.ClientSecret ?? string.Empty,
                ["grant_type"] = "client_credentials",
            });
            using var response = await client.PostAsync(TokenUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();
            var token = await response.Content.ReadFromJsonAsync<TwitchToken>(Json, cancellationToken);
            if (string.IsNullOrEmpty(token?.AccessToken))
            {
                throw new HttpRequestException("Twitch returned no access token.");
            }

            // Renew a minute early, so a request never starts with a token that is about to expire.
            _token = new AccessToken(token.AccessToken, settings.ClientId, time.GetUtcNow().AddSeconds(token.ExpiresIn - 60));
            return _token.Value;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    // The game's main cover (usually the North American one) and its regional covers (Europe, Japan, Korea).
    private IEnumerable<CoverCandidate> ToCandidates(IgdbGame game)
    {
        var platforms = game.Platforms ?? [];
        var localizations = game.GameLocalizations ?? [];
        var main = new CoverCandidate(
            Name,
            game.Title!,
            (game.AlternativeNames ?? []).Select(n => n.Text).Concat(localizations.Select(l => l.Text)).OfType<string>().ToList(),
            platforms.Select(p => p.PlatformName).OfType<string>().ToList(),
            platforms.SelectMany(p => new[] { p.Abbreviation, p.AlternativeName }).OfType<string>().ToList(),
            game.FirstReleaseDate is { } released ? DateTimeOffset.FromUnixTimeSeconds(released).Year : null,
            ImageUrl("t_cover_big_2x", game.Cover!.ImageId!),
            ImageUrl("t_cover_big", game.Cover.ImageId!));

        yield return main;
        foreach (var localization in localizations.Where(l => IsValidImageId(l.Cover?.ImageId) && l.Cover!.ImageId != game.Cover.ImageId))
        {
            yield return main with
            {
                ImageUrl = ImageUrl("t_cover_big_2x", localization.Cover!.ImageId!),
                PreviewUrl = ImageUrl("t_cover_big", localization.Cover.ImageId!),
                Region = RegionName(localization.Region?.Identifier),
            };
        }
    }

    // IGDB's region identifiers ("EU", "ja-JP", "ko-KR") as ScreenScraper's short names, like CoverOptions.Regions.
    private static string? RegionName(string? identifier)
    {
        return identifier switch
        {
            null or "" => null,
            "ja-JP" => "jp",
            "ko-KR" => "kr",
            _ => identifier.ToLowerInvariant(),
        };
    }

    private static bool IsValidImageId(string? imageId)
    {
        return imageId != null && ValidImageId().IsMatch(imageId);
    }

    // https://api-docs.igdb.com/#images: cover_big is 264 × 374, the _2x variant twice that.
    private static Uri ImageUrl(string size, string imageId)
    {
        return new Uri($"https://{ImageHost}/igdb/image/upload/{size}/{imageId}.jpg");
    }

    // Image ids are short lower-case tokens; anything else is not put into an address.
    [GeneratedRegex("^[a-z0-9]{1,64}$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex ValidImageId();

    private sealed record AccessToken(string Value, string? ClientId, DateTimeOffset ExpiresAt);

    private sealed record TwitchToken(string? AccessToken, int ExpiresIn);

    // "name" is renamed in the records below, so it doesn't hide this class's Name.
    private sealed record IgdbGame(
        [property: JsonPropertyName("name")] string? Title,
        long? FirstReleaseDate,
        IgdbImage? Cover,
        IReadOnlyList<IgdbPlatform>? Platforms,
        IReadOnlyList<IgdbName>? AlternativeNames,
        IReadOnlyList<IgdbLocalization>? GameLocalizations);

    private sealed record IgdbImage(string? ImageId);

    private sealed record IgdbPlatform([property: JsonPropertyName("name")] string? PlatformName, string? Abbreviation, string? AlternativeName);

    private sealed record IgdbName([property: JsonPropertyName("name")] string? Text);

    private sealed record IgdbLocalization([property: JsonPropertyName("name")] string? Text, IgdbRegion? Region, IgdbImage? Cover);

    private sealed record IgdbRegion(string? Identifier);
}
