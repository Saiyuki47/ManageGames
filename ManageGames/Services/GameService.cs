using ManageGames.Data;
using ManageGames.Models;
using Microsoft.EntityFrameworkCore;

namespace ManageGames.Services
{
    /// <summary>
    /// Games belong to exactly one user. Every read and write here is scoped by the owner's id,
    /// so a user can never see or change another user's collection, even with a crafted game id.
    /// </summary>
    public class GameService
    {
        private readonly AppDbContext _db;

        public GameService(AppDbContext db)
        {
            _db = db;
        }

        public List<GameModel> GetGames(Guid userId, bool onWishList)
        {
            return _db.Games
                .Where(g => g.UserId == userId && g.IsOnWishList == onWishList)
                .Include(g => g.Console)
                .OrderBy(g => EF.Functions.Collate(g.GameName, "NOCASE"))
                .AsNoTracking()
                .ToList();
        }

        public GameModel? GetGame(int id, Guid userId)
        {
            return _db.Games
                .AsNoTracking()
                .FirstOrDefault(g => g.GameId == id && g.UserId == userId);
        }

        public void AddGame(Guid userId, string name, int copies, int? consoleId, bool onWishList)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            _db.Games.Add(new GameModel
            {
                UserId = userId,
                GameName = name.Trim(),
                Copies = Math.Max(1, copies),
                ConsoleId = ExistingConsoleId(consoleId),
                IsOnWishList = onWishList,
            });
            _db.SaveChanges();
        }

        /// <summary>Returns false when the game doesn't exist or belongs to someone else.</summary>
        public bool UpdateGame(int id, Guid userId, string name, int copies, int? consoleId, bool onWishList)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var game = _db.Games.FirstOrDefault(g => g.GameId == id && g.UserId == userId);
            if (game == null)
            {
                return false;
            }

            game.GameName = name.Trim();
            game.Copies = Math.Max(1, copies);
            game.ConsoleId = ExistingConsoleId(consoleId);
            game.IsOnWishList = onWishList;
            _db.SaveChanges();
            return true;
        }

        /// <summary>Returns the deleted game, or null when it doesn't exist or belongs to someone else.</summary>
        public GameModel? DeleteGame(int id, Guid userId)
        {
            var game = _db.Games.FirstOrDefault(g => g.GameId == id && g.UserId == userId);
            if (game == null)
            {
                return null;
            }

            _db.Games.Remove(game);
            _db.SaveChanges();
            return game;
        }

        // A console id that no longer exists (stale form, crafted input) degrades to "no console"
        // instead of failing on the foreign key.
        private int? ExistingConsoleId(int? id)
        {
            return id.HasValue && _db.Consoles.Any(c => c.ConsoleId == id.Value) ? id : null;
        }
    }
}
