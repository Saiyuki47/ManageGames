using ManageGames.Data;
using ManageGames.Models;
using Microsoft.EntityFrameworkCore;

namespace ManageGames.Services;

/// <summary>A row of the game list; <see cref="CoverVersion"/> is null while the game has no cover.</summary>
public sealed record GameListItem(int Id, string Name, string? ConsoleName, int Copies, Guid? CoverVersion);

/// <summary>
/// Games belong to exactly one user. Every read and write here is scoped by the owner's id,
/// so a user can never see or change another user's collection, even with a crafted game id.
/// </summary>
public class GameService(AppDbContext db)
{
    public async Task<IReadOnlyList<GameListItem>> GetGamesAsync(Guid userId, bool onWishList, CancellationToken cancellationToken = default)
    {
        return await db.Games
            .Where(g => g.UserId == userId && g.IsOnWishList == onWishList)
            .OrderBy(g => g.Name)
            // Only the cover's version, never its image data.
            .Select(g => new GameListItem(g.Id, g.Name, g.Console != null ? g.Console.Name : null, g.Copies, g.Cover != null ? (Guid?)g.Cover.Version : null))
            .ToListAsync(cancellationToken);
    }

    public Task<Game?> GetGameAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        return db.Games
            .Include(g => g.Console)
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId, cancellationToken);
    }

    /// <summary>Returns the new game's id.</summary>
    public async Task<int> AddGameAsync(Guid userId, string name, int copies, int? consoleId, bool onWishList, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var game = new Game
        {
            UserId = userId,
            Name = name.Trim(),
            Copies = Math.Max(1, copies),
            ConsoleId = await ExistingConsoleIdAsync(consoleId, cancellationToken),
            IsOnWishList = onWishList,
        };
        db.Games.Add(game);
        await db.SaveChangesAsync(cancellationToken);
        return game.Id;
    }

    /// <summary>Returns false when the game doesn't exist or belongs to someone else.</summary>
    public async Task<bool> UpdateGameAsync(int id, Guid userId, string name, int copies, int? consoleId, bool onWishList, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId, cancellationToken);
        if (game == null)
        {
            return false;
        }

        game.Name = name.Trim();
        game.Copies = Math.Max(1, copies);
        game.ConsoleId = await ExistingConsoleIdAsync(consoleId, cancellationToken);
        game.IsOnWishList = onWishList;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Returns the deleted game, or null when it doesn't exist or belongs to someone else.</summary>
    public async Task<Game?> DeleteGameAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId, cancellationToken);
        if (game == null)
        {
            return null;
        }

        db.Games.Remove(game);
        await db.SaveChangesAsync(cancellationToken);
        return game;
    }

    // A console id that no longer exists (stale form, crafted input) degrades to "no console"
    // instead of failing on the foreign key.
    private async Task<int?> ExistingConsoleIdAsync(int? id, CancellationToken cancellationToken)
    {
        return id.HasValue && await db.Consoles.AnyAsync(c => c.Id == id.Value, cancellationToken) ? id : null;
    }
}
