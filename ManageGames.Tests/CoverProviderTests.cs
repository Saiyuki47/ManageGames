using System.Net;
using ManageGames.Services.Covers;
using ManageGames.Tests.Infrastructure;
using Microsoft.Extensions.Time.Testing;

namespace ManageGames.Tests;

public class IgdbCoverProviderTests
{
    private const string TokenJson = """{"access_token":"token-1","expires_in":5000,"token_type":"bearer"}""";

    // The shape of an IGDB answer to the query of BuildQuery (https://api-docs.igdb.com/#game).
    private const string GamesJson = """
        [
          {
            "id": 7346,
            "name": "The Legend of Zelda: Breath of the Wild",
            "first_release_date": 1488499200,
            "cover": { "id": 1, "image_id": "co3p2d" },
            "platforms": [
              { "id": 130, "name": "Nintendo Switch", "abbreviation": "Switch", "alternative_name": "NX" },
              { "id": 41, "name": "Wii U", "abbreviation": "WiiU" }
            ],
            "alternative_names": [ { "id": 2, "name": "Zelda BotW" } ],
            "game_localizations": [ { "id": 3, "name": "ゼルダの伝説 ブレス オブ ザ ワイルド" } ]
          },
          { "id": 1, "name": "Broken image id", "cover": { "id": 4, "image_id": "../../evil" } },
          { "id": 2, "name": "No cover" }
        ]
        """;

    private readonly FakeTimeProvider _time = new();

    [Fact]
    public async Task SearchAsync_GetsATokenAndQueriesTheGames()
    {
        var web = new FakeWeb(request => request.RequestUri!.Host == "id.twitch.tv" ? FakeWeb.Json(TokenJson) : FakeWeb.Json(GamesJson));
        using var provider = CreateProvider(web);

        var candidates = await provider.SearchAsync("Zelda \"BotW\"", TestContext.Current.CancellationToken);

        var requests = web.Requests.ToList();
        Assert.Equal(2, requests.Count);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.Equal("https://id.twitch.tv/oauth2/token", requests[0].Url.ToString());
        Assert.Equal("client_id=client&client_secret=secret&grant_type=client_credentials", requests[0].Body);
        Assert.Equal("https://api.igdb.com/v4/games", requests[1].Url.ToString());
        Assert.Equal("client", requests[1].Headers["Client-ID"]);
        Assert.Equal("Bearer token-1", requests[1].Authorization);
        Assert.StartsWith("search \"Zelda BotW\"; fields name,", requests[1].Body);
        Assert.Contains("cover.image_id", requests[1].Body);

        var zelda = Assert.Single(candidates);
        Assert.Equal(IgdbCoverProvider.ProviderName, zelda.Provider);
        Assert.Equal("The Legend of Zelda: Breath of the Wild", zelda.Title);
        Assert.Equal(["Zelda BotW", "ゼルダの伝説 ブレス オブ ザ ワイルド"], zelda.AlternativeTitles);
        Assert.Equal(["Nintendo Switch", "Wii U"], zelda.Platforms);
        Assert.Equal(["Switch", "NX", "WiiU"], zelda.PlatformAliases);
        Assert.Equal(2017, zelda.Year);
        Assert.Equal("https://images.igdb.com/igdb/image/upload/t_cover_big_2x/co3p2d.jpg", zelda.ImageUrl.ToString());
        Assert.Equal("https://images.igdb.com/igdb/image/upload/t_cover_big/co3p2d.jpg", zelda.PreviewUrl.ToString());
        Assert.True(provider.IsOwnImage(zelda.ImageUrl));
    }

