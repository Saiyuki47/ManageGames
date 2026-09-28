using ManageGames.Data;
using ManageGames.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ManageGames.Tests
{
    /// <summary>Upgrades a database that holds data in the schema before the AccountSecurity migration.</summary>
    public sealed class MigrationTests : IDisposable
    {
        private const string LastMigrationBeforeAccountSecurity = "20260815222929_CompanyTimestamps";

        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"managegames-migration-{Guid.NewGuid():N}.db");

        [Fact]
        public void AccountSecurity_KeepsExistingUsersAndGames_BothWays()
        {
            var hasher = new PasswordHasher<UserModel>();
            var legacyAdminId = Guid.NewGuid().ToString().ToUpperInvariant();
            var otherUserId = Guid.NewGuid().ToString().ToUpperInvariant();
            var legacyHash = hasher.HashPassword(new UserModel(), "legacy-password");

            using (var db = CreateContext())
            {
                var migrator = db.GetService<IMigrator>();
                migrator.Migrate(LastMigrationBeforeAccountSecurity);
                db.Database.ExecuteSql($"""
                    INSERT INTO tblUser (UserID, ProfilePicturesID, Username, NormalizedUsername, Password, IsAdmin, CookieID)
                    VALUES ({legacyAdminId}, '00000000-0000-0000-0000-000000000000', 'user', 'user', {legacyHash}, 1, 'StartCook'),
                           ({otherUserId}, '00000000-0000-0000-0000-000000000000', 'Alice', 'alice', {legacyHash}, 0, NULL)
                    """);
                db.Database.ExecuteSql($"INSERT INTO tblGames (Console, GameName, Copies, IsOnWishList, UserID) VALUES (NULL, 'Zelda', 1, 0, {legacyAdminId})");

                migrator.Migrate();
            }

            using (var db = CreateContext())
            {
                var users = db.Users.OrderBy(u => u.NormalizedUsername).ToList();
                Assert.Equal(new[] { "alice", "user" }, users.Select(u => u.NormalizedUsername));
                Assert.All(users, u => Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(u, u.PasswordHash, "legacy-password")));
                Assert.All(users, u => Assert.Equal(32, u.SecurityStamp.Length));
                Assert.NotEqual(users[0].SecurityStamp, users[1].SecurityStamp);
                // Only the formerly seeded admin 'user', whose password was public, has to change it.
                Assert.False(users[0].MustChangePassword);
                Assert.True(users[1].MustChangePassword);
                Assert.Equal("Zelda", db.Games.Single().GameName);

                // Rolling back rebuilds tblUser; the games that reference it must survive that.
                db.GetService<IMigrator>().Migrate(LastMigrationBeforeAccountSecurity);
                Assert.Equal(2L, Scalar(db, "SELECT count(*) FROM tblUser"));
                Assert.Equal(1L, Scalar(db, "SELECT count(*) FROM tblGames"));
                Assert.Equal(legacyHash, Scalar(db, "SELECT Password FROM tblUser WHERE NormalizedUsername = 'user'"));
            }
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            File.Delete(_databasePath);
        }

        private AppDbContext CreateContext()
        {
            return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={_databasePath}")
                .Options);
        }

        private static object? Scalar(AppDbContext db, string sql)
        {
            var connection = db.Database.GetDbConnection();
            connection.Open();
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                return command.ExecuteScalar();
            }
            finally
            {
                connection.Close();
            }
        }
    }
}
