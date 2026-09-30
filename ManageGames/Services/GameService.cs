using ManageGames.Data;
using ManageGames.Helpers;
using ManageGames.Models;
using Microsoft.EntityFrameworkCore;

namespace ManageGames.Services;

/// <summary>
/// Games belong to exactly one user. Every read and write here is scoped by the owner's id,
/// so a user can never see or change another user's collection, even with a crafted game id.
/// </summary>
public class GameService(AppDbContext db)
{
    public List<Game> GetGames(Guid userId, bool onWishList)
    {
        return db.Games
            .Where(g => g.UserId == userId && g.IsOnWishList == onWishList)
            .Include(g => g.Console)
            .AsNoTracking()
            .AsEnumerable()
            .OrderBy(g => g.Name, NameOrder.Comparer)
            .ToList();
    }

    public Game? GetGame(int id, Guid userId)
    {
        return db.Games
            .AsNoTracking()
            .FirstOrDefault(g => g.Id == id && g.UserId == userId);
    }

    public void AddGame(Guid userId, string name, int copies, int? consoleId, bool onWishList)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        db.Games.Add(new Game
        {
            UserId = userId,
            Name = name.Trim(),
            Copies = Math.Max(1, copies),
            ConsoleId = ExistingConsoleId(consoleId),
            IsOnWishList = onWishList,
        });
        db.SaveChanges();
    }

    /// <summary>Returns false when the game doesn't exist or belongs to someone else.</summary>
    public bool UpdateGame(int id, Guid userId, string name, int copies, int? consoleId, bool onWishList)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var game = db.Games.FirstOrDefault(g => g.Id == id && g.UserId == userId);
        if (game == null)
        {
            return false;
        }

        game.Name = name.Trim();
        game.Copies = Math.Max(1, copies);
        game.ConsoleId = ExistingConsoleId(consoleId);
        game.IsOnWishList = onWishList;
        db.SaveChanges();
        return true;
    }

    /// <summary>Returns the deleted game, or null when it doesn't exist or belongs to someone else.</summary>
    public Game? DeleteGame(int id, Guid userId)
    {
        var game = db.Games.FirstOrDefault(g => g.Id == id && g.UserId == userId);
        if (game == null)
        {
            return null;
        }

        db.Games.Remove(game);
        db.SaveChanges();
        return game;
    }

    // A console id that no longer exists (stale form, crafted input) degrades to "no console"
    // instead of failing on the foreign key.
    private int? ExistingConsoleId(int? id)
    {
        return id.HasValue && db.Consoles.Any(c => c.Id == id.Value) ? id : null;
    }
}
