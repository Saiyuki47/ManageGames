using ManageGames.Data;
using ManageGames.Models;
using ManageGames.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ManageGames.Tests;

/// <summary>Hosts the app on a database file the test prepared.</summary>
public class ExistingDatabaseFactory(string databasePath) : ManageGamesFactory
{
    protected override string DatabasePath => databasePath;
}

/// <summary>Upgrades databases that hold data in the schema of older versions.</summary>
public sealed class MigrationTests : IDisposable
{
    // The schema before AccountSecurity: plain Password column, CookieID, ProfilePicturesID.
    private const string SchemaBeforeAccountSecurity = "20260815222929_CompanyTimestamps";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"managegames-migration-{Guid.NewGuid():N}.db");

    [Fact]
    public void Upgrade_MovesUsersRolesAndGamesToIdentity_AndBack()
    {
        var hasher = new PasswordHasher<AppUser>();
        var legacyHash = hasher.HashPassword(new AppUser(), "legacy-password");
        var adminId = Guid.NewGuid();
        // Early versions wrote GUIDs in lower case; EF Core writes and compares them in upper case.
        var aliceId = Guid.NewGuid();
        var ghostId = Guid.NewGuid();

        using (var db = CreateContext())
        {
            db.GetService<IMigrator>().Migrate(SchemaBeforeAccountSecurity);
            // Old versions didn't enforce foreign keys, so dangling references may exist.
            db.Database.OpenConnection();
            db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
            db.Database.ExecuteSql($"""
                INSERT INTO tblUser (UserID, ProfilePicturesID, Username, NormalizedUsername, Password, IsAdmin, CookieID)
                VALUES ({adminId.ToString().ToUpperInvariant()}, '00000000-0000-0000-0000-000000000000', 'user', 'user', {legacyHash}, 1, 'StartCook'),
                       ({aliceId.ToString()}, '00000000-0000-0000-0000-000000000000', 'Alice', 'alice', {legacyHash}, 0, NULL)
                """);
            db.Database.ExecuteSql($"INSERT INTO tblCompanies (ID, CompanyName) VALUES (7, 'Nintendo')");
            db.Database.ExecuteSql($"INSERT INTO tblConsoles (ID, ConsoleName, Company) VALUES (3, 'Switch', 7), (4, 'Unknown maker', 99)");
            db.Database.ExecuteSql($"""
                INSERT INTO tblGames (Console, GameName, Copies, IsOnWishList, UserID)
                VALUES (3, 'Zelda', 1, 0, {adminId.ToString().ToUpperInvariant()}),
                       (42, 'Metroid', 2, 1, {aliceId.ToString()}),
                       (NULL, 'Ghost', 1, 0, {ghostId.ToString()})
                """);
            db.Database.CloseConnection();

            db.GetService<IMigrator>().Migrate();
        }

        using (var db = CreateContext())
        {
            var users = db.Users.OrderBy(u => u.NormalizedUserName).ToList();
            Assert.Equal(["alice", "user"], users.Select(u => u.NormalizedUserName));
            Assert.Equal([aliceId, adminId], users.Select(u => u.Id));
            Assert.All(users, u => Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(u, u.PasswordHash!, "legacy-password")));
            Assert.All(users, u => Assert.True(u.LockoutEnabled));
            // Only the formerly seeded admin 'user', whose password was public, has to change it.
            Assert.Equal([false, true], users.Select(u => u.MustChangePassword));

            var adminRole = db.Roles.Single();
            Assert.Equal(("Admin", "admin"), (adminRole.Name, adminRole.NormalizedName));
            Assert.Equal([adminId], db.UserRoles.Where(r => r.RoleId == adminRole.Id).Select(r => r.UserId).ToList());

            Assert.Equal("Nintendo", db.Companies.Single(c => c.Id == 7).Name);
            Assert.Equal(7, db.Consoles.Single(c => c.Id == 3).CompanyId);
            Assert.Null(db.Consoles.Single(c => c.Id == 4).CompanyId);

            // Games stay with their owners (also the lower-case one); the ownerless one is gone.
            Assert.Equal(("Zelda", (int?)3), db.Games.Where(g => g.UserId == adminId).Select(g => ValueTuple.Create(g.Name, g.ConsoleId)).Single());
            Assert.Equal(("Metroid", (int?)null), db.Games.Where(g => g.UserId == aliceId).Select(g => ValueTuple.Create(g.Name, g.ConsoleId)).Single());
            Assert.Equal(2, db.Games.Count());

            // Rolling back restores the old tables with the same data.
            db.GetService<IMigrator>().Migrate(SchemaBeforeAccountSecurity);
            Assert.Equal(2L, Scalar(db, "SELECT count(*) FROM tblUser"));
            Assert.Equal(2L, Scalar(db, "SELECT count(*) FROM tblGames"));
            Assert.Equal(2L, Scalar(db, "SELECT count(*) FROM tblConsoles"));
            Assert.Equal(legacyHash, Scalar(db, "SELECT Password FROM tblUser WHERE NormalizedUsername = 'user'"));
            Assert.Equal(1L, Scalar(db, "SELECT IsAdmin FROM tblUser WHERE NormalizedUsername = 'user'"));
            Assert.Equal(0L, Scalar(db, "SELECT IsAdmin FROM tblUser WHERE NormalizedUsername = 'alice'"));
        }
    }

    [Fact]
    public async Task PlaintextPasswords_OfOldVersions_AreHashed_AndMustBeChanged()
    {
        using (var db = CreateContext())
        {
            db.GetService<IMigrator>().Migrate(SchemaBeforeAccountSecurity);
            // Versions before the EF Core rewrite stored the password itself, e.g. the seeded 'password1'.
            db.Database.ExecuteSql($"""
                INSERT INTO tblUser (UserID, ProfilePicturesID, Username, NormalizedUsername, Password, IsAdmin, CookieID)
                VALUES ({Guid.NewGuid().ToString()}, '00000000-0000-0000-0000-000000000000', 'user', 'user', 'password1', 1, 'StartCook')
                """);
        }
        SqliteConnection.ClearAllPools();

        // Starting the app migrates the database and hashes the plaintext password.
        using var factory = new ExistingDatabaseFactory(_databasePath);
        var browser = await factory.SignInAsync("user", "password1");

        Browser.AssertRedirect(await browser.GetAsync("/Games"), "/Account/ChangePassword");
        var hash = factory.Query(db => db.Users.Single().PasswordHash)!;
        Assert.NotEqual("password1", hash);
        Assert.Equal(0x01, Convert.FromBase64String(hash)[0]);
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
