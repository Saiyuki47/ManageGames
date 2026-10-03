using System.Collections.Concurrent;
using System.Net;
using System.Text;
using ManageGames.Services.Covers;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace ManageGames.Tests.Infrastructure;

/// <summary>Small images for the cover tests.</summary>
public static class TestImages
{
    /// <summary>A real 1 × 1 pixel PNG (a new copy each time).</summary>
    public static byte[] Png => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    /// <summary>Starts like a JPEG; that is all the format check looks at, but it can't be decoded.</summary>
    public static byte[] Jpeg => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

    /// <summary>A real, decodable image of the given size, filled with one color.</summary>
    public static byte[] Create(int width, int height, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.OrangeRed);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }
}

/// <summary>A cover source that answers from a table, so tests decide what each title finds.</summary>
public sealed class FakeCoverProvider(string name = FakeCoverProvider.DefaultName) : ICoverProvider
{
    public const string DefaultName = "Fake";

    private readonly ConcurrentDictionary<string, IReadOnlyList<CoverCandidate>> _results = new();
    private readonly ConcurrentDictionary<string, Exception> _failures = new();

    public string Name => name;

    public bool IsConfigured { get; set; } = true;

    /// <summary>The image server of this source.</summary>
    public string ImageHost => $"images.{name.ToLowerInvariant()}.test";

    public ConcurrentQueue<string> Searches { get; } = new();

    /// <summary>Like ScreenScraper's credentials: added to image addresses only for the download.</summary>
    public string? DownloadKey { get; set; }

    public int MaxParallelDownloads { get; set; } = 8;

    public Uri GetDownloadUrl(Uri imageUrl)
    {
        return DownloadKey == null ? imageUrl : new Uri($"{imageUrl}?key={DownloadKey}");
    }

    public bool IsOwnImage(Uri url)
    {
        return url.Scheme == Uri.UriSchemeHttps && url.Host == ImageHost;
    }

    public void Returns(string title, params CoverCandidate[] candidates)
    {
        _results[title] = candidates;
    }

    public void Fails(string title, Exception exception)
    {
        _failures[title] = exception;
    }

    public Task<IReadOnlyList<CoverCandidate>> SearchAsync(string title, CancellationToken cancellationToken)
    {
        Searches.Enqueue(title);
        return _failures.TryGetValue(title, out var exception)
            ? Task.FromException<IReadOnlyList<CoverCandidate>>(exception)
            : Task.FromResult(_results.GetValueOrDefault(title, []));
    }

    public CoverCandidate Candidate(string title, string image = "cover.png", params string[] platforms)
    {
        return new CoverCandidate(Name, title, [], platforms, [], 2017,
            new Uri($"https://{ImageHost}/{image}"), new Uri($"https://{ImageHost}/thumb/{image}"));
    }

    public CoverCandidate RegionalCandidate(string title, string region, string image)
    {
        return Candidate(title, image) with { Region = region };
    }
}

public sealed record RecordedRequest(HttpMethod Method, Uri Url, string? Authorization, IReadOnlyDictionary<string, string> Headers, string? Body);

/// <summary>
/// Stands in for the internet: every HTTP request of the app's clients is answered by <see cref="Respond"/>
/// and recorded.
/// </summary>
public sealed class FakeWeb(Func<HttpRequestMessage, HttpResponseMessage> respond)
{
    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = respond;

    public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

    /// <summary>How long every answer takes, like a real server.</summary>
    public TimeSpan Delay { get; set; }

    /// <summary>The most requests that were in progress at the same time.</summary>
    public int MostConcurrentRequests => _mostConcurrentRequests;

    private int _concurrentRequests;
    private int _mostConcurrentRequests;

    public IHttpClientFactory ClientFactory => new ClientFactoryForWeb(this);

    public void ResetConcurrency()
    {
        _mostConcurrentRequests = 0;
    }

    public HttpMessageHandler CreateHandler()
    {
        return new Handler(this);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    public static HttpResponseMessage File(byte[] data, string contentType = "application/octet-stream")
    {
        var content = new ByteArrayContent(data);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    public static HttpResponseMessage Status(HttpStatusCode status)
    {
        return new HttpResponseMessage(status);
    }

    private sealed class Handler(FakeWeb web) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);
            web.Requests.Enqueue(new RecordedRequest(request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), headers, body));
            var now = Interlocked.Increment(ref web._concurrentRequests);
            try
            {
                for (var most = Volatile.Read(ref web._mostConcurrentRequests); now > most; most = Volatile.Read(ref web._mostConcurrentRequests))
                {
                    Interlocked.CompareExchange(ref web._mostConcurrentRequests, now, most);
                }
                if (web.Delay > TimeSpan.Zero)
                {
                    await Task.Delay(web.Delay, cancellationToken);
                }
                return web.Respond(request);
            }
            finally
            {
                Interlocked.Decrement(ref web._concurrentRequests);
            }
        }
    }

    private sealed class ClientFactoryForWeb(FakeWeb web) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(web.CreateHandler());
        }
    }
}

/// <summary>Fixed settings for services created by hand in unit tests.</summary>
public sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue { get; set; } = value;

    public T Get(string? name)
    {
        return CurrentValue;
    }

    public IDisposable? OnChange(Action<T, string?> listener)
    {
        return null;
    }
}
