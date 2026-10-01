using System.Globalization;
using ManageGames.Auth;
using ManageGames.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ManageGames.Data;

public record ImportResult(int Users, int Companies, int Consoles, int Games);

/// <summary>
/// One-time move of an older installation's SQLite database into PostgreSQL
/// (<c>dotnet ManageGames.dll import-sqlite path/to/DataBase.db</c>). Reads both SQLite layouts the app
/// had: the tbl* tables before ASP.NET Core Identity, and the Identity tables of the last SQLite version.
/// The SQLite file is only read. Ids, password hashes and timestamps are kept, so everybody signs in as
/// before; references to rows that no longer exist become empty, and games without an owner are skipped.
/// </summary>
public partial class SqliteImporter(AppDbContext db, ILookupNormalizer normalizer, ILogger<SqliteImporter> logger)
{
    private const string ResetIdSequences = """
        SELECT setval(pg_get_serial_sequence('"Companies"', 'Id'), COALESCE(MAX("Id"), 0) + 1, false) FROM "Companies";
        SELECT setval(pg_get_serial_sequence('"Consoles"', 'Id'), COALESCE(MAX("Id"), 0) + 1, false) FROM "Consoles";
        SELECT setval(pg_get_serial_sequence('"Games"', 'Id'), COALESCE(MAX("Id"), 0) + 1, false) FROM "Games";
        """;

    private sealed record SourceCompany(int Id, string Name, DateTime? CreatedAt, DateTime? UpdatedAt);

    private sealed record SourceConsole(int Id, string Name, int? CompanyId, DateTime? CreatedAt, DateTime? UpdatedAt);

    private sealed record SourceUser(
        Guid Id, string UserName, string? PasswordHash, string? SecurityStamp, bool IsAdmin, bool MustChangePassword,
        DateTime? CreatedAt, DateTime? UpdatedAt);

    private sealed record SourceGame(
        int Id, string Name, int Copies, bool IsOnWishList, int? ConsoleId, Guid UserId, DateTime? CreatedAt, DateTime? UpdatedAt);

    private sealed record SourceData(List<SourceCompany> Companies, List<SourceConsole> Consoles, List<SourceUser> Users, List<SourceGame> Games);

