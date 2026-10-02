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
    /// API keys are skipped, so it is enough to configure the ones you have.
    /// </summary>
    public string Providers { get; set; } = $"{IgdbCoverProvider.ProviderName},{SteamGridDbCoverProvider.ProviderName}";

    public IgdbOptions Igdb { get; set; } = new();

    public SteamGridDbOptions SteamGridDb { get; set; } = new();

    /// <summary>How long adding a game may wait for its cover; after that the game is saved without one.</summary>
    public TimeSpan AutoSearchTimeout { get; set; } = TimeSpan.FromSeconds(15);
}

/// <summary>A Twitch application (https://dev.twitch.tv/console), which IGDB uses for its API access.</summary>
public class IgdbOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}

/// <summary>An API key from https://www.steamgriddb.com/profile/preferences/api.</summary>
public class SteamGridDbOptions
{
    public string? ApiKey { get; set; }
}
