using ManageGames.Data;
using ManageGames.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ManageGames.Service
{
    public class DataBase_Service
    {
        private readonly AppDbContext _db;
        private readonly PasswordHasher<UserModel> _hasher = new();

        public DataBase_Service(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Seeds the default admin user on an empty database. Called once at startup.
        /// Consoles and companies are optional now (nullable FKs), so nothing is pre-seeded there.
        /// </summary>
        public void EnsureSeeded()
        {
            if (!_db.Users.Any())
            {
                CreateUser("user", "password1", true);
            }
        }

        #region WriteToDB

        public void DeleteGame(int id, Guid userId)
        {
            // Scoped by owner so a user can only delete their own games.
            var game = _db.Games.FirstOrDefault(g => g.GameId == id && g.UserId == userId);
            if (game == null) return;

            _db.Games.Remove(game);
            _db.SaveChanges();
        }
        public void AddGame(string gameName, int game_amount, int? console, string wishlist, Guid userId)
        {
            if (string.IsNullOrWhiteSpace(gameName)) return;

            _db.Games.Add(new GameModel
            {
                // Ignore a console id that no longer exists (stale form / crafted input) instead of crashing on the FK.
                ConsoleId = ExistingConsoleId(console),
                GameName = gameName,
                Copies = Math.Max(1, game_amount),
                IsOnWishList = wishlist != null,
                UserId = userId
            });
            _db.SaveChanges();
        }
        public void AddCategory(string categoryName, int? company)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) return;

            _db.Consoles.Add(new ConsoleModel { ConsoleName = categoryName, CompanyId = ExistingCompanyId(company) });
            _db.SaveChanges();
        }
        public void UpdateGame(string gameName, int game_amount, int? console, string wishlist, int id, Guid userId)
        {
            // Scoped by owner so a user can only edit their own games.
            var game = _db.Games.FirstOrDefault(g => g.GameId == id && g.UserId == userId);
            if (game == null) return;
            if (string.IsNullOrWhiteSpace(gameName)) return;

            game.GameName = gameName;
            game.Copies = Math.Max(1, game_amount);
            game.ConsoleId = ExistingConsoleId(console);
            game.IsOnWishList = wishlist != null;
            _db.SaveChanges();
        }
        public void UpdateCategory(int category_id, string categoryName, int? company)
        {
            var console = _db.Consoles.Find(category_id);
            if (console == null) return;
            if (string.IsNullOrWhiteSpace(categoryName)) return;

            console.ConsoleName = categoryName;
            console.CompanyId = ExistingCompanyId(company);
            _db.SaveChanges();
        }
        public void DeleteCategory(int id)
        {
            var console = _db.Consoles.Find(id);
            if (console == null) return;

            // Games referencing this console have their ConsoleId set to NULL automatically
            // by the database (ON DELETE SET NULL) — no manual reassignment needed.
            _db.Consoles.Remove(console);
            _db.SaveChanges();
        }
        public void AddCompany(string companyName)
        {
            if (string.IsNullOrWhiteSpace(companyName)) return;

            _db.Companies.Add(new CompanyModel { CompanyName = companyName });
            _db.SaveChanges();
        }
        public void UpdateCompany(int company_id, string companyName)
        {
            var company = _db.Companies.Find(company_id);
            if (company == null) return;
            if (string.IsNullOrWhiteSpace(companyName)) return;

            company.CompanyName = companyName;
            _db.SaveChanges();
        }
        public void DeleteCompany(int id)
        {
            var company = _db.Companies.Find(id);
            if (company == null) return;

            // Consoles referencing this company have their CompanyId set to NULL automatically
            // by the database (ON DELETE SET NULL) — no manual reassignment needed.
            _db.Companies.Remove(company);
            _db.SaveChanges();
        }
        /// <summary>
        /// Creates a user with a salted password hash. Returns false without writing when the
        /// username or password is blank, or when the normalized username is already taken —
        /// the UNIQUE index on NormalizedUsername would otherwise throw on SaveChanges.
        /// </summary>
        public bool CreateUser(string username, string password, bool isAdmin)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            var normalized = Normalize(username);
            if (_db.Users.Any(u => u.NormalizedUsername == normalized))
            {
                return false;
            }

            var user = new UserModel
            {
                UserID = Guid.NewGuid(),
                ProfilePicturesID = Guid.Empty,
                Username = username,
                NormalizedUsername = normalized,
                IsAdmin = isAdmin,
                CookieID = "StartCook"
            };
            // Store a salted hash, never the plaintext password.
            user.Password = _hasher.HashPassword(user, password);

            _db.Users.Add(user);
            _db.SaveChanges();
            return true;
        }
        public void ChangeCookieId(Guid userID, string cookieID)
        {
            var user = _db.Users.Find(userID);
            if (user == null) return;

            user.CookieID = cookieID;
            _db.SaveChanges();
        }

        #endregion

        #region ReadFromDB

        public GameModel GetSingleGame(int id, Guid userId)
        {
            // Scoped by owner so a user can only open their own games for editing.
            return _db.Games
                .Include(g => g.Console).ThenInclude(c => c.Company)
                .Include(g => g.User)
                .AsNoTracking()
                .FirstOrDefault(g => g.GameId == id && g.UserId == userId) ?? new GameModel();
        }
        public ConsoleModel GetSingleConsole(int id)
        {
            return _db.Consoles
                .Include(c => c.Company)
                .AsNoTracking()
                .FirstOrDefault(c => c.ConsoleId == id) ?? new ConsoleModel();
        }
        public List<GameModel> GetGamesForUser(Guid userId, bool onWishList)
        {
            // Owner + wishlist filters both run in SQL so only the rows the page shows are materialized.
            return _db.Games
                .Where(g => g.UserId == userId && g.IsOnWishList == onWishList)
                .Include(g => g.Console).ThenInclude(c => c.Company)
                .Include(g => g.User)
                .AsNoTracking()
                .ToList();
        }
        public List<ConsoleModel> GetCategoryList()
        {
            return _db.Consoles
                .Include(c => c.Company)
                .AsNoTracking()
                .ToList();
        }
        public List<CompanyModel> GetCompanyList()
        {
            return _db.Companies
                .AsNoTracking()
                .ToList();
        }
        public CompanyModel GetSingleCompany(int id)
        {
            return _db.Companies
                .AsNoTracking()
                .FirstOrDefault(c => c.CompanyId == id) ?? new CompanyModel();
        }
        /// <summary>
        /// Returns the matching user if the password verifies against the stored hash, otherwise null.
        /// </summary>
        public UserModel? ValidateCredentials(string username, string password)
        {
            var normalized = Normalize(username);
            var user = _db.Users.AsNoTracking()
                .FirstOrDefault(u => u.NormalizedUsername == normalized);
            if (user == null || string.IsNullOrEmpty(user.Password))
            {
                return null;
            }

            var result = _hasher.VerifyHashedPassword(user, user.Password, password);
            return result != PasswordVerificationResult.Failed ? user : null;
        }

        /// <summary>
        /// True when the given user id and cookie id identify a known user (server-side session check).
        /// </summary>
        public bool IsValidSession(Guid userId, string cookieId)
        {
            return _db.Users.AsNoTracking().Any(u => u.UserID == userId && u.CookieID == cookieId);
        }

        /// <summary>
        /// Like <see cref="IsValidSession"/> but also requires the user to be an admin. Gates
        /// management of globally-shared data (consoles, companies, users).
        /// </summary>
        public bool IsValidAdminSession(Guid userId, string cookieId)
        {
            return _db.Users.AsNoTracking().Any(u => u.UserID == userId && u.CookieID == cookieId && u.IsAdmin);
        }

        #endregion

        private static string Normalize(string? value)
        {
            return (value ?? string.Empty).ToLower().Replace(" ", string.Empty);
        }

        // Returns the id only if a row with it actually exists, else null — so a stale or crafted
        // FK value degrades to "(none)" instead of throwing a foreign-key constraint violation.
        private int? ExistingConsoleId(int? id)
        {
            return id.HasValue && _db.Consoles.Any(c => c.ConsoleId == id.Value) ? id : null;
        }

        private int? ExistingCompanyId(int? id)
        {
            return id.HasValue && _db.Companies.Any(c => c.CompanyId == id.Value) ? id : null;
        }
    }
}
