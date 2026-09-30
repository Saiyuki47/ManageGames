using System.Net;
using ManageGames.Tests.Infrastructure;
using ManageGames.ViewModels;

namespace ManageGames.Tests;

public class GameTests(ManageGamesFactory factory) : IClassFixture<ManageGamesFactory>
{
    [Fact]
    public async Task User_CanAddEditAndDeleteTheirOwnGame()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var name = ManageGamesFactory.Unique("Ocarina");

        Browser.AssertRedirect(await browser.AddGameAsync(name, copies: 2), "/Games");
        var gameId = factory.GameId(name);
        Assert.Contains(name, await browser.GetPageAsync("/Games"));

        var renamed = ManageGamesFactory.Unique("Majora");
        var edit = await browser.SubmitFormAsync($"/Games/Edit/{gameId}", $"/Games/Edit/{gameId}", new Dictionary<string, string>
        {
            ["Name"] = renamed,
            ["Copies"] = "3",
            ["OnWishList"] = "false",
        });
        Browser.AssertRedirect(edit, "/Games");
        var edited = factory.Query(db => db.Games.Single(g => g.Id == gameId));
        Assert.Equal((renamed, 3), (edited.Name, edited.Copies));

        Browser.AssertRedirect(await browser.SubmitFormAsync("/Games", $"/Games/Delete/{gameId}"), "/Games");
        Assert.False(factory.Query(db => db.Games.Any(g => g.Id == gameId)));
    }

    [Fact]
    public async Task Games_StayInvisibleAndUntouchable_ForOtherUsers()
    {
        var owner = await factory.SignInAsync(await factory.CreateUserAsync());
        var intruder = await factory.SignInAsync(await factory.CreateUserAsync());
        var name = ManageGamesFactory.Unique("Private");
        await owner.AddGameAsync(name);
        var gameId = factory.GameId(name);

        var list = await intruder.GetPageAsync("/Games");
        var editPage = await intruder.GetAsync($"/Games/Edit/{gameId}");
        var edit = await intruder.SubmitFormAsync("/Games", $"/Games/Edit/{gameId}", new Dictionary<string, string>
        {
            ["Name"] = "Hijacked",
            ["Copies"] = "1",
        });
        var delete = await intruder.SubmitFormAsync("/Games", $"/Games/Delete/{gameId}");

        Assert.DoesNotContain(name, list);
        Assert.Equal(HttpStatusCode.NotFound, editPage.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(name, factory.Query(db => db.Games.Single(g => g.Id == gameId).Name));
    }

    [Fact]
    public async Task WishlistGames_AppearOnTheWishlistOnly()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var name = ManageGamesFactory.Unique("Wished");

        Browser.AssertRedirect(await browser.AddGameAsync(name, onWishList: true), "/Games/Wishlist");

        Assert.Contains(name, await browser.GetPageAsync("/Games/Wishlist"));
        Assert.DoesNotContain(name, await browser.GetPageAsync("/Games"));
    }

    [Fact]
    public async Task AddGame_WithUnknownConsole_StoresTheGameWithoutConsole()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var name = ManageGamesFactory.Unique("No console");

        Browser.AssertRedirect(await browser.AddGameAsync(name, consoleId: 99999), "/Games");

        Assert.Null(factory.Query(db => db.Games.Single(g => g.Name == name).ConsoleId));
    }

    [Fact]
    public async Task AddGame_RejectsTooLongTitles_AndInvalidCopies()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var tooLong = new string('x', FieldLimits.NameMaxLength + 1);
        var noCopies = ManageGamesFactory.Unique("Zero copies");

        var longTitle = await browser.AddGameAsync(tooLong);
        var zeroCopies = await browser.AddGameAsync(noCopies, copies: 0);

        // The form is shown again with the error; nothing is stored.
        Assert.Equal(HttpStatusCode.OK, longTitle.StatusCode);
        Assert.Contains("maximum length", await longTitle.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, zeroCopies.StatusCode);
        Assert.Contains("must be between 1 and", await zeroCopies.Content.ReadAsStringAsync());
        Assert.False(factory.Query(db => db.Games.Any(g => g.Name == tooLong || g.Name == noCopies)));
    }
}
