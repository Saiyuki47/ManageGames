using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ManageGames.Services.Covers;

/// <summary>
/// Covers from SteamGridDB (https://www.steamgriddb.com), a community collection of cover art in the
/// "grid" format (portrait, 600 × 900). It doesn't know which platforms a game came out on, so its results
/// are matched by title only.
/// </summary>
public sealed class SteamGridDbCoverProvider(IHttpClientFactory httpClientFactory, IOptionsMonitor<CoverOptions> options) : ICoverProvider
{
    public const string ProviderName = "SteamGridDB";

    private const string ApiBase = "https://www.steamgriddb.com/api/v2/";
    // Grids of the best-fitting games only, a few each; the rest are rarely what is wanted.
    private const int MaxGames = 3;
    private const int GridsPerGame = 8;
    // Portrait formats like a box, still images only, nothing marked as NSFW, humor or epilepsy risk.
    private const string GridFilter = "dimensions=600x900,342x482,660x930&types=static&nsfw=false&humor=false&epilepsy=false";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public string Name => ProviderName;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    private string? ApiKey => options.CurrentValue.SteamGridDb.ApiKey;

    /// <summary>The images are served by the steamgriddb.com CDN hosts (cdn2.steamgriddb.com, ...).</summary>
    public bool IsOwnImage(Uri url)
    {
        return url.IsAbsoluteUri && url.Scheme == Uri.UriSchemeHttps && url.IsDefaultPort
            && url.Host.EndsWith(".steamgriddb.com", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<CoverCandidate>> SearchAsync(string title, CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient(CoverHttpClients.Api);
        // A slash would be read as a path separator, even when escaped.
        var term = Uri.EscapeDataString(title.Replace('/', ' ').Trim());
        var games = await GetAsync<SgdbGame>(client, $"search/autocomplete/{term}", cancellationToken);

        var grids = await Task.WhenAll(games.Take(MaxGames).Select(async game =>
        {
            var path = string.Create(CultureInfo.InvariantCulture, $"grids/game/{game.Id}?{GridFilter}&limit={GridsPerGame}");
            return (Game: game, Grids: await GetAsync<SgdbGrid>(client, path, cancellationToken));
        }));

        return grids
            .SelectMany(result => result.Grids.Select(grid => ToCandidate(result.Game, grid)))
            .OfType<CoverCandidate>()
            .ToList();
    }

    private async Task<IReadOnlyList<T>> GetAsync<T>(HttpClient client, string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(ApiBase + path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        using var response = await client.SendAsync(request, cancellationToken);
        // "No game by that name / no grids for that game".
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SgdbResponse<T>>(Json, cancellationToken);
        return body?.Data ?? [];
    }

    private CoverCandidate? ToCandidate(SgdbGame game, SgdbGrid grid)
    {
        if (string.IsNullOrWhiteSpace(game.Title)
            || !Uri.TryCreate(grid.Url, UriKind.Absolute, out var url) || !IsOwnImage(url)
            || !Uri.TryCreate(grid.Thumb, UriKind.Absolute, out var thumb) || !IsOwnImage(thumb))
        {
            return null;
        }

        return new CoverCandidate(
            Name,
            game.Title,
            [],
            [],
            [],
            game.ReleaseDate is { } released ? DateTimeOffset.FromUnixTimeSeconds(released).Year : null,
            url,
            thumb);
    }

    private sealed record SgdbResponse<T>(bool Success, IReadOnlyList<T>? Data);

    // "name" is renamed, so it doesn't hide this class's Name.
    private sealed record SgdbGame(int Id, [property: JsonPropertyName("name")] string? Title, long? ReleaseDate);

    private sealed record SgdbGrid(string? Url, string? Thumb);
}
