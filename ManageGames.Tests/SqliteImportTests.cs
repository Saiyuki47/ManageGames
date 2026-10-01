using System.Net;
using ManageGames.Auth;
using ManageGames.Data;
using ManageGames.Models;
using ManageGames.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ManageGames.Tests;

/// <summary>Hosts the app on a database the test prepared.</summary>
public class ExistingDatabaseFactory(string connectionString) : ManageGamesFactory
{
    public override ValueTask InitializeAsync()
    {
        ConnectionString = connectionString;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Moving older, SQLite-based installations to PostgreSQL. The SQLite databases are built from the schema
/// scripts of the old versions (LegacySqlite/*.sql, generated from their migrations).
/// </summary>
public sealed class SqliteImportTests : IAsyncLifetime
{
    private const string LegacyPassword = "legacy-password";

    private readonly string _sqlitePath = Path.Combine(Path.GetTempPath(), $"managegames-import-{Guid.NewGuid():N}.db");
    private string _connectionString = string.Empty;

    public async ValueTask InitializeAsync()
    {
        _connectionString = await TestDatabase.CreateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_sqlitePath);
        await TestDatabase.DropAsync(_connectionString);
    }

    [Fact]
    public async Task Import_OfTheLayoutBeforeIdentity_KeepsUsersAdminsGamesAndIds()
    {
        var hash = new PasswordHasher<AppUser>().HashPassword(new AppUser(), LegacyPassword);
        var adminId = Guid.NewGuid();
        // Early versions wrote GUIDs in lower case and booleans as text.
        var aliceId = Guid.NewGuid();
        CreateSqlite("20260927002711_AccountSecurity", $"""
            INSERT INTO tblUser (UserID, Username, NormalizedUsername, PasswordHash, IsAdmin, MustChangePassword, SecurityStamp, CreatedAt, UpdatedAt)
            VALUES ('{adminId.ToString().ToUpperInvariant()}', 'user', 'user', '{hash}', 1, 1, 'STAMP1', '2026-08-01 10:00:00', '2026-08-02 10:00:00'),
                   ('{aliceId}', 'Alice', 'alice', '{hash}', 'False', 0, 'STAMP2', '2026-08-03 10:00:00', '2026-08-03 10:00:00');
            INSERT INTO tblCompanies (ID, CompanyName) VALUES (7, 'Nintendo');
            INSERT INTO tblConsoles (ID, ConsoleName, Company) VALUES (3, 'Switch', 7), (4, 'Unknown maker', 99);
            INSERT INTO tblGames (ID, Console, GameName, Copies, IsOnWishList, UserID, CreatedAt, UpdatedAt)
            VALUES (5, 3, 'Zelda', 1, 0, '{adminId.ToString().ToUpperInvariant()}', '2026-08-20 10:00:00.1234567', '2026-08-20 10:00:00'),
                   (9, 42, 'Metroid', 2, 1, '{aliceId}', '2026-08-21 10:00:00', '2026-08-21 10:00:00'),
                   (11, NULL, 'Ghost', 1, 0, '{Guid.NewGuid()}', '2026-08-22 10:00:00', '2026-08-22 10:00:00');
            """);

        var result = await ImportAsync();

        Assert.Equal(new ImportResult(Users: 2, Companies: 1, Consoles: 2, Games: 2), result);
        await using (var db = CreateContext())
        {
            Assert.Equal([adminId], await AdminIdsAsync(db));
            Assert.Equal("Alice", (await db.Users.SingleAsync(u => u.Id == aliceId)).UserName);
            Assert.Equal(7, (await db.Consoles.SingleAsync(c => c.Id == 3)).CompanyId);
            Assert.Null((await db.Consoles.SingleAsync(c => c.Id == 4)).CompanyId);
            var zelda = await db.Games.SingleAsync(g => g.Id == 5);
            Assert.Equal((adminId, (int?)3), (zelda.UserId, zelda.ConsoleId));
            // PostgreSQL keeps microseconds; the seventh decimal of the .NET value is dropped.
            Assert.Equal(new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc).AddTicks(1234560), zelda.CreatedAt);
            Assert.Null((await db.Games.SingleAsync(g => g.Id == 9)).ConsoleId);
        }

        // Everybody signs in with their old password; the formerly seeded admin still has to change it.
        await using var factory = new ExistingDatabaseFactory(_connectionString);
        await factory.InitializeAsync();
        var admin = await factory.SignInAsync("user", LegacyPassword);
        Browser.AssertRedirect(await admin.GetAsync("/Games"), "/Account/ChangePassword");
        var alice = await factory.SignInAsync("alice", LegacyPassword);
        Assert.Contains("Metroid", await alice.GetPageAsync("/Games/Wishlist"), StringComparison.Ordinal);

        // New rows continue after the highest imported id (9; the ownerless game 11 was skipped) instead
        // of starting at 1 again and running into the imported ids.
        Browser.AssertRedirect(await alice.AddGameAsync("Samus Returns"), "/Games");
        Assert.Equal(10, factory.Query(db => db.Games.Single(g => g.Name == "Samus Returns").Id));
    }

