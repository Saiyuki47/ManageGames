using System.Security.Cryptography;
using ManageGames.Data;
using ManageGames.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ManageGames.Services
{
    public enum CreateUserResult
    {
        Created,
        UsernameTaken,
    }

    public enum DeleteUserResult
    {
        Deleted,
        NotFound,
        CannotDeleteSelf,
    }

    /// <summary>
    /// User accounts: credentials, password changes and the security stamp that ties auth cookies
    /// to the database.
    /// </summary>
    public class UserService
    {
        private const string DefaultAdminUsername = "admin";
        // Characters for generated passwords; look-alikes (0/O, 1/l/I) are left out.
        private const string PasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

        // Verifying against this hash when a username doesn't exist makes that failure take as long
        // as a wrong password, so response times don't reveal which usernames exist.
        private static readonly string TimingDummyHash = new PasswordHasher<UserModel>().HashPassword(new UserModel(), "timing-dummy");

        private readonly AppDbContext _db;
        private readonly ILogger<UserService> _logger;
        private readonly PasswordHasher<UserModel> _hasher = new();

        public UserService(AppDbContext db, ILogger<UserService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Creates the first admin on an empty database. Without a configured password a random
        /// one-time password is generated and logged; it has to be changed at the first login.
        /// </summary>
        public void EnsureInitialAdmin(string? configuredUsername, string? configuredPassword)
        {
            if (_db.Users.Any())
            {
                return;
            }

            var username = string.IsNullOrWhiteSpace(configuredUsername) ? DefaultAdminUsername : configuredUsername;
            if (!string.IsNullOrEmpty(configuredPassword))
            {
                CreateUser(username, configuredPassword, isAdmin: true, mustChangePassword: false);
                return;
            }

            var oneTimePassword = RandomNumberGenerator.GetString(PasswordAlphabet, 16);
            CreateUser(username, oneTimePassword, isAdmin: true, mustChangePassword: true);
            _logger.LogWarning(
                "Created the initial admin account '{Username}' with the one-time password '{Password}'. You will be asked to choose your own password after logging in.",
                username, oneTimePassword);
        }

        /// <summary>
        /// Versions before the EF Core rewrite stored passwords in plaintext, which the hasher can't
        /// verify (it throws on values that aren't Base64). Hashes them once; since the plaintext may
        /// have been exposed, those users have to choose a new password and their sessions end.
        /// </summary>
        public void HashPlaintextPasswords()
        {
            var users = _db.Users
                .AsEnumerable()
                .Where(u => u.PasswordHash.Length > 0 && !IsPasswordHash(u.PasswordHash))
                .ToList();
            if (users.Count == 0)
            {
                return;
            }

            foreach (var user in users)
            {
                user.PasswordHash = _hasher.HashPassword(user, user.PasswordHash);
                user.MustChangePassword = true;
                user.SecurityStamp = NewSecurityStamp();
            }
            _db.SaveChanges();
            _logger.LogWarning(
                "Hashed the plaintext passwords of {Count} user(s). They have to choose a new password at their next login.",
                users.Count);
        }

        /// <summary>
        /// Returns the user when the password matches, otherwise null. Hashes created with older
        /// hashing parameters are upgraded on the fly.
        /// </summary>
        public UserModel? ValidateCredentials(string? username, string? password)
        {
            password ??= string.Empty;
            var normalized = Normalize(username);
            var user = _db.Users.FirstOrDefault(u => u.NormalizedUsername == normalized);
            if (user == null)
            {
                _hasher.VerifyHashedPassword(new UserModel(), TimingDummyHash, password);
                return null;
            }

            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (result == PasswordVerificationResult.Failed)
            {
                return null;
            }
            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _hasher.HashPassword(user, password);
                _db.SaveChanges();
            }
            return user;
        }

        public CreateUserResult CreateUser(string username, string password, bool isAdmin, bool mustChangePassword)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
            ArgumentException.ThrowIfNullOrEmpty(password);

            var normalized = Normalize(username);
            // Checked up front: the unique index on NormalizedUsername would otherwise throw on save.
            if (_db.Users.Any(u => u.NormalizedUsername == normalized))
            {
                return CreateUserResult.UsernameTaken;
            }

            var user = new UserModel
            {
                UserID = Guid.NewGuid(),
                Username = username.Trim(),
                NormalizedUsername = normalized,
                IsAdmin = isAdmin,
                MustChangePassword = mustChangePassword,
                SecurityStamp = NewSecurityStamp(),
            };
            user.PasswordHash = _hasher.HashPassword(user, password);

            _db.Users.Add(user);
            _db.SaveChanges();
            return CreateUserResult.Created;
        }

        /// <summary>Returns false when the current password is wrong.</summary>
        public bool ChangePassword(Guid userId, string currentPassword, string newPassword)
        {
            var user = _db.Users.Find(userId);
            if (user == null
                || _hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
            {
                return false;
            }

            SetPassword(user, newPassword, mustChangePassword: false);
            return true;
        }

        /// <summary>
        /// Sets a temporary password chosen by an admin; the user has to replace it at the next login.
        /// </summary>
        public bool ResetPassword(Guid userId, string temporaryPassword)
        {
            var user = _db.Users.Find(userId);
            if (user == null)
            {
                return false;
            }

            SetPassword(user, temporaryPassword, mustChangePassword: true);
            return true;
        }

        /// <summary>Revokes every session of the user, on all devices (used by logout).</summary>
        public void EndSessions(Guid userId)
        {
            var user = _db.Users.Find(userId);
            if (user == null)
            {
                return;
            }

            user.SecurityStamp = NewSecurityStamp();
            _db.SaveChanges();
        }

        public DeleteUserResult DeleteUser(Guid userId, Guid actingUserId)
        {
            // Admins can't delete their own account, which also guarantees that one admin always remains.
            if (userId == actingUserId)
            {
                return DeleteUserResult.CannotDeleteSelf;
            }

            var user = _db.Users.Find(userId);
            if (user == null)
            {
                return DeleteUserResult.NotFound;
            }

            // The user's games are deleted along with them (cascading foreign key).
            _db.Users.Remove(user);
            _db.SaveChanges();
            return DeleteUserResult.Deleted;
        }

        public UserModel? GetUser(Guid userId)
        {
            return _db.Users.AsNoTracking().FirstOrDefault(u => u.UserID == userId);
        }

        public List<UserModel> GetUsers()
        {
            return _db.Users.AsNoTracking().OrderBy(u => u.NormalizedUsername).ToList();
        }

        public string? GetSecurityStamp(Guid userId)
        {
            return _db.Users
                .Where(u => u.UserID == userId)
                .Select(u => u.SecurityStamp)
                .FirstOrDefault();
        }

        private void SetPassword(UserModel user, string newPassword, bool mustChangePassword)
        {
            ArgumentException.ThrowIfNullOrEmpty(newPassword);

            user.PasswordHash = _hasher.HashPassword(user, newPassword);
            user.MustChangePassword = mustChangePassword;
            // New credentials end every session that was started with the old ones.
            user.SecurityStamp = NewSecurityStamp();
            _db.SaveChanges();
        }

        // PasswordHasher output is Base64 with a format marker: 0x00 for the Identity v2 format (always
        // 49 bytes) or 0x01 for v3 (a 13-byte header followed by salt and subkey).
        private static bool IsPasswordHash(string value)
        {
            var bytes = new byte[value.Length];
            if (!Convert.TryFromBase64String(value, bytes, out var length) || length == 0)
            {
                return false;
            }
            return (bytes[0] == 0x00 && length == 49) || (bytes[0] == 0x01 && length > 13);
        }

        // Lower-cased with spaces removed, like the backfill in the UsernameRequiredAndUnique migration.
        private static string Normalize(string? username)
        {
            return (username ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", string.Empty);
        }

        private static string NewSecurityStamp()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        }
    }
}
