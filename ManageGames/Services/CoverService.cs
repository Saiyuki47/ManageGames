using ManageGames.Data;
using ManageGames.Models;
using ManageGames.Services.Covers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ManageGames.Services;

/// <summary>What the edit page shows about a game's cover.</summary>
public sealed record CoverInfo(Guid Version, string Source);

public enum CoverChoiceResult
{
    Saved,
    GameNotFound,
    /// <summary>The image isn't from an active cover source (stale or crafted form).</summary>
    NotAllowed,
    DownloadFailed,
}

/// <summary>
/// The covers of the users' games: stored in the database, found automatically or chosen and uploaded by the user.
/// Like <see cref="GameService"/>, every access is scoped by the owner's id.
/// </summary>
public class CoverService(
    AppDbContext db,
    CoverScraper scraper,
    IOptionsMonitor<CoverOptions> options,
    ILookupNormalizer normalizer,
    ILogger<CoverService> logger)
{
    public Task<GameCover?> GetCoverAsync(int gameId, Guid userId, CancellationToken cancellationToken = default)
    {
        return db.GameCovers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.GameId == gameId && c.Game!.UserId == userId, cancellationToken);
    }

    public Task<CoverInfo?> GetCoverInfoAsync(int gameId, Guid userId, CancellationToken cancellationToken = default)
    {
        return db.GameCovers
            .Where(c => c.GameId == gameId && c.Game!.UserId == userId)
            .Select(c => new CoverInfo(c.Version, c.Source))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Stores or replaces the cover. Returns false when the game doesn't exist or belongs to someone else.</summary>
    public async Task<bool> SetCoverAsync(int gameId, Guid userId, CoverImage image, string source, CancellationToken cancellationToken = default)
    {
        // One statement that inserts or replaces, so two quick clicks on two covers can't collide; the
        // SELECT only yields a row for the owner's own game.
        var rows = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "GameCovers" ("GameId", "Version", "ContentType", "Data", "Source", "CreatedAt", "UpdatedAt")
            SELECT "Id", {Guid.NewGuid()}, {image.ContentType}, {image.Data}, {source}, now(), now()
            FROM "Games"
            WHERE "Id" = {gameId} AND "UserId" = {userId}
            ON CONFLICT ("GameId") DO UPDATE SET
                "Version" = excluded."Version",
                "ContentType" = excluded."ContentType",
                "Data" = excluded."Data",
                "Source" = excluded."Source",
                "UpdatedAt" = excluded."UpdatedAt"
            """, cancellationToken);
        return rows > 0;
    }

    /// <summary>Returns false when the game doesn't exist or belongs to someone else.</summary>
    public async Task<bool> RemoveCoverAsync(int gameId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await OwnsGameAsync(gameId, userId, cancellationToken))
        {
            return false;
        }

        await db.GameCovers.Where(c => c.GameId == gameId).ExecuteDeleteAsync(cancellationToken);
        return true;
    }

    /// <summary>Downloads and stores a cover the user picked from the search results.</summary>
    public async Task<CoverChoiceResult> ChooseCoverAsync(int gameId, Guid userId, string? provider, string? imageUrl, CancellationToken cancellationToken = default)
    {
        var source = scraper.FindProvider(provider);
        if (source == null || !Uri.TryCreate(imageUrl, UriKind.Absolute, out var url) || !source.IsOwnImage(url))
        {
            return CoverChoiceResult.NotAllowed;
        }
        if (!await OwnsGameAsync(gameId, userId, cancellationToken))
        {
            return CoverChoiceResult.GameNotFound;
        }

        var image = await scraper.DownloadAsync(url, cancellationToken);
        if (image == null)
        {
            return CoverChoiceResult.DownloadFailed;
        }
        return await SetCoverAsync(gameId, userId, image, source.Name, cancellationToken) ? CoverChoiceResult.Saved : CoverChoiceResult.GameNotFound;
    }

    /// <summary>
    /// The automatic search for a game that has no cover yet, e.g. right after it was added. Returns whether
    /// it found and stored one; it never fails because a source is down or slow.
    /// </summary>
    public async Task<bool> FindMissingCoverAsync(int gameId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (scraper.GetActiveProviders().Count == 0)
        {
            return false;
        }

        var game = await db.Games
            .Where(g => g.Id == gameId && g.UserId == userId && g.Cover == null)
            .Select(g => new { g.Name, Console = g.Console != null ? g.Console.Name : null })
            .FirstOrDefaultAsync(cancellationToken);
        return game != null && await SearchAndStoreAsync(gameId, userId, game.Name, game.Console, cancellationToken);
    }

    /// <summary>
    /// For the <c>scrape-covers</c> command: the automatic search for every game without a cover, of one user or
    /// of all. Returns false when no source is configured or the user doesn't exist.
    /// </summary>
    public async Task<bool> FindMissingCoversAsync(string? username, TimeSpan pauseBetweenGames, CancellationToken cancellationToken = default)
    {
        if (scraper.GetActiveProviders().Count == 0)
        {
            logger.NoCoverProviders();
            return false;
        }

        Guid? userId = null;
        if (username != null)
        {
            var normalizedName = normalizer.NormalizeName(username);
            userId = await db.Users.Where(u => u.NormalizedUserName == normalizedName).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(cancellationToken);
            if (userId == null)
            {
                logger.UnknownUser(username);
                return false;
            }
        }

        var games = await db.Games
            .Where(g => g.Cover == null && (userId == null || g.UserId == userId))
            .OrderBy(g => g.Id)
            .Select(g => new { g.Id, g.UserId, g.Name, Console = g.Console != null ? g.Console.Name : null })
            .ToListAsync(cancellationToken);
        var found = 0;
        foreach (var game in games)
        {
            if (await SearchAndStoreAsync(game.Id, game.UserId, game.Name, game.Console, cancellationToken))
            {
                found++;
            }
            // Stays well below the sources' rate limits (IGDB: four requests per second).
            await Task.Delay(pauseBetweenGames, cancellationToken);
        }
        logger.MissingCoversSearched(games.Count, found);
        return true;
    }

    private async Task<bool> SearchAndStoreAsync(int gameId, Guid userId, string title, string? console, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.CurrentValue.AutoSearchTimeout);
        FoundCover? found;
        try
        {
            found = await scraper.FindCoverAsync(title, console, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.CoverSearchTimedOut(title);
            return false;
        }

        if (found == null)
        {
            logger.NoCoverFound(title);
            return false;
        }
        logger.CoverFound(title, found.Provider, found.MatchedTitle);
        return await SetCoverAsync(gameId, userId, found.Image, found.Provider, cancellationToken);
    }

    private Task<bool> OwnsGameAsync(int gameId, Guid userId, CancellationToken cancellationToken)
    {
        return db.Games.AnyAsync(g => g.Id == gameId && g.UserId == userId, cancellationToken);
    }
}
