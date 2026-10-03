using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ManageGames.Services.Covers;

/// <summary>
/// Box art from ScreenScraper (https://www.screenscraper.fr), the database behind many emulation frontends. It
/// has scans of the boxes of each region (de, eu, us, jp, ...) and the regional titles, so German titles and
/// German covers are found. Every request carries developer credentials, which ScreenScraper hands out on
/// request; they are added to image addresses only when downloading (<see cref="GetDownloadUrl"/>).
/// </summary>
public sealed class ScreenScraperCoverProvider(IHttpClientFactory httpClientFactory, IOptionsMonitor<CoverOptions> options)
    : ICoverProvider, IDisposable
{
    public const string ProviderName = "ScreenScraper";
    public const string ApiHost = "api.screenscraper.fr";

    private const string ApiBase = $"https://{ApiHost}/api2/";
    private const string MediaPath = "/api2/mediaJeu.php";
    // The best-fitting games of the search only, with the box fronts of a few regions each.
    private const int MaxGames = 5;
    private const int MaxRegionsPerGame = 4;
    // Regions whose title is shown when the preferred ones have none.
    private static readonly string[] FallbackTitleRegions = ["wor", "eu", "us", "ss"];

    private readonly SemaphoreSlim _systemsLock = new(1, 1);
    private IReadOnlyDictionary<string, IReadOnlyList<string>>? _systemNames;

    public string Name => ProviderName;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Settings.DevId) && !string.IsNullOrWhiteSpace(Settings.DevPassword);

    // Without a membership, ScreenScraper allows one request at a time.
    public int MaxParallelDownloads => 1;

    private ScreenScraperOptions Settings => options.CurrentValue.ScreenScraper;

    /// <summary>Only the image download of the API, as built by this class.</summary>
    public bool IsOwnImage(Uri url)
    {
        return url.IsAbsoluteUri && url.Scheme == Uri.UriSchemeHttps && url.IsDefaultPort
            && string.Equals(url.Host, ApiHost, StringComparison.OrdinalIgnoreCase)
            && string.Equals(url.AbsolutePath, MediaPath, StringComparison.Ordinal);
    }

    public Uri GetDownloadUrl(Uri imageUrl)
    {
        return new Uri($"{imageUrl.AbsoluteUri}&{CredentialsQuery()}");
    }

    public async Task<IReadOnlyList<CoverCandidate>> SearchAsync(string title, CancellationToken cancellationToken)
    {
        var systems = await GetSystemNamesAsync(cancellationToken);
        using var client = httpClientFactory.CreateClient(CoverHttpClients.Api);
        using var search = await GetJsonAsync(client, $"jeuRecherche.php?{CredentialsQuery()}&output=json&recherche={Uri.EscapeDataString(title)}", cancellationToken);

        var preferredRegions = CoverRegions.Parse(options.CurrentValue.Regions);
        var candidates = new List<CoverCandidate>();
        if (!search.RootElement.TryGetProperty("response", out var response) || !response.TryGetProperty("jeux", out var games)
            || games.ValueKind != JsonValueKind.Array)
        {
            return candidates;
        }

        // Without a match, ScreenScraper answers with one empty game.
        foreach (var game in games.EnumerateArray().Where(g => Text(g, "id") != null).Take(MaxGames))
        {
            candidates.AddRange(ToCandidates(game, systems, preferredRegions));
        }
        return candidates;
    }

    public void Dispose()
    {
        _systemsLock.Dispose();
    }

    private IEnumerable<CoverCandidate> ToCandidates(JsonElement game, IReadOnlyDictionary<string, IReadOnlyList<string>> systems, IReadOnlyList<string> preferredRegions)
    {
        var gameId = Text(game, "id")!;
        var system = game.TryGetProperty("systeme", out var s) ? s : default;
        var systemId = system.ValueKind == JsonValueKind.Object ? Text(system, "id") : null;
        var systemName = system.ValueKind == JsonValueKind.Object ? Text(system, "text") : null;
        var names = Entries(game, "noms");
        var title = preferredRegions.Concat(FallbackTitleRegions)
            .Select(region => names.FirstOrDefault(n => n.Region == region).Text)
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))
            ?? names.Select(n => n.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        if (title == null || systemId == null || !IsNumber(gameId) || !IsNumber(systemId))
        {
            yield break;
        }

        var boxRegions = Entries(game, "medias", "type", "box-2D")
            .Select(m => m.Region)
            .Where(r => r.Length > 0 && r.All(char.IsAsciiLetterLower))
            .Distinct(StringComparer.Ordinal)
            // The preferred regions first, then the others in ScreenScraper's order.
            .OrderBy(r => CoverRegions.Rank(preferredRegions, r))
            .Take(MaxRegionsPerGame);
        var years = Entries(game, "dates")
            .Select(d => d.Text.Length >= 4 && int.TryParse(d.Text.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year) ? year : (int?)null)
            .OfType<int>()
            .ToList();

        foreach (var region in boxRegions)
        {
            var media = $"box-2D({region})";
            yield return new CoverCandidate(
                Name,
                title,
                names.Select(n => n.Text).Where(t => t.Length > 0 && t != title).Distinct(StringComparer.Ordinal).ToList(),
                systemName == null ? [] : [systemName],
                systems.GetValueOrDefault(systemId, []),
                years.Count == 0 ? null : years.Min(),
                MediaUrl(systemId, gameId, media, 640, 900),
                MediaUrl(systemId, gameId, media, 264, 374))
            {
                Region = region,
            };
        }
    }

    // The image download without credentials; ScreenScraper scales the box down to the given size. The ids are
    // checked to be numbers and the region to be lower-case letters, so nothing else ends up in the address.
    private static Uri MediaUrl(string systemId, string gameId, string media, int maxWidth, int maxHeight)
    {
        return new Uri(string.Create(CultureInfo.InvariantCulture,
            $"https://{ApiHost}{MediaPath}?systemeid={systemId}&jeuid={gameId}&media={media}&maxwidth={maxWidth}&maxheight={maxHeight}&outputformat=jpg"));
    }

    /// <summary>
    /// The names of every system (console) by id, e.g. "Nintendo DS", "NDS", for matching the game's console.
    /// Loaded once; empty while ScreenScraper can't be asked, so the console is unknown then.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetSystemNamesAsync(CancellationToken cancellationToken)
    {
        if (_systemNames != null)
        {
            return _systemNames;
        }

        await _systemsLock.WaitAsync(cancellationToken);
        try
        {
            if (_systemNames == null)
            {
                using var client = httpClientFactory.CreateClient(CoverHttpClients.Api);
                using var list = await GetJsonAsync(client, $"systemesListe.php?{CredentialsQuery()}&output=json", cancellationToken);
                _systemNames = ParseSystemNames(list.RootElement);
            }
            return _systemNames;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        }
        finally
        {
            _systemsLock.Release();
        }
    }

    // Every name in a system's "noms" object (nom_eu, nom_us, ...); "noms_commun" lists several, comma-separated.
    private static Dictionary<string, IReadOnlyList<string>> ParseSystemNames(JsonElement root)
    {
        var names = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (!root.TryGetProperty("response", out var response) || !response.TryGetProperty("systemes", out var systems)
            || systems.ValueKind != JsonValueKind.Array)
        {
            return names;
        }

        foreach (var system in systems.EnumerateArray())
        {
            if (Text(system, "id") is { } id && system.TryGetProperty("noms", out var noms) && noms.ValueKind == JsonValueKind.Object)
            {
                names[id] = noms.EnumerateObject()
                    .Where(n => n.Value.ValueKind == JsonValueKind.String)
                    .SelectMany(n => n.Value.GetString()!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
        return names;
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string pathAndQuery, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(new Uri(ApiBase + pathAndQuery), cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        // Errors (wrong developer credentials, quota used up, API closed) come as a line of plain text, often
        // with status 200; pass ScreenScraper's message on to the log instead of a JSON parse error.
        if (!body.TrimStart().StartsWith('{'))
        {
            var message = body.Trim();
            throw new HttpRequestException($"ScreenScraper refused the request: {(message.Length > 200 ? message[..200] : message)}");
        }
        return JsonDocument.Parse(body);
    }

    private string CredentialsQuery()
    {
        var settings = Settings;
        var query = $"devid={Uri.EscapeDataString(settings.DevId ?? string.Empty)}" +
            $"&devpassword={Uri.EscapeDataString(settings.DevPassword ?? string.Empty)}" +
            $"&softname={Uri.EscapeDataString(settings.SoftName)}";
        if (!string.IsNullOrWhiteSpace(settings.Username) && !string.IsNullOrEmpty(settings.Password))
        {
            query += $"&ssid={Uri.EscapeDataString(settings.Username)}&sspassword={Uri.EscapeDataString(settings.Password)}";
        }
        return query;
    }

    // ScreenScraper's region-tagged lists: [{ "region": "de", "text": "..." }] or, for "medias", with a "type".
    private static List<(string Region, string Text)> Entries(JsonElement game, string property, string? type = null, string? typeValue = null)
    {
        if (!game.TryGetProperty(property, out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return list.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.Object && (type == null || string.Equals(Text(e, type), typeValue, StringComparison.Ordinal)))
            .Select(e => ((Text(e, "region") ?? string.Empty).ToLowerInvariant(), Text(e, "text") ?? string.Empty))
            .ToList();
    }

    // ScreenScraper writes numbers as strings; accept both.
    private static string? Text(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static bool IsNumber(string value)
    {
        return value.Length is > 0 and <= 10 && value.All(char.IsAsciiDigit);
    }
}
