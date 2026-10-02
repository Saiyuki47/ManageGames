using ManageGames.Services.Covers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ManageGames.Tests.Infrastructure;

/// <summary>
/// The app with a fake cover source (<see cref="Provider"/>) as its only one, and a fake image server that
/// serves a PNG for every address of that source.
/// </summary>
public class CoverFactory : ManageGamesFactory
{
    public FakeCoverProvider Provider { get; } = new();

    public FakeWeb Web { get; }

    public CoverFactory()
    {
        Web = new FakeWeb(request => request.RequestUri!.Host == Provider.ImageHost
            ? FakeWeb.File(TestImages.Png, "image/png")
            : FakeWeb.Status(System.Net.HttpStatusCode.NotFound));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Covers:Providers"] = Provider.Name,
        }));
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ICoverProvider>(Provider);
            services.AddHttpClient(CoverHttpClients.Images).ConfigurePrimaryHttpMessageHandler(Web.CreateHandler);
        });
    }
}
