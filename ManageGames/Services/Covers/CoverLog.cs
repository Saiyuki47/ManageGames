namespace ManageGames.Services.Covers;

/// <summary>Log messages of the cover search, as source-generated log messages.</summary>
public static partial class CoverLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Warning, Message = "The cover search at {Provider} failed.")]
    public static partial void CoverSearchFailed(this ILogger logger, string provider, Exception exception);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "Could not download the cover {Url}: {Reason}.")]
    public static partial void CoverDownloadFailed(this ILogger logger, Uri url, string reason);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Found a cover for '{Title}' at {Provider} (as '{MatchedTitle}').")]
    public static partial void CoverFound(this ILogger logger, string title, string provider, string matchedTitle);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Information, Message = "Found no fitting cover for '{Title}'.")]
    public static partial void NoCoverFound(this ILogger logger, string title);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "The cover search for '{Title}' took too long and was stopped.")]
    public static partial void CoverSearchTimedOut(this ILogger logger, string title);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Error, Message = "No cover source is configured. Add the API keys of IGDB or SteamGridDB (see the README).")]
    public static partial void NoCoverProviders(this ILogger logger);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Error, Message = "There is no user '{Username}'.")]
    public static partial void UnknownUser(this ILogger logger, string username);

    [LoggerMessage(EventId = 2007, Level = LogLevel.Information, Message = "Searched covers for {Searched} games without one and found {Found}.")]
    public static partial void MissingCoversSearched(this ILogger logger, int searched, int found);
}
