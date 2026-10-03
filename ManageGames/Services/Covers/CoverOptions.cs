namespace ManageGames.Services.Covers;

/// <summary>
/// Settings of the cover search (configuration section "Covers"). The API keys are secrets: keep them in the
/// user secrets or in environment variables, never in appsettings.json.
/// </summary>
public class CoverOptions
{
    public const string SectionName = "Covers";

    /// <summary>
    /// The cover sources in the order they are tried when a game is added, comma-separated. Sources without
    /// API keys are skipped, so it is enough to configure the ones you have. ScreenScraper comes first because
    /// it has the boxes of each region; IGDB mostly has North American ones.
    /// </summary>
    public string Providers { get; set; } =
        $"{ScreenScraperCoverProvider.ProviderName},{IgdbCoverProvider.ProviderName},{SteamGridDbCoverProvider.ProviderName}";

    /// <summary>
    /// The regions whose box art is preferred, best first, comma-separated (ScreenScraper's short names: de,
    /// eu, fr, uk, us, jp, wor, ...). Among equally fitting covers of a source, these win.
    /// </summary>
    public string Regions { get; set; } = "de,eu";

    public IgdbOptions Igdb { get; set; } = new();

    public SteamGridDbOptions SteamGridDb { get; set; } = new();

    public ScreenScraperOptions ScreenScraper { get; set; } = new();

    /// <summary>How long adding a game may wait for its cover; after that the game is saved without one.</summary>
    public TimeSpan AutoSearchTimeout { get; set; } = TimeSpan.FromSeconds(15);
}

/// <summary>A Twitch application (https://dev.twitch.tv/console), which IGDB uses for its API access.</summary>
public class IgdbOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}

/// <summary>
/// ScreenScraper (https://www.screenscraper.fr) needs developer credentials, which its team hands out on
/// request in the forum; a user account (Username, Password) is optional and raises the request limits.
/// </summary>
public class ScreenScraperOptions
{
    public string? DevId { get; set; }
    public string? DevPassword { get; set; }

    /// <summary>The name ScreenScraper sees as the calling software.</summary>
    public string SoftName { get; set; } = "ManageGames";

    public string? Username { get; set; }
    public string? Password { get; set; }
}

/// <summary>An API key from https://www.steamgriddb.com/profile/preferences/api.</summary>
public class SteamGridDbOptions
{
    public string? ApiKey { get; set; }
}