    [Fact]
    public async Task Import_OfTheLastSqliteLayout_KeepsRolesAndGames()
    {
        var hash = new PasswordHasher<AppUser>().HashPassword(new AppUser(), LegacyPassword);
        var adminId = Guid.NewGuid();
        var bobId = Guid.NewGuid();
        // The schema script itself already created the Admin role with this id.
        const string AdminRoleId = "6E9D3F0B-8C2A-4B7E-9F1D-2A5C7E4B8D10";
        CreateSqlite("20260930215558_AspNetIdentity", $"""
            INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
                PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, MustChangePassword, CreatedAt, UpdatedAt)
            VALUES ('{adminId.ToString().ToUpperInvariant()}', 'Chief', 'chief', 0, '{hash}', 'S1', 'C1', 0, 0, 1, 0, 0, '2026-09-30 10:00:00', '2026-09-30 10:00:00'),
                   ('{bobId.ToString().ToUpperInvariant()}', 'Bob', 'bob', 0, '{hash}', 'S2', 'C2', 0, 0, 1, 0, 0, '2026-09-30 11:00:00', '2026-09-30 11:00:00');
            INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES ('{adminId.ToString().ToUpperInvariant()}', '{AdminRoleId}');
            INSERT INTO Companies (Id, Name, CreatedAt, UpdatedAt) VALUES (1, 'Sega', '2026-09-30 10:00:00', '2026-09-30 10:00:00');
            INSERT INTO Consoles (Id, Name, CompanyId, CreatedAt, UpdatedAt) VALUES (2, 'Dreamcast', 1, '2026-09-30 10:00:00', '2026-09-30 10:00:00');
            INSERT INTO Games (Id, Name, Copies, IsOnWishList, ConsoleId, UserId, CreatedAt, UpdatedAt)
            VALUES (3, 'Shenmue', 1, 0, 2, '{bobId.ToString().ToUpperInvariant()}', '2026-09-30 12:00:00', '2026-09-30 12:00:00');
            """);

        var result = await ImportAsync();

        Assert.Equal(new ImportResult(Users: 2, Companies: 1, Consoles: 1, Games: 1), result);
        await using (var db = CreateContext())
        {
            Assert.Equal([adminId], await AdminIdsAsync(db));
            var shenmue = await db.Games.SingleAsync(g => g.Id == 3);
            Assert.Equal((bobId, (int?)2), (shenmue.UserId, shenmue.ConsoleId));
        }

        await using var factory = new ExistingDatabaseFactory(_connectionString);
        await factory.InitializeAsync();
        var chief = await factory.SignInAsync("Chief", LegacyPassword);
        Assert.Equal(HttpStatusCode.OK, (await chief.GetAsync("/Users")).StatusCode);
    }

    [Fact]
    public async Task Import_OfPlaintextPasswords_HashesThemAtTheNextStart_AndForcesAChange()
    {
        // Versions before the EF Core rewrite stored the password itself, e.g. the seeded 'password1'.
        CreateSqlite("20260815222929_CompanyTimestamps", $"""
            INSERT INTO tblUser (UserID, ProfilePicturesID, Username, NormalizedUsername, Password, IsAdmin, CookieID, CreatedAt, UpdatedAt)
            VALUES ('{Guid.NewGuid()}', '00000000-0000-0000-0000-000000000000', 'user', 'user', 'password1', 'True', 'StartCook',
                    '2026-08-01 10:00:00', '2026-08-01 10:00:00');
            """);

        await ImportAsync();

        await using var factory = new ExistingDatabaseFactory(_connectionString);
        await factory.InitializeAsync();
        var browser = await factory.SignInAsync("user", "password1");
        Browser.AssertRedirect(await browser.GetAsync("/Games"), "/Account/ChangePassword");
        var hash = factory.Query(db => db.Users.Single().PasswordHash)!;
        Assert.Equal(0x01, Convert.FromBase64String(hash)[0]);
        Assert.True(factory.Query(db => db.UserRoles.Any()));
    }

    [Fact]
    public async Task Import_RefusesADatabaseThatAlreadyHoldsData()
    {
        CreateSqlite("20260927002711_AccountSecurity", "INSERT INTO tblCompanies (ID, CompanyName) VALUES (1, 'Atari');");
        await ImportAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(ImportAsync);
    }

    [Fact]
    public async Task Import_RejectsUnknownLayouts()
    {
        CreateSqlite(schemaScript: null, "CREATE TABLE Something (Id INTEGER);");

        await Assert.ThrowsAsync<NotSupportedException>(ImportAsync);
    }

    private async Task<ImportResult> ImportAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        return await new SqliteImporter(db, new UsernameNormalizer(), NullLogger<SqliteImporter>.Instance).ImportAsync(_sqlitePath);
    }

    private AppDbContext CreateContext()
    {
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString, npgsql => npgsql.EnableRetryOnFailure())
            .Options);
    }

    private static Task<List<Guid>> AdminIdsAsync(AppDbContext db)
    {
        return db.UserRoles
            .Where(ur => db.Roles.Any(r => r.Id == ur.RoleId && r.Name == Roles.Admin))
            .Select(ur => ur.UserId)
            .ToListAsync();
    }

    // Builds an old app's SQLite database from its schema script and fills it with data.
    private void CreateSqlite(string? schemaScript, string data)
    {
        using var connection = new SqliteConnection($"Data Source={_sqlitePath};Pooling=False");
        connection.Open();
        if (schemaScript != null)
        {
            Execute(connection, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "LegacySqlite", schemaScript + ".sql")));
        }
        // The old versions didn't enforce foreign keys, so their databases may hold dangling references.
        Execute(connection, "PRAGMA foreign_keys = OFF;");
        Execute(connection, data);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        // Reading through every statement's result makes errors in later statements surface too.
        using var reader = command.ExecuteReader();
        while (reader.NextResult())
        {
        }
    }
}
