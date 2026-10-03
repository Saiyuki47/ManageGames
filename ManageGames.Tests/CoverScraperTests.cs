using System.Net;
using ManageGames.Services.Covers;
using ManageGames.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace ManageGames.Tests;

public class CoverScraperTests
{
    private readonly FakeCoverProvider _first = new("First");
    private readonly FakeCoverProvider _second = new("Second");
    private readonly FakeWeb _web;

    public CoverScraperTests()
    {
        _web = new FakeWeb(request => request.RequestUri!.AbsolutePath.Contains("missing", StringComparison.Ordinal)
            ? FakeWeb.Status(HttpStatusCode.NotFound)
            : FakeWeb.File(TestImages.Png));
    }

    [Fact]
    public void GetActiveProviders_FollowsTheConfiguredOrder_AndSkipsSourcesWithoutKeys()
    {
        var third = new FakeCoverProvider("Third") { IsConfigured = false };

        var active = CreateScraper(" second , Unknown,FIRST,Third, first", third).GetActiveProviders();

        Assert.Equal(["Second", "First"], active.Select(p => p.Name));
    }

    [Fact]
    public async Task FindCoverAsync_FallsBackToTheNextSource_WhenTheFirstHasNoGoodMatch()
    {
        _first.Returns("Pikmin 4", _first.Candidate("Pikmin 3 Deluxe"));
        _second.Returns("Pikmin 4", _second.Candidate("Pikmin 4", "pikmin4.png"));

        var found = await CreateScraper().FindCoverAsync("Pikmin 4", null, TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.Equal(("Second", "Pikmin 4", "image/png"), (found.Provider, found.MatchedTitle, found.Image.ContentType));
        Assert.Equal($"https://{_second.ImageHost}/pikmin4.png", Assert.Single(_web.Requests).Url.ToString());
    }

    [Fact]
    public async Task FindCoverAsync_StopsAtTheFirstSourceWithAMatch()
    {
        _first.Returns("Kirby", _first.Candidate("Kirby"));

        var found = await CreateScraper().FindCoverAsync("Kirby", null, TestContext.Current.CancellationToken);

        Assert.Equal("First", found?.Provider);
        Assert.Empty(_second.Searches);
    }

    [Fact]
    public async Task FindCoverAsync_PrefersTheGamesConsole_AndRejectsOtherConsoles()
    {
        _first.Returns("Doom",
            _first.Candidate("Doom", "doom-pc.png", "PC (Microsoft Windows)"),
            _first.Candidate("Doom", "doom-switch.png", "Nintendo Switch", "PlayStation 4"));
        _first.Returns("Tetris", _first.Candidate("Tetris", "tetris-gb.png", "Game Boy"));

        var doom = await CreateScraper().FindCoverAsync("Doom", "Switch", TestContext.Current.CancellationToken);
        var tetris = await CreateScraper().FindCoverAsync("Tetris", "Game Boy Advance", TestContext.Current.CancellationToken);

        Assert.NotNull(doom);
        Assert.Equal($"https://{_first.ImageHost}/doom-switch.png", Assert.Single(_web.Requests).Url.ToString());
        Assert.Null(tetris);
    }

    [Fact]
    public async Task FindCoverAsync_PrefersTheMainTitle_OverAnAlternativeTitle()
    {
        // Both fit perfectly, the collection only through one of its other titles.
        var collection = _first.Candidate("Kirby's Dream Collection", "collection.png", "Wii") with { AlternativeTitles = ["Kirby's Adventure"] };
        _first.Returns("Kirby's Adventure", collection, _first.Candidate("Kirby's Adventure", "adventure.png", "Wii"));

        var found = await CreateScraper().FindCoverAsync("Kirby's Adventure", "Wii", TestContext.Current.CancellationToken);
        var search = await CreateScraper().SearchAsync("Kirby's Adventure", "Wii", TestContext.Current.CancellationToken);

        Assert.Equal("Kirby's Adventure", found?.MatchedTitle);
        Assert.Equal(["adventure.png", "collection.png"], search.Covers.Select(c => c.Candidate.ImageUrl.Segments[^1]));
    }

    [Fact]
    public async Task FindCoverAsync_DoesNotTakeAVariantForThePlainGame()
    {
        var plus = _first.Candidate("Wii Fit Plus", "wii-fit-plus.png", "Wii") with { AlternativeTitles = ["Wii Fit +"] };
        var squared = _first.Candidate("Pikmin²", "pikmin2.png", "Nintendo GameCube") with { AlternativeTitles = ["Pikmin ²", "Pikmin^2"] };
        _first.Returns("Wii Fit", plus);
        _first.Returns("Pikmin", squared);

        Assert.Null(await CreateScraper().FindCoverAsync("Wii Fit", "Wii", TestContext.Current.CancellationToken));
        Assert.Null(await CreateScraper().FindCoverAsync("Pikmin", "Nintendo GameCube", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FindCoverAsync_TriesTheNextMatch_WhenADownloadFails()
    {
        _first.Returns("Celeste", _first.Candidate("Celeste", "missing.png"), _first.Candidate("Celeste", "celeste.png"));

        var found = await CreateScraper().FindCoverAsync("Celeste", null, TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.Equal(2, _web.Requests.Count);
    }

    [Fact]
    public async Task FindCoverAsync_SkipsFailingSources()
    {
        _first.Fails("Hades", new HttpRequestException("down"));
        _second.Returns("Hades", _second.Candidate("Hades"));

        var found = await CreateScraper().FindCoverAsync("Hades", null, TestContext.Current.CancellationToken);

        Assert.Equal("Second", found?.Provider);
    }

    [Fact]
    public async Task FindCoverAsync_ReturnsNull_WhenNothingFits()
    {
        _first.Returns("Zelda", _first.Candidate("The Legend of Zelda: Tears of the Kingdom"));

        Assert.Null(await CreateScraper().FindCoverAsync("Zelda", null, TestContext.Current.CancellationToken));
        Assert.Empty(_web.Requests);
    }

    [Fact]
    public async Task SearchAsync_CombinesAllSources_BestMatchesFirst()
    {
        _first.Returns("Mario Kart 8 Deluxe",
            _first.Candidate("Mario Kart 8", "mk8.png", "Wii U"),
            _first.Candidate("Mario Kart 8 Deluxe", "mk8d.png", "Nintendo Switch"));
        _second.Returns("Mario Kart 8 Deluxe", _second.Candidate("Mario Kart 8 Deluxe", "mk8d-grid.png"));

        var result = await CreateScraper().SearchAsync("Mario Kart 8 Deluxe", "Nintendo Switch", TestContext.Current.CancellationToken);

        Assert.Equal(["mk8d.png", "mk8d-grid.png", "mk8.png"], result.Covers.Select(c => c.Candidate.ImageUrl.Segments[^1]));
        Assert.Equal([true, true, false], result.Covers.Select(c => c.IsConfident));
        Assert.Empty(result.FailedProviders);
    }

    [Fact]
    public async Task SearchAsync_ReportsFailingSources_AndKeepsTheOthersResults()
    {
        _first.Fails("Hollow Knight", new HttpRequestException("down"));
        _second.Returns("Hollow Knight", _second.Candidate("Hollow Knight"));

        var result = await CreateScraper().SearchAsync("Hollow Knight", null, TestContext.Current.CancellationToken);

        Assert.Equal(["First"], result.FailedProviders);
        Assert.Single(result.Covers);
    }

    [Fact]
    public async Task SearchAsync_TreatsTimeoutsAsFailures_ButNotCancellation()
    {
        _first.Fails("Slow", new TaskCanceledException("timeout"));

        var result = await CreateScraper().SearchAsync("Slow", null, TestContext.Current.CancellationToken);
        Assert.Equal(["First"], result.FailedProviders);

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateScraper().SearchAsync("Slow", null, cancelled.Token));
    }

    [Fact]
    public async Task DownloadAsync_ReturnsCheckedImages()
    {
        var image = await CreateScraper().DownloadAsync(new Uri($"https://{_first.ImageHost}/a.png"), TestContext.Current.CancellationToken);

        Assert.Equal(TestImages.Png, image?.Data);
    }

    [Fact]
    public async Task DownloadAsync_RejectsErrorsNonImagesAndTooLargeFiles()
    {
        var url = new Uri($"https://{_first.ImageHost}/a.png");
        var scraper = CreateScraper();

        _web.Respond = _ => FakeWeb.Status(HttpStatusCode.InternalServerError);
        Assert.Null(await scraper.DownloadAsync(url, TestContext.Current.CancellationToken));

        _web.Respond = _ => FakeWeb.File("<html></html>"u8.ToArray(), "image/png");
        Assert.Null(await scraper.DownloadAsync(url, TestContext.Current.CancellationToken));

        _web.Respond = _ => throw new HttpRequestException("unreachable");
        Assert.Null(await scraper.DownloadAsync(url, TestContext.Current.CancellationToken));

        _web.Respond = _ => throw new TaskCanceledException("timeout");
        Assert.Null(await scraper.DownloadAsync(url, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DownloadAsync_DoesNotReadFilesAnnouncedAsTooLarge()
    {
        var file = new CountingStream([.. TestImages.Png, .. new byte[CoverImage.MaxBytes]], canSeek: true);
        _web.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(file) };

        Assert.Null(await CreateScraper().DownloadAsync(new Uri($"https://{_first.ImageHost}/a.png"), TestContext.Current.CancellationToken));
        Assert.Equal(0, file.BytesRead);
    }

    [Fact]
    public async Task DownloadAsync_StopsReading_OnceAFileOfUnknownSizeIsTooLarge()
    {
        // Without a known length the response has no Content-Length, like a streamed download.
        var file = new CountingStream([.. TestImages.Png, .. new byte[3 * CoverImage.MaxBytes]], canSeek: false);
        _web.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(file) };

        Assert.Null(await CreateScraper().DownloadAsync(new Uri($"https://{_first.ImageHost}/a.png"), TestContext.Current.CancellationToken));
        Assert.InRange(file.BytesRead, CoverImage.MaxBytes, CoverImage.MaxBytes + (1024 * 1024));
    }

    [Fact]
    public async Task FindCoverAsync_PrefersTheConfiguredRegions()
    {
        _first.Returns("Celeste",
            _first.Candidate("Celeste", "main.png"),
            _first.RegionalCandidate("Celeste", "us", "us.png"),
            _first.RegionalCandidate("Celeste", "eu", "eu.png"),
            _first.RegionalCandidate("Celeste", "de", "de.png"));

        var found = await CreateScraper().FindCoverAsync("Celeste", null, TestContext.Current.CancellationToken);

        Assert.Equal("de", found?.Region);
        Assert.Equal("/de.png", Assert.Single(_web.Requests).Url.AbsolutePath);
    }

    [Fact]
    public async Task FindCoverAsync_WithPreferredRegionOnly_SkipsCoversOfOtherRegions()
    {
        _first.Returns("Celeste", _first.Candidate("Celeste", "main.png"), _first.RegionalCandidate("Celeste", "us", "us.png"));
        _second.Returns("Celeste", _second.RegionalCandidate("Celeste", "eu", "eu.png"));

        var any = await CreateScraper().FindCoverAsync("Celeste", null, TestContext.Current.CancellationToken);
        var preferred = await CreateScraper().FindCoverAsync("Celeste", null, preferredRegionOnly: true, TestContext.Current.CancellationToken);

        Assert.Equal(("First", (string?)null), (any?.Provider, any?.Region));
        Assert.Equal(("Second", "eu"), (preferred?.Provider, preferred?.Region));
    }

    [Fact]
    public async Task SearchAsync_ListsThePreferredRegionsFirst()
    {
        _first.Returns("Celeste",
            _first.RegionalCandidate("Celeste", "us", "us.png"),
            _first.Candidate("Celeste", "main.png"),
            _first.RegionalCandidate("Celeste", "eu", "eu.png"));

        var result = await CreateScraper().SearchAsync("Celeste", null, TestContext.Current.CancellationToken);

        Assert.Equal(["eu.png", "us.png", "main.png"], result.Covers.Select(c => c.Candidate.ImageUrl.Segments[^1]));
    }

    [Fact]
    public void IsPreferredRegion_FollowsTheSetting()
    {
        var scraper = CreateScraper();

        Assert.True(scraper.IsPreferredRegion("de"));
        Assert.True(scraper.IsPreferredRegion("eu"));
        Assert.False(scraper.IsPreferredRegion("us"));
        Assert.False(scraper.IsPreferredRegion(null));
    }

    [Fact]
    public async Task DownloadAsync_AddsTheSourcesCredentials_OnlyToTheRequest()
    {
        _first.DownloadKey = "secret";

        var image = await CreateScraper().DownloadAsync(new Uri($"https://{_first.ImageHost}/a.png"), TestContext.Current.CancellationToken);

        Assert.NotNull(image);
        Assert.Equal("?key=secret", Assert.Single(_web.Requests).Url.Query);
    }

    [Fact]
    public async Task DownloadAsync_KeepsToTheSourcesParallelLimit()
    {
        _web.Delay = TimeSpan.FromMilliseconds(100);
        _first.MaxParallelDownloads = 1;
        _second.MaxParallelDownloads = 4;
        var scraper = CreateScraper();

        Task DownloadThreeAsync(FakeCoverProvider provider)
        {
            return Task.WhenAll(Enumerable.Range(0, 3).Select(i => Task.Run(() =>
                scraper.DownloadAsync(new Uri($"https://{provider.ImageHost}/{i}.png"), TestContext.Current.CancellationToken))));
        }

        await DownloadThreeAsync(_first);
        Assert.Equal(1, _web.MostConcurrentRequests);
        _web.ResetConcurrency();
        await DownloadThreeAsync(_second);
        Assert.True(_web.MostConcurrentRequests > 1, $"{_web.MostConcurrentRequests} at once");
    }

    [Fact]
    public void IsKnownImage_And_FindProvider_OnlyKnowActiveSources()
    {
        _second.IsConfigured = false;
        var scraper = CreateScraper();

        Assert.True(scraper.IsKnownImage(new Uri($"https://{_first.ImageHost}/a.png")));
        Assert.False(scraper.IsKnownImage(new Uri($"https://{_second.ImageHost}/a.png")));
        Assert.Same(_first, scraper.FindProvider("first"));
        Assert.Null(scraper.FindProvider("Second"));
        Assert.Null(scraper.FindProvider(null));
    }

    private CoverScraper CreateScraper(string order = "First,Second", params FakeCoverProvider[] more)
    {
        var options = new CoverOptions { Providers = order };
        return new CoverScraper([_first, _second, .. more], _web.ClientFactory, new TestOptionsMonitor<CoverOptions>(options), NullLogger<CoverScraper>.Instance);
    }

    // MemoryStream's asynchronous reads go through Read(Span), so counting the synchronous ones is enough.
    private sealed class CountingStream(byte[] data, bool canSeek) : MemoryStream(data)
    {
        public long BytesRead { get; private set; }

        public override bool CanSeek => canSeek;

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = base.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = base.Read(buffer);
            BytesRead += read;
            return read;
        }
    }
}
