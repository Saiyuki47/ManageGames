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
            "game_localizations": [
              { "id": 3, "name": "ゼルダの伝説 ブレス オブ ザ ワイルド", "region": { "id": 3, "identifier": "ja-JP" }, "cover": { "id": 5, "image_id": "co3p2d" } },
              { "id": 4, "region": { "id": 4, "identifier": "EU" }, "cover": { "id": 6, "image_id": "coeu01" } },
              { "id": 6, "region": { "id": 2, "identifier": "ko-KR" }, "cover": { "id": 7, "image_id": "../kr" } },
              { "id": 7, "region": { "id": 2, "identifier": "ko-KR" } }
            ]
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
        Assert.Contains("game_localizations.cover.image_id", requests[1].Body);

        // The main cover and the European one; the Japanese one is the same image, the Korean ones are unusable.
        Assert.Equal(2, candidates.Count);
        var zelda = candidates[0];
        Assert.Equal(IgdbCoverProvider.ProviderName, zelda.Provider);
        Assert.Equal("The Legend of Zelda: Breath of the Wild", zelda.Title);
        Assert.Equal(["Zelda BotW", "ゼルダの伝説 ブレス オブ ザ ワイルド"], zelda.AlternativeTitles);
        Assert.Equal(["Nintendo Switch", "Wii U"], zelda.Platforms);
        Assert.Equal(["Switch", "NX", "WiiU"], zelda.PlatformAliases);
        Assert.Equal(2017, zelda.Year);
        Assert.Equal("https://images.igdb.com/igdb/image/upload/t_cover_big_2x/co3p2d.jpg", zelda.ImageUrl.ToString());
        Assert.Equal("https://images.igdb.com/igdb/image/upload/t_cover_big/co3p2d.jpg", zelda.PreviewUrl.ToString());
        Assert.Null(zelda.Region);
        Assert.True(provider.IsOwnImage(zelda.ImageUrl));

        var european = candidates[1];
        Assert.Equal(("eu", zelda.Title, zelda.Platforms), (european.Region, european.Title, european.Platforms));
        Assert.Equal("https://images.igdb.com/igdb/image/upload/t_cover_big_2x/coeu01.jpg", european.ImageUrl.ToString());
        Assert.Equal("https://images.igdb.com/igdb/image/upload/t_cover_big/coeu01.jpg", european.PreviewUrl.ToString());
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

        Assert.NotEmpty(candidates);
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
    public void Downloads_UseTheImageAddressAsIs_SeveralAtOnce()
    {
        using var provider = CreateProvider(new FakeWeb(_ => FakeWeb.Status(HttpStatusCode.NotFound)));
        ICoverProvider source = provider;
        var image = new Uri("https://images.igdb.com/igdb/image/upload/t_cover_big/abc.jpg");

        Assert.Same(image, source.GetDownloadUrl(image));
        Assert.Equal(8, source.MaxParallelDownloads);
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

public class ScreenScraperCoverProviderTests
{
    private const string SystemsJson = """
        {"header": {"success": "true"}, "response": {"systemes": [
          {"id": 15, "noms": {"nom_eu": "Nintendo DS", "nom_recalbox": "nds", "noms_commun": "Nintendo DS, NDS, DS"}, "compagnie": "Nintendo"},
          {"id": 225, "noms": {"nom_eu": "Switch"}}
        ]}}
        """;

    // The shape of ScreenScraper's answer to jeuRecherche.php with output=json; numbers come as strings.
    private const string SearchJson = """
        {"header": {"success": "true"}, "response": {"jeux": [
          {
            "id": "1234",
            "noms": [
              {"region": "ss", "text": "Zelda : Spirit Tracks"},
              {"region": "us", "text": "The Legend of Zelda: Spirit Tracks"},
              {"region": "de", "text": "The Legend of Zelda: Spirit Tracks (DE)"}
            ],
            "systeme": {"id": "15", "text": "NDS"},
            "dates": [{"region": "us", "text": "2009-12-07"}, {"region": "jp", "text": "2009-12-23"}],
            "medias": [
              {"type": "box-2D", "region": "us", "url": "https://neoclone.screenscraper.fr/api2/mediaJeu.php?devid=x&devpassword=y", "format": "png"},
              {"type": "wheel", "region": "eu"},
              {"type": "box-2D", "region": "eu"},
              {"type": "box-2D", "region": "de"},
              {"type": "box-2D", "region": "de"},
              {"type": "box-2D", "region": "../x"},
              {"type": "box-2D-back", "region": "fr"}
            ]
          },
          {"id": "77", "noms": [{"region": "wor", "text": "No box"}], "systeme": {"id": "15", "text": "NDS"}, "medias": [{"type": "wheel", "region": "wor"}]},
          {"id": "abc", "noms": [{"region": "wor", "text": "Bad id"}], "systeme": {"id": "15"}, "medias": [{"type": "box-2D", "region": "wor"}]},
          {}
        ]}}
        """;

    [Fact]
    public async Task SearchAsync_ReturnsTheBoxFrontsOfEachRegion_PreferredOnesFirst()
    {
        var web = new FakeWeb(Answer);
        using var provider = CreateProvider(web);

        var candidates = await provider.SearchAsync("Zelda Spirit Tracks", TestContext.Current.CancellationToken);

        Assert.Equal(["de", "eu", "us"], candidates.Select(c => c.Region));
        var german = candidates[0];
        Assert.Equal((ScreenScraperCoverProvider.ProviderName, "The Legend of Zelda: Spirit Tracks (DE)", 2009), (german.Provider, german.Title, german.Year));
        Assert.Equal(["Zelda : Spirit Tracks", "The Legend of Zelda: Spirit Tracks"], german.AlternativeTitles);
        Assert.Equal(["NDS"], german.Platforms);
        Assert.Equal(["Nintendo DS", "nds", "DS"], german.PlatformAliases);
        Assert.Equal(
            "https://api.screenscraper.fr/api2/mediaJeu.php?systemeid=15&jeuid=1234&media=box-2D(de)&maxwidth=640&maxheight=900&outputformat=jpg",
            german.ImageUrl.ToString());
        Assert.Contains("maxwidth=264", german.PreviewUrl.ToString(), StringComparison.Ordinal);
        Assert.All(candidates, c => Assert.True(provider.IsOwnImage(c.ImageUrl) && provider.IsOwnImage(c.PreviewUrl)));
        // The credentials are only added when downloading.
        Assert.All(candidates, c => Assert.DoesNotContain("dev", c.ImageUrl.Query, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SearchAsync_SendsTheCredentials_AndTheUserAccountIfSet()
    {
        var web = new FakeWeb(Answer);
        using var withoutAccount = CreateProvider(web);
        await withoutAccount.SearchAsync("Zelda & Link", TestContext.Current.CancellationToken);
        var search = web.Requests.Last().Url;

        Assert.Equal("/api2/jeuRecherche.php", search.AbsolutePath);
        Assert.Contains("devid=dev&devpassword=p%26ss&softname=ManageGames", search.Query, StringComparison.Ordinal);
        Assert.Contains("output=json&recherche=Zelda%20%26%20Link", search.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("ssid", search.Query, StringComparison.Ordinal);

        using var withAccount = CreateProvider(web, username: "me", password: "secret");
        await withAccount.SearchAsync("Zelda", TestContext.Current.CancellationToken);
        Assert.Contains("&ssid=me&sspassword=secret", web.Requests.Last().Url.Query, StringComparison.Ordinal);
    }

    [Fact]
    public void GetDownloadUrl_AddsTheCredentials()
    {
        using var provider = CreateProvider(new FakeWeb(Answer), username: "me", password: "secret");
        var image = new Uri("https://api.screenscraper.fr/api2/mediaJeu.php?systemeid=15&jeuid=1&media=box-2D(eu)");

        Assert.Equal(
            "https://api.screenscraper.fr/api2/mediaJeu.php?systemeid=15&jeuid=1&media=box-2D(eu)&devid=dev&devpassword=p%26ss&softname=ManageGames&ssid=me&sspassword=secret",
            provider.GetDownloadUrl(image).AbsoluteUri);
        Assert.Equal(1, ((ICoverProvider)provider).MaxParallelDownloads);
    }

    [Fact]
    public async Task SearchAsync_LoadsTheSystemsOnce()
    {
        var web = new FakeWeb(Answer);
        using var provider = CreateProvider(web);

        await provider.SearchAsync("a", TestContext.Current.CancellationToken);
        await provider.SearchAsync("b", TestContext.Current.CancellationToken);

        Assert.Equal(1, web.Requests.Count(r => r.Url.AbsolutePath.EndsWith("systemesListe.php", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task SearchAsync_StillWorks_WhenTheSystemsCannotBeLoaded_AndTriesAgainLater()
    {
        var systemsDown = true;
        var web = new FakeWeb(request => systemsDown && request.RequestUri!.AbsolutePath.EndsWith("systemesListe.php", StringComparison.Ordinal)
            ? FakeWeb.Status(System.Net.HttpStatusCode.InternalServerError)
            : Answer(request));
        using var provider = CreateProvider(web);

        var first = await provider.SearchAsync("a", TestContext.Current.CancellationToken);
        systemsDown = false;
        var second = await provider.SearchAsync("b", TestContext.Current.CancellationToken);

        Assert.Empty(first[0].PlatformAliases);
        Assert.NotEmpty(second[0].PlatformAliases);
    }

    [Theory]
    [InlineData("""{"response": {"jeux": [{}]}}""")]
    [InlineData("""{"response": {}}""")]
    [InlineData("""{"header": {}}""")]
    public async Task SearchAsync_FindsNothing_InEmptyAnswers(string json)
    {
        var web = new FakeWeb(request => request.RequestUri!.AbsolutePath.EndsWith("jeuRecherche.php", StringComparison.Ordinal) ? FakeWeb.Json(json) : Answer(request));
        using var provider = CreateProvider(web);

        Assert.Empty(await provider.SearchAsync("Unknown", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchAsync_Fails_OnErrorsAndPlainTextAnswers()
    {
        // ScreenScraper reports wrong credentials or a used-up quota as plain text.
        using var forbidden = CreateProvider(new FakeWeb(_ => FakeWeb.Status(System.Net.HttpStatusCode.Forbidden)));
        using var text = CreateProvider(new FakeWeb(_ => FakeWeb.File("Erreur de login : Vérifier vos identifiants développeur !  "u8.ToArray(), "text/html")));
        using var broken = CreateProvider(new FakeWeb(_ => FakeWeb.Json("{ \"response\": ")));

        await Assert.ThrowsAsync<HttpRequestException>(() => forbidden.SearchAsync("Zelda", TestContext.Current.CancellationToken));
        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => text.SearchAsync("Zelda", TestContext.Current.CancellationToken));
        Assert.Equal("ScreenScraper refused the request: Erreur de login : Vérifier vos identifiants développeur !", refused.Message);
        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => broken.SearchAsync("Zelda", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("https://api.screenscraper.fr/api2/mediaJeu.php?systemeid=1&jeuid=2&media=box-2D(eu)", true)]
    [InlineData("https://API.SCREENSCRAPER.FR/api2/mediaJeu.php?x=1", true)]
    [InlineData("https://api.screenscraper.fr/api2/jeuInfos.php?x=1", false)]
    [InlineData("http://api.screenscraper.fr/api2/mediaJeu.php", false)]
    [InlineData("https://neoclone.screenscraper.fr/api2/mediaJeu.php", false)]
    [InlineData("https://api.screenscraper.fr.evil.test/api2/mediaJeu.php", false)]
    public void IsOwnImage_OnlyAcceptsTheMediaDownload(string url, bool expected)
    {
        using var provider = CreateProvider(new FakeWeb(Answer));

        Assert.Equal(expected, provider.IsOwnImage(new Uri(url)));
    }

    [Fact]
    public void IsConfigured_NeedsTheDeveloperCredentials()
    {
        var web = new FakeWeb(Answer);
        using var withoutPassword = CreateProvider(web, devPassword: "");
        using var complete = CreateProvider(web);

        Assert.False(withoutPassword.IsConfigured);
        Assert.True(complete.IsConfigured);
    }

    private static HttpResponseMessage Answer(HttpRequestMessage request)
    {
        return request.RequestUri!.AbsolutePath.EndsWith("systemesListe.php", StringComparison.Ordinal)
            ? FakeWeb.Json(SystemsJson)
            : FakeWeb.Json(SearchJson);
    }

    private static ScreenScraperCoverProvider CreateProvider(FakeWeb web, string devPassword = "p&ss", string? username = null, string? password = null)
    {
        var options = new CoverOptions
        {
            Regions = "de,eu",
            ScreenScraper = { DevId = "dev", DevPassword = devPassword, Username = username, Password = password },
        };
        return new ScreenScraperCoverProvider(web.ClientFactory, new TestOptionsMonitor<CoverOptions>(options));
    }
}
