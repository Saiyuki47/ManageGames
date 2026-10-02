using System.Net;
using ManageGames.Models;
using ManageGames.Services;
using ManageGames.Tests.Infrastructure;

namespace ManageGames.Tests;

public class CoverTests(CoverFactory factory) : IClassFixture<CoverFactory>
{
    private FakeCoverProvider Provider => factory.Provider;

    [Fact]
    public async Task AddGame_FindsTheCover_AndTheListShowsIt()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var name = ManageGamesFactory.Unique("Odyssey");
        Provider.Returns(name, Provider.Candidate(name));

        Browser.AssertRedirect(await browser.AddGameAsync(name), "/Games");

        var gameId = factory.GameId(name);
        var cover = Cover(gameId);
        Assert.NotNull(cover);
        Assert.Equal((FakeCoverProvider.DefaultName, "image/png"), (cover.Source, cover.ContentType));
        Assert.Contains($"/Covers/Image/{gameId}?v={cover.Version:N}", await browser.GetPageAsync("/Games"));

        var image = await browser.GetAsync($"/Covers/Image/{gameId}?v={cover.Version:N}");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TestImages.Png, await image.Content.ReadAsByteArrayAsync());
        var caching = image.Headers.CacheControl!;
        Assert.True(caching.Private);
        Assert.Equal(TimeSpan.FromDays(365), caching.MaxAge);
        Assert.Contains(caching.Extensions, e => e.Name == "immutable");
    }

    [Fact]
    public async Task CoverImage_WithoutTheCurrentVersion_IsRevalidated()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var gameId = await AddGameWithCoverAsync(browser);

        var image = await browser.GetAsync($"/Covers/Image/{gameId}?v=outdated");

        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.True(image.Headers.CacheControl is { Private: true, NoCache: true, MaxAge: null });
        Assert.Equal($"\"{Cover(gameId)!.Version:N}\"", image.Headers.ETag?.Tag);
    }

    [Fact]
    public async Task AddGame_StoresNoCover_WithoutAGoodMatch()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var name = ManageGamesFactory.Unique("Lonely");
        Provider.Returns(name, Provider.Candidate("Something completely different"));

        Browser.AssertRedirect(await browser.AddGameAsync(name), "/Games");

        Assert.Null(Cover(factory.GameId(name)));
        Assert.Contains("cover-placeholder", await browser.GetPageAsync("/Games"));
    }

    [Fact]
    public async Task AddGame_IgnoresCoversForAnotherConsole()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var name = ManageGamesFactory.Unique("Tetris");
        Provider.Returns(name, Provider.Candidate(name, "tetris.png", "Game Boy"));

        await browser.AddGameAsync(name, consoleId: ConsoleId("Nintendo Switch"));

        Assert.Null(Cover(factory.GameId(name)));
    }

    [Fact]
    public async Task AddGame_StillSavesTheGame_WhenTheSourceFails()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var name = ManageGamesFactory.Unique("Offline");
        Provider.Fails(name, new HttpRequestException("down"));

        Browser.AssertRedirect(await browser.AddGameAsync(name), "/Games");

        Assert.Null(Cover(factory.GameId(name)));
    }

    [Fact]
    public async Task EditGame_SearchesAgain_WhenTheTitleChanges()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var name = ManageGamesFactory.Unique("Typo");
        await browser.AddGameAsync(name);
        var gameId = factory.GameId(name);
        var corrected = ManageGamesFactory.Unique("Fixed");
        Provider.Returns(corrected, Provider.Candidate(corrected));

        Browser.AssertRedirect(await SaveGameAsync(browser, gameId, name), "/Games");
        // Unchanged title: no new search.
        Assert.Null(Cover(gameId));
        Assert.Equal(1, Provider.Searches.Count(s => s == name));

        Browser.AssertRedirect(await SaveGameAsync(browser, gameId, corrected), "/Games");
        Assert.NotNull(Cover(gameId));
    }

    [Fact]
    public async Task EditPage_ShowsTheCoverAndItsSource()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var gameId = await AddGameWithCoverAsync(browser);

        var page = await browser.GetPageAsync($"/Games/Edit/{gameId}");

        Assert.Contains($"/Covers/Image/{gameId}?v=", page);
        Assert.Contains($"Cover from {FakeCoverProvider.DefaultName}.", page);
        Assert.Contains("Remove cover", page);
    }

    [Fact]
    public async Task CoverPicker_ShowsTheResults_AndStoresTheChosenOne()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var name = ManageGamesFactory.Unique("Metroid");
        await browser.AddGameAsync(name);
        var gameId = factory.GameId(name);
        var other = ManageGamesFactory.Unique("Prime");
        Provider.Returns(other, Provider.Candidate(other, "first.png", "Wii"), Provider.Candidate(other, "second.png"));

        var page = await browser.GetPageAsync($"/Covers/Edit/{gameId}?query={other}");
        Assert.Contains($"value=\"{other}\"", page);
        Assert.Contains(WebUtility.HtmlEncode($"/Covers/Preview?url={Uri.EscapeDataString($"https://{Provider.ImageHost}/thumb/first.png")}"), page);
        Assert.Contains($"https://{Provider.ImageHost}/second.png", page);
        // Razor encodes the middle dot.
        Assert.Contains("2017 &#xB7; Wii", page);

        var choose = await browser.SubmitFormAsync($"/Covers/Edit/{gameId}", $"/Covers/Choose/{gameId}", new Dictionary<string, string>
        {
            ["provider"] = Provider.Name,
            ["imageUrl"] = $"https://{Provider.ImageHost}/second.png",
        });

        Browser.AssertRedirect(choose, $"/Games/Edit/{gameId}");
        Assert.Equal(FakeCoverProvider.DefaultName, Cover(gameId)?.Source);
        Assert.Contains(factory.Web.Requests, r => r.Url.AbsolutePath == "/second.png");
    }

    [Fact]
    public async Task CoverPicker_SearchesForTheTitle_ByDefault()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var name = ManageGamesFactory.Unique("Default");
        await browser.AddGameAsync(name);

        var page = await browser.GetPageAsync($"/Covers/Edit/{factory.GameId(name)}");

        Assert.Contains($"value=\"{name}\"", page);
        Assert.Contains("No covers found", page);
        Assert.Equal(2, Provider.Searches.Count(s => s == name));
    }

    [Fact]
    public async Task CoverPicker_ReportsFailingSources()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var name = ManageGamesFactory.Unique("Down");
        await browser.AddGameAsync(name);
        Provider.Fails(name, new HttpRequestException("down"));

        var page = await browser.GetPageAsync($"/Covers/Edit/{factory.GameId(name)}");

        Assert.Contains($"{FakeCoverProvider.DefaultName} could not be reached", page);
    }

    [Fact]
    public async Task Choose_RefusesImagesFromAnywhereElse()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var name = ManageGamesFactory.Unique("Crafted");
        await browser.AddGameAsync(name);
        var gameId = factory.GameId(name);

        foreach (var (provider, url) in new[]
        {
            (Provider.Name, "https://internal.example.test/secret.png"),
            (Provider.Name, "http://localhost:5432/"),
            (Provider.Name, "not a url"),
            ("Unknown", $"https://{Provider.ImageHost}/a.png"),
        })
        {
            var response = await browser.SubmitFormAsync("/Games", $"/Covers/Choose/{gameId}", new Dictionary<string, string>
            {
                ["provider"] = provider,
                ["imageUrl"] = url,
            });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        Assert.Null(Cover(gameId));
        Assert.DoesNotContain(factory.Web.Requests, r => r.Url.Host != Provider.ImageHost);
    }

    [Fact]
    public async Task Choose_ReportsImagesThatCannotBeDownloaded()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var name = ManageGamesFactory.Unique("Gone");
        await browser.AddGameAsync(name);
        var gameId = factory.GameId(name);
        var previous = factory.Web.Respond;
        factory.Web.Respond = _ => FakeWeb.Status(HttpStatusCode.NotFound);
        try
        {
            var response = await browser.SubmitFormAsync("/Games", $"/Covers/Choose/{gameId}", new Dictionary<string, string>
            {
                ["provider"] = Provider.Name,
                ["imageUrl"] = $"https://{Provider.ImageHost}/gone.png",
            });

            Browser.AssertRedirect(response, $"/Covers/Edit/{gameId}");
            Assert.Null(Cover(gameId));
        }
        finally
        {
            factory.Web.Respond = previous;
        }
    }

    [Fact]
    public async Task Upload_StoresTheImage()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var name = ManageGamesFactory.Unique("Homebrew");
        await browser.AddGameAsync(name);
        var gameId = factory.GameId(name);

        var response = await browser.UploadFileAsync($"/Games/Edit/{gameId}", $"/Covers/Upload/{gameId}", TestImages.Jpeg, "photo.png");

        Browser.AssertRedirect(response, $"/Games/Edit/{gameId}");
        var cover = Cover(gameId);
        Assert.Equal((GameCover.UploadSource, "image/jpeg"), (cover?.Source, cover?.ContentType));
        Assert.Contains("Uploaded by you.", await browser.GetPageAsync($"/Games/Edit/{gameId}"));
    }

    [Fact]
    public async Task Upload_RejectsFilesThatAreNoImages()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var name = ManageGamesFactory.Unique("Script");
        await browser.AddGameAsync(name);
        var gameId = factory.GameId(name);

        var svg = await browser.UploadFileAsync($"/Games/Edit/{gameId}", $"/Covers/Upload/{gameId}",
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray(), "cover.png");
        var empty = await browser.UploadFileAsync($"/Games/Edit/{gameId}", $"/Covers/Upload/{gameId}", [], "empty.png");

        Browser.AssertRedirect(svg, $"/Games/Edit/{gameId}");
        Browser.AssertRedirect(empty, $"/Games/Edit/{gameId}");
        Assert.Null(Cover(gameId));
        Assert.Contains("Please choose a JPEG, PNG, WebP or GIF image", await browser.GetPageAsync($"/Games/Edit/{gameId}"));
    }

    [Fact]
    public async Task Upload_ReplacesAnExistingCover()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var gameId = await AddGameWithCoverAsync(browser);
        var before = Cover(gameId)!;

        await browser.UploadFileAsync($"/Games/Edit/{gameId}", $"/Covers/Upload/{gameId}", TestImages.Jpeg, "new.jpg");

        var after = Cover(gameId)!;
        Assert.NotEqual(before.Version, after.Version);
        Assert.Equal(TestImages.Jpeg, after.Data);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
    }

    [Fact]
    public async Task Remove_DeletesTheCover()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var gameId = await AddGameWithCoverAsync(browser);

        Browser.AssertRedirect(await browser.SubmitFormAsync($"/Games/Edit/{gameId}", $"/Covers/Remove/{gameId}"), $"/Games/Edit/{gameId}");

        Assert.Null(Cover(gameId));
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/Covers/Image/{gameId}")).StatusCode);
    }

    [Fact]
    public async Task DeletingTheGame_DeletesItsCover()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var gameId = await AddGameWithCoverAsync(browser);

        await browser.SubmitFormAsync("/Games", $"/Games/Delete/{gameId}");

        Assert.False(factory.Query(db => db.GameCovers.Any(c => c.GameId == gameId)));
    }

    [Fact]
    public async Task Covers_StayInvisibleAndUntouchable_ForOtherUsers()
    {
        var owner = await factory.SignInAsync(await factory.CreateUserAsync());
        var intruder = await factory.SignInAsync(await factory.CreateUserAsync());
        var gameId = await AddGameWithCoverAsync(owner);
        var version = Cover(gameId)!.Version;

        var image = await intruder.GetAsync($"/Covers/Image/{gameId}");
        var picker = await intruder.GetAsync($"/Covers/Edit/{gameId}");
        var choose = await intruder.SubmitFormAsync("/Games", $"/Covers/Choose/{gameId}", new Dictionary<string, string>
        {
            ["provider"] = Provider.Name,
            ["imageUrl"] = $"https://{Provider.ImageHost}/intruder.png",
        });
        var upload = await intruder.UploadFileAsync("/Games", $"/Covers/Upload/{gameId}", TestImages.Jpeg, "x.jpg");
        var remove = await intruder.SubmitFormAsync("/Games", $"/Covers/Remove/{gameId}");

        Assert.Equal(HttpStatusCode.NotFound, image.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, picker.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, choose.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
        Assert.Equal(version, Cover(gameId)?.Version);
        Assert.DoesNotContain(factory.Web.Requests, r => r.Url.AbsolutePath == "/intruder.png");
    }

    [Fact]
    public async Task Covers_NeedASignedInUser()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var gameId = await AddGameWithCoverAsync(browser);
        var anonymous = factory.CreateBrowser();

        Assert.True(Browser.IsRedirect(await anonymous.GetAsync($"/Covers/Image/{gameId}")));
        Assert.True(Browser.IsRedirect(await anonymous.GetAsync($"/Covers/Preview?url=https://{Provider.ImageHost}/a.png")));
    }

    [Fact]
    public async Task Preview_PassesThroughTheSourcesImagesOnly()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());

        var preview = await browser.GetAsync($"/Covers/Preview?url={Uri.EscapeDataString($"https://{Provider.ImageHost}/thumb/a.png")}");
        var foreign = await browser.GetAsync($"/Covers/Preview?url={Uri.EscapeDataString("https://internal.example.test/a.png")}");
        var missing = await browser.GetAsync("/Covers/Preview");

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal("image/png", preview.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TestImages.Png, await preview.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.DoesNotContain(factory.Web.Requests, r => r.Url.Host == "internal.example.test");
    }

    [Fact]
    public async Task Preview_AnswersNotFound_WhenTheImageIsGone()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var previous = factory.Web.Respond;
        factory.Web.Respond = _ => FakeWeb.Status(HttpStatusCode.NotFound);
        try
        {
            var preview = await browser.GetAsync($"/Covers/Preview?url={Uri.EscapeDataString($"https://{Provider.ImageHost}/gone.png")}");

            Assert.Equal(HttpStatusCode.NotFound, preview.StatusCode);
        }
        finally
        {
            factory.Web.Respond = previous;
        }
    }

    [Fact]
    public async Task FindMissingCovers_SearchesTheUsersGamesWithoutCover()
    {
        var user = await factory.CreateUserAsync();
        var other = await factory.CreateUserAsync();
        var browser = await factory.SignInAsync(user);
        var otherBrowser = await factory.SignInAsync(other);
        var withCover = await AddGameWithCoverAsync(browser);
        var coverVersion = Cover(withCover)!.Version;
        var first = ManageGamesFactory.Unique("Old one");
        var second = ManageGamesFactory.Unique("Old two");
        var othersGame = ManageGamesFactory.Unique("Not mine");
        await browser.AddGameAsync(first);
        await browser.AddGameAsync(second);
        await otherBrowser.AddGameAsync(othersGame);
        // Found now, e.g. because the API keys were added in the meantime.
        Provider.Returns(first, Provider.Candidate(first));
        Provider.Returns(othersGame, Provider.Candidate(othersGame));

        var succeeded = await factory.WithServiceAsync<CoverService, bool>(s => s.FindMissingCoversAsync(user.Username.ToUpperInvariant(), TimeSpan.Zero));

        Assert.True(succeeded);
        Assert.NotNull(Cover(factory.GameId(first)));
        Assert.Null(Cover(factory.GameId(second)));
        Assert.Null(Cover(factory.GameId(othersGame)));
        Assert.Equal(coverVersion, Cover(withCover)?.Version);

        // Without a username: everybody's games.
        Assert.True(await factory.WithServiceAsync<CoverService, bool>(s => s.FindMissingCoversAsync(null, TimeSpan.Zero)));
        Assert.NotNull(Cover(factory.GameId(othersGame)));
    }

    [Fact]
    public async Task FindMissingCovers_Fails_ForUnknownUsers()
    {
        Assert.False(await factory.WithServiceAsync<CoverService, bool>(s => s.FindMissingCoversAsync("nobody-" + Guid.NewGuid(), TimeSpan.Zero)));
    }

    private async Task<int> AddGameWithCoverAsync(HttpClient browser)
    {
        var name = ManageGamesFactory.Unique("Covered");
        Provider.Returns(name, Provider.Candidate(name));
        await browser.AddGameAsync(name);
        var gameId = factory.GameId(name);
        Assert.NotNull(Cover(gameId));
        return gameId;
    }

    private static Task<HttpResponseMessage> SaveGameAsync(HttpClient browser, int gameId, string name)
    {
        return browser.SubmitFormAsync($"/Games/Edit/{gameId}", $"/Games/Edit/{gameId}", new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Copies"] = "1",
            ["OnWishList"] = "false",
        });
    }

    private GameCover? Cover(int gameId)
    {
        return factory.Query(db => db.GameCovers.SingleOrDefault(c => c.GameId == gameId));
    }

    private int ConsoleId(string name)
    {
        return factory.Query(db => db.Consoles.Single(c => c.Name == name).Id);
    }
}

/// <summary>An installation without any API keys: everything works, only the search is unavailable.</summary>
public class CoverWithoutSourcesTests(ManageGamesFactory factory) : IClassFixture<ManageGamesFactory>
{
    [Fact]
    public async Task CoverPicker_ExplainsThatNoSourceIsSetUp()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var name = ManageGamesFactory.Unique("Unsearched");
        Browser.AssertRedirect(await browser.AddGameAsync(name), "/Games");

        var page = await browser.GetPageAsync($"/Covers/Edit/{factory.GameId(name)}");

        Assert.Contains("No cover source is set up yet", page);
        Assert.False(factory.Query(db => db.GameCovers.Any()));
    }

    [Fact]
    public async Task FindMissingCovers_Fails_WithoutSources()
    {
        Assert.False(await factory.WithServiceAsync<CoverService, bool>(s => s.FindMissingCoversAsync(null, TimeSpan.Zero)));
    }
}