    [Fact]
    public async Task SearchAsync_ReusesTheTokenUntilItExpires()
    {
        var web = new FakeWeb(request => request.RequestUri!.Host == "id.twitch.tv" ? FakeWeb.Json(TokenJson) : FakeWeb.Json("[]"));
        using var provider = CreateProvider(web);

        await provider.SearchAsync("a", TestContext.Current.CancellationToken);
        await provider.SearchAsync("b", TestContext.Current.CancellationToken);
        Assert.Equal(1, TokenRequests(web));

        // Renewed a minute before it runs out.
        _time.Advance(TimeSpan.FromSeconds(5000 - 61));
        await provider.SearchAsync("c", TestContext.Current.CancellationToken);
        Assert.Equal(1, TokenRequests(web));
        _time.Advance(TimeSpan.FromSeconds(2));
        await provider.SearchAsync("d", TestContext.Current.CancellationToken);
        Assert.Equal(2, TokenRequests(web));
    }

    [Fact]
    public async Task SearchAsync_RenewsARejectedTokenOnce()
    {
        var gameRequests = 0;
        var web = new FakeWeb(request =>
        {
            if (request.RequestUri!.Host == "id.twitch.tv")
            {
                return FakeWeb.Json(TokenJson);
            }
            return ++gameRequests == 1 ? FakeWeb.Status(HttpStatusCode.Unauthorized) : FakeWeb.Json(GamesJson);
        });
        using var provider = CreateProvider(web);

        var candidates = await provider.SearchAsync("Zelda", TestContext.Current.CancellationToken);

        Assert.Single(candidates);
        Assert.Equal(2, TokenRequests(web));
    }

