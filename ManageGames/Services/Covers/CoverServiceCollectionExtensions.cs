using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ManageGames.Services.Covers;

public static class CoverServiceCollectionExtensions
{
    /// <summary>The cover search: settings, HTTP clients, the sources and the services using them.</summary>
    public static IServiceCollection AddCoverSearch(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CoverOptions>(configuration.GetSection(CoverOptions.SectionName));
        services.AddHttpClient(CoverHttpClients.Api, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(CoverHttpClients.UserAgent);
        });
        services.AddHttpClient(CoverHttpClients.Images, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(CoverHttpClients.UserAgent);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });

        services.TryAddSingleton(TimeProvider.System);
        // Singletons: IGDB keeps its access token between requests.
        services.AddSingleton<ICoverProvider, IgdbCoverProvider>();
        services.AddSingleton<ICoverProvider, SteamGridDbCoverProvider>();
        services.AddSingleton<CoverScraper>();
        services.AddScoped<CoverService>();
        return services;
    }
}