    public async Task<ImportResult> ImportAsync(string sqlitePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sqlitePath))
        {
            throw new FileNotFoundException("The SQLite database to import doesn't exist.", sqlitePath);
        }
        if (await db.Users.AnyAsync(cancellationToken) || await db.Games.AnyAsync(cancellationToken)
            || await db.Consoles.AnyAsync(cancellationToken) || await db.Companies.AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException("The PostgreSQL database already contains data. Import into a new, empty database.");
        }

        SourceData source;
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = sqlitePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString();
        await using (var sqlite = new SqliteConnection(connectionString))
        {
            await sqlite.OpenAsync(cancellationToken);
            var tables = await TablesAsync(sqlite, cancellationToken);
            if (tables.Contains("AspNetUsers"))
            {
                source = await ReadIdentityLayoutAsync(sqlite, cancellationToken);
            }
            else if (tables.Contains("tblUser") && (await ColumnsAsync(sqlite, "tblGames", cancellationToken)).Contains("GameName"))
            {
                source = await ReadLegacyLayoutAsync(sqlite, cancellationToken);
            }
            else
            {
                throw new NotSupportedException(
                    "This SQLite database has a layout from before August 2026. Start the last SQLite version of ManageGames "
                    + "(commit e741e94) on it once to upgrade it, then import it.");
            }
        }

        var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(() => WriteAsync(source, cancellationToken));
        LogImported(logger, result.Users, result.Companies, result.Consoles, result.Games);
        return result;
    }

    private async Task<ImportResult> WriteAsync(SourceData source, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var adminRoleName = normalizer.NormalizeName(Roles.Admin);
        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == adminRoleName, cancellationToken);
        if (adminRole == null)
        {
            adminRole = new IdentityRole<Guid>(Roles.Admin)
            {
                Id = Guid.NewGuid(),
                NormalizedName = adminRoleName,
                ConcurrencyStamp = Guid.NewGuid().ToString(),
            };
            db.Roles.Add(adminRole);
        }

        foreach (var company in source.Companies)
        {
            db.Companies.Add(new Company { Id = company.Id, Name = company.Name, CreatedAt = company.CreatedAt ?? default, UpdatedAt = company.UpdatedAt ?? default });
        }

        var companyIds = source.Companies.Select(c => c.Id).ToHashSet();
        foreach (var console in source.Consoles)
        {
            db.Consoles.Add(new GameConsole
            {
                Id = console.Id,
                Name = console.Name,
                CompanyId = console.CompanyId is { } companyId && companyIds.Contains(companyId) ? companyId : null,
                CreatedAt = console.CreatedAt ?? default,
                UpdatedAt = console.UpdatedAt ?? default,
            });
        }

        foreach (var user in source.Users)
        {
            db.Users.Add(new AppUser
            {
                Id = user.Id,
                UserName = user.UserName,
                NormalizedUserName = normalizer.NormalizeName(user.UserName),
                // Plaintext passwords of very old versions are hashed at the next start (HashPlaintextPasswordsAsync).
                PasswordHash = string.IsNullOrEmpty(user.PasswordHash) ? null : user.PasswordHash,
                SecurityStamp = string.IsNullOrEmpty(user.SecurityStamp) ? Guid.NewGuid().ToString("N") : user.SecurityStamp,
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                LockoutEnabled = true,
                MustChangePassword = user.MustChangePassword,
                CreatedAt = user.CreatedAt ?? default,
                UpdatedAt = user.UpdatedAt ?? default,
            });
            if (user.IsAdmin)
            {
                db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = adminRole.Id });
            }
        }

        var userIds = source.Users.Select(u => u.Id).ToHashSet();
        var consoleIds = source.Consoles.Select(c => c.Id).ToHashSet();
        var games = source.Games.Where(g => userIds.Contains(g.UserId)).ToList();
        foreach (var game in games)
        {
            db.Games.Add(new Game
            {
                Id = game.Id,
                Name = game.Name,
                Copies = Math.Max(1, game.Copies),
                IsOnWishList = game.IsOnWishList,
                ConsoleId = game.ConsoleId is { } consoleId && consoleIds.Contains(consoleId) ? consoleId : null,
                UserId = game.UserId,
                CreatedAt = game.CreatedAt ?? default,
                UpdatedAt = game.UpdatedAt ?? default,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        // The ids were inserted explicitly, so the identity sequences still start at 1; continue after the
        // highest imported id instead.
        await db.Database.ExecuteSqlRawAsync(ResetIdSequences, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new ImportResult(source.Users.Count, source.Companies.Count, source.Consoles.Count, games.Count);
    }

    private static async Task<SourceData> ReadIdentityLayoutAsync(SqliteConnection sqlite, CancellationToken cancellationToken)
    {
        var admins = (await ReadAsync(sqlite, """
            SELECT ur.UserId FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id = ur.RoleId WHERE lower(r.Name) = 'admin'
            """, r => ParseGuid(r, 0), cancellationToken)).ToHashSet();

        return new SourceData(
            await ReadAsync(sqlite, "SELECT Id, Name, CreatedAt, UpdatedAt FROM Companies",
                r => new SourceCompany(r.GetInt32(0), r.GetString(1), ParseDate(r, 2), ParseDate(r, 3)), cancellationToken),
            await ReadAsync(sqlite, "SELECT Id, Name, CompanyId, CreatedAt, UpdatedAt FROM Consoles",
                r => new SourceConsole(r.GetInt32(0), r.GetString(1), ParseInt(r, 2), ParseDate(r, 3), ParseDate(r, 4)), cancellationToken),
            await ReadAsync(sqlite, "SELECT Id, UserName, PasswordHash, SecurityStamp, MustChangePassword, CreatedAt, UpdatedAt FROM AspNetUsers",
                r => new SourceUser(ParseGuid(r, 0), r.GetString(1), ParseString(r, 2), ParseString(r, 3), admins.Contains(ParseGuid(r, 0)),
                    ParseBool(r, 4), ParseDate(r, 5), ParseDate(r, 6)), cancellationToken),
            await ReadAsync(sqlite, "SELECT Id, Name, Copies, IsOnWishList, ConsoleId, UserId, CreatedAt, UpdatedAt FROM Games",
                r => new SourceGame(r.GetInt32(0), r.GetString(1), r.GetInt32(2), ParseBool(r, 3), ParseInt(r, 4), ParseGuid(r, 5),
                    ParseDate(r, 6), ParseDate(r, 7)), cancellationToken));
    }

    // The tbl* layout grew over time: optional columns are read as NULL when they don't exist yet.
    private static async Task<SourceData> ReadLegacyLayoutAsync(SqliteConnection sqlite, CancellationToken cancellationToken)
    {
        var userColumns = await ColumnsAsync(sqlite, "tblUser", cancellationToken);
        var companyColumns = await ColumnsAsync(sqlite, "tblCompanies", cancellationToken);
        string Optional(HashSet<string> columns, string name) => columns.Contains(name) ? $"\"{name}\"" : "NULL";
        var password = userColumns.Contains("PasswordHash") ? "PasswordHash" : "Password";

        return new SourceData(
            await ReadAsync(sqlite, $"SELECT ID, CompanyName, {Optional(companyColumns, "CreatedAt")}, {Optional(companyColumns, "UpdatedAt")} FROM tblCompanies",
                r => new SourceCompany(r.GetInt32(0), r.GetString(1), ParseDate(r, 2), ParseDate(r, 3)), cancellationToken),
            await ReadAsync(sqlite, "SELECT ID, ConsoleName, Company, CreatedAt, UpdatedAt FROM tblConsoles",
                r => new SourceConsole(r.GetInt32(0), r.GetString(1), ParseInt(r, 2), ParseDate(r, 3), ParseDate(r, 4)), cancellationToken),
            await ReadAsync(sqlite, $"""
                SELECT UserID, Username, "{password}", {Optional(userColumns, "SecurityStamp")}, IsAdmin,
                       {Optional(userColumns, "MustChangePassword")}, CreatedAt, UpdatedAt
                FROM tblUser
                """,
                r => new SourceUser(ParseGuid(r, 0), r.GetString(1), ParseString(r, 2), ParseString(r, 3), ParseBool(r, 4),
                    ParseBool(r, 5), ParseDate(r, 6), ParseDate(r, 7)), cancellationToken),
            await ReadAsync(sqlite, "SELECT ID, GameName, Copies, IsOnWishList, Console, UserID, CreatedAt, UpdatedAt FROM tblGames",
                r => new SourceGame(r.GetInt32(0), r.GetString(1), r.GetInt32(2), ParseBool(r, 3), ParseInt(r, 4), ParseGuid(r, 5),
                    ParseDate(r, 6), ParseDate(r, 7)), cancellationToken));
    }

    private static async Task<List<T>> ReadAsync<T>(SqliteConnection sqlite, string sql, Func<SqliteDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var command = sqlite.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(map(reader));
        }
        return rows;
    }

    private static async Task<HashSet<string>> TablesAsync(SqliteConnection sqlite, CancellationToken cancellationToken)
    {
        return (await ReadAsync(sqlite, "SELECT name FROM sqlite_master WHERE type = 'table'", r => r.GetString(0), cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static async Task<HashSet<string>> ColumnsAsync(SqliteConnection sqlite, string table, CancellationToken cancellationToken)
    {
        return (await ReadAsync(sqlite, $"SELECT name FROM pragma_table_info('{table}')", r => r.GetString(0), cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
    }

    // Early versions wrote GUIDs in lower case and booleans as 'True'/'False' text; SQLite kept them as written.
    private static Guid ParseGuid(SqliteDataReader reader, int ordinal)
    {
        return Guid.Parse(reader.GetString(ordinal));
    }

    private static bool ParseBool(SqliteDataReader reader, int ordinal)
    {
        return !reader.IsDBNull(ordinal) && reader.GetValue(ordinal) switch
        {
            long number => number != 0,
            string text => text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1",
            _ => false,
        };
    }

    private static int? ParseInt(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static string? ParseString(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    // SQLite stored the timestamps as UTC text, e.g. "2026-08-15 22:30:38.6905517".
    private static DateTime? ParseDate(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? null
            : DateTime.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Imported {Users} user(s), {Companies} companies, {Consoles} consoles and {Games} games from SQLite.")]
    private static partial void LogImported(ILogger logger, int users, int companies, int consoles, int games);
}