    [Fact]
    public async Task SearchAsync_Fails_WhenEvenANewTokenIsRejected()
    {
        var web = new FakeWeb(request => request.RequestUri!.Host == "id.twitch.tv" ? FakeWeb.Json(TokenJson) : FakeWeb.Status(HttpStatusCode.Unauthorized));
        using var provider = CreateProvider(web);

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.SearchAsync("Zelda", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchAsync_Fails_WithoutAccessToken()
    {
        var web = new FakeWeb(_ => FakeWeb.Json("""{"status":400,"message":"invalid client"}"""));
        using var provider = CreateProvider(web);

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.SearchAsync("Zelda", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void IsConfigured_NeedsClientIdAndSecret()
    {
        var web = new FakeWeb(_ => FakeWeb.Status(HttpStatusCode.NotFound));
        using var withoutSecret = CreateProvider(web, secret: " ");
        using var complete = CreateProvider(web);

        Assert.False(withoutSecret.IsConfigured);
        Assert.True(complete.IsConfigured);
    }

    [Theory]
    [InlineData("https://images.igdb.com/igdb/image/upload/t_cover_big/abc.jpg", true)]
    [InlineData("https://IMAGES.IGDB.COM/x.jpg", true)]
    [InlineData("http://images.igdb.com/x.jpg", false)]
    [InlineData("https://images.igdb.com:8443/x.jpg", false)]
    [InlineData("https://images.igdb.com.evil.test/x.jpg", false)]
    [InlineData("https://localhost/x.jpg", false)]
    public void IsOwnImage_OnlyAcceptsIgdbsImageServer(string url, bool expected)
    {
        using var provider = CreateProvider(new FakeWeb(_ => FakeWeb.Status(HttpStatusCode.NotFound)));

        Assert.Equal(expected, provider.IsOwnImage(new Uri(url)));
    }

    [Fact]
    public void BuildQuery_KeepsTheSearchTermInsideItsQuotes()
    {
        var query = IgdbCoverProvider.BuildQuery("  A \"quoted\\\" \n title ");

        Assert.StartsWith("search \"A quoted  title\"; ", query);
    }

    private IgdbCoverProvider CreateProvider(FakeWeb web, string secret = "secret")
    {
        var options = new CoverOptions { Igdb = { ClientId = "client", ClientSecret = secret } };
        return new IgdbCoverProvider(web.ClientFactory, new TestOptionsMonitor<CoverOptions>(options), _time);
    }

    private static int TokenRequests(FakeWeb web)
    {
        return web.Requests.Count(r => r.Url.Host == "id.twitch.tv");
    }
}

public class SteamGridDbCoverProviderTests
{
    [Fact]
    public async Task SearchAsync_LooksUpTheGamesAndTheirGrids()
    {
        var web = new FakeWeb(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/v2/search/autocomplete/Metroid%20Prime%20%20%20Remastered" => FakeWeb.Json("""
                {"success":true,"data":[
                  {"id":10,"name":"Metroid Prime Remastered","release_date":1676505600,"types":["steam"],"verified":true},
                  {"id":11,"name":"Metroid Prime","types":[],"verified":true}
                ]}
                """),
            "/api/v2/grids/game/10" => FakeWeb.Json("""
                {"success":true,"page":0,"total":2,"limit":8,"data":[
                  {"id":1,"score":3,"style":"alternate","width":600,"height":900,"url":"https://cdn2.steamgriddb.com/grid/a.png","thumb":"https://cdn2.steamgriddb.com/thumb/a.jpg"},
                  {"id":2,"url":"https://evil.test/grid/b.png","thumb":"https://cdn2.steamgriddb.com/thumb/b.jpg"}
                ]}
                """),
            "/api/v2/grids/game/11" => FakeWeb.Json("""{"success":false,"errors":["Game not found"]}""", HttpStatusCode.NotFound),
            _ => FakeWeb.Status(HttpStatusCode.InternalServerError),
        });
        var provider = CreateProvider(web);

        var candidates = await provider.SearchAsync("Metroid Prime / Remastered", TestContext.Current.CancellationToken);

        var grid = Assert.Single(candidates);
        Assert.Equal((SteamGridDbCoverProvider.ProviderName, "Metroid Prime Remastered", 2023), (grid.Provider, grid.Title, grid.Year));
        Assert.Empty(grid.Platforms);
        Assert.Equal("https://cdn2.steamgriddb.com/grid/a.png", grid.ImageUrl.ToString());
        Assert.Equal("https://cdn2.steamgriddb.com/thumb/a.jpg", grid.PreviewUrl.ToString());
        Assert.All(web.Requests, r => Assert.Equal("Bearer key", r.Authorization));
        Assert.Contains(web.Requests, r => r.Url.Query.Contains("dimensions=600x900", StringComparison.Ordinal) && r.Url.Query.Contains("nsfw=false", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SearchAsync_FindsNothing_WhenNoGameMatches()
    {
        var web = new FakeWeb(_ => FakeWeb.Json("""{"success":true,"data":[]}"""));
        var provider = CreateProvider(web);

        Assert.Empty(await provider.SearchAsync("Unknown", TestContext.Current.CancellationToken));
        Assert.Single(web.Requests);
    }

    [Fact]
    public async Task SearchAsync_Fails_OnServerErrors()
    {
        var provider = CreateProvider(new FakeWeb(_ => FakeWeb.Status(HttpStatusCode.Unauthorized)));

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.SearchAsync("Zelda", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("https://cdn2.steamgriddb.com/grid/a.png", true)]
    [InlineData("https://cdn.steamgriddb.com/grid/a.png", true)]
    [InlineData("http://cdn2.steamgriddb.com/grid/a.png", false)]
    [InlineData("https://steamgriddb.com.evil.test/a.png", false)]
    [InlineData("https://evilsteamgriddb.com/a.png", false)]
    public void IsOwnImage_OnlyAcceptsSteamGridDbServers(string url, bool expected)
    {
        Assert.Equal(expected, CreateProvider(new FakeWeb(_ => FakeWeb.Status(HttpStatusCode.NotFound))).IsOwnImage(new Uri(url)));
    }

    [Fact]
    public void IsConfigured_NeedsTheApiKey()
    {
        var web = new FakeWeb(_ => FakeWeb.Status(HttpStatusCode.NotFound));

        Assert.False(CreateProvider(web, apiKey: null).IsConfigured);
        Assert.True(CreateProvider(web).IsConfigured);
    }

    private static SteamGridDbCoverProvider CreateProvider(FakeWeb web, string? apiKey = "key")
    {
        var options = new CoverOptions { SteamGridDb = { ApiKey = apiKey } };
        return new SteamGridDbCoverProvider(web.ClientFactory, new TestOptionsMonitor<CoverOptions>(options));
    }
}
