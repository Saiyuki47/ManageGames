using ManageGames.Data;
using ManageGames.Models;
using ManageGames.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ManageGames.Tests;

/// <summary>The consoles and their makers every new database starts with.</summary>
public class CatalogSeedTests(ManageGamesFactory factory) : IClassFixture<ManageGamesFactory>
{
    public const int ConsoleCount = 63;
    public const int CompanyCount = 23;

    public static readonly string[] NintendoConsoles =
    [
        "Game & Watch", "Nintendo Entertainment System", "Game Boy", "Super Nintendo Entertainment System", "Virtual Boy",
        "Nintendo 64", "Game Boy Color", "Game Boy Advance", "Nintendo GameCube", "Nintendo DS", "Wii", "Nintendo 3DS",
        "Wii U", "Nintendo Switch", "Nintendo Switch 2",
    ];

    public static readonly string[] SonyConsoles =
    [
        "PlayStation", "PlayStation 2", "PlayStation Portable", "PlayStation 3", "PlayStation Vita", "PlayStation 4", "PlayStation 5",
    ];

    public static readonly string[] MicrosoftConsoles = ["Xbox", "Xbox 360", "Xbox One", "Xbox Series X|S"];

    public static readonly string[] SegaConsoles =
    [
        "Sega SG-1000", "Sega Master System", "Sega Mega Drive", "Sega Game Gear", "Sega Mega-CD", "Sega 32X", "Sega Saturn",
        "Sega Dreamcast",
    ];

    [Fact]
    public void NewDatabase_HasTheWellKnownConsoles_EachLinkedToItsMaker()
    {
        var consoles = factory.Query(db => db.Consoles.Include(c => c.Company).ToList());

        Assert.Equal(ConsoleCount, consoles.Count);
        Assert.Equal(CompanyCount, factory.Query(db => db.Companies.Count()));
        Assert.All(consoles, c => Assert.NotNull(c.Company));
        Assert.Equal(NintendoConsoles.Order(), MadeBy(consoles, "Nintendo"));
        Assert.Equal(SonyConsoles.Order(), MadeBy(consoles, "Sony"));
        Assert.Equal(MicrosoftConsoles.Order(), MadeBy(consoles, "Microsoft"));
        Assert.Equal(SegaConsoles.Order(), MadeBy(consoles, "Sega"));
    }

    [Fact]
    public async Task SeededConsoles_CanBeChosenForGames()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var wiiId = factory.Query(db => db.Consoles.Single(c => c.Name == "Wii").Id);

        Browser.AssertRedirect(await browser.AddGameAsync(ManageGamesFactory.Unique("Mario Kart Wii"), consoleId: wiiId), "/Games");

        Assert.Contains("Wii", await browser.GetPageAsync("/Games"), StringComparison.Ordinal);
    }

    private static List<string> MadeBy(List<GameConsole> consoles, string company)
    {
        return consoles.Where(c => c.Company?.Name == company).Select(c => c.Name).Order().ToList();
    }
}

/// <summary>The seed migration on a database that already holds entries of its own.</summary>
public sealed class CatalogSeedMigrationTests : IAsyncLifetime
{
    private const string MigrationBeforeSeed = "20261001160132_InitialCreate";

    private string _connectionString = string.Empty;

    public async ValueTask InitializeAsync()
    {
        _connectionString = await TestDatabase.CreateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await TestDatabase.DropAsync(_connectionString);
    }

    [Fact]
    public async Task Seed_ReusesExistingEntries_AndRollingBackKeepsThem()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connectionString).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationBeforeSeed);
        var nintendo = new Company { Name = "Nintendo" };
        db.Consoles.Add(new GameConsole { Name = "Switch", Company = nintendo });
        await db.SaveChangesAsync();

        await migrator.MigrateAsync();

        // The existing maker is reused, the existing console stays, the seeded ones are added.
        db.ChangeTracker.Clear();
        Assert.Single(await db.Companies.Where(c => c.Name == "Nintendo").ToListAsync());
        Assert.Equal(nintendo.Id, (await db.Consoles.SingleAsync(c => c.Name == "Switch")).CompanyId);
        Assert.Equal(CatalogSeedTests.NintendoConsoles.Length + 1, await db.Consoles.CountAsync(c => c.CompanyId == nintendo.Id));
        Assert.Equal(CatalogSeedTests.SonyConsoles.Length, await db.Consoles.CountAsync(c => c.Company!.Name == "Sony"));
        Assert.Equal(CatalogSeedTests.ConsoleCount + 1, await db.Consoles.CountAsync());
        Assert.Equal(CatalogSeedTests.CompanyCount, await db.Companies.CountAsync());

        // Rolling back removes the seeded entries only.
        await migrator.MigrateAsync(MigrationBeforeSeed);
        Assert.Equal(["Switch"], await db.Consoles.Select(c => c.Name).ToListAsync());
        Assert.Equal(["Nintendo"], await db.Companies.Select(c => c.Name).ToListAsync());
    }
}
