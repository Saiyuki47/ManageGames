using System.Globalization;
using System.Net;
using ManageGames.Data;
using ManageGames.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ManageGames.Tests.Infrastructure;

public record TestUser(string Username, string Password);

/// <summary>
/// Hosts the real app in memory against its own throw-away PostgreSQL database (see
/// <see cref="TestDatabase"/>) and a known admin password.
/// </summary>
public class ManageGamesFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminUsername = "admin";
    // Test-only passwords; they meet the password policy (15+ characters, not guessable).
    public const string AdminPassword = "Blue-Ocean-Lantern-42";
    public const string UserPassword = "Quiet-Maple-Rocket-17";

    /// <summary>The database of this factory's app; dropped when the factory is disposed.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>Password of the seeded admin; null lets the app generate a one-time password.</summary>
    protected virtual string? SeedAdminPassword => AdminPassword;

    protected virtual int LoginPermitLimit => 10_000;

    protected virtual int PasswordChangePermitLimit => 10_000;

    /// <summary>Keeps the cookie keys in memory; false uses the app's own key storage in the database.</summary>
    protected virtual bool UseEphemeralKeys => true;

    public virtual async ValueTask InitializeAsync()
    {
        ConnectionString = await TestDatabase.CreateAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (ConnectionString.Length > 0)
        {
            await TestDatabase.DropAsync(ConnectionString);
        }
        GC.SuppressFinalize(this);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Added last, so these win over appsettings.Development.json (which points to the local database).
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ManageGames"] = ConnectionString,
            ["Seed:AdminUsername"] = AdminUsername,
            ["Seed:AdminPassword"] = SeedAdminPassword ?? string.Empty,
            ["RateLimiting:LoginPermitLimit"] = LoginPermitLimit.ToString(CultureInfo.InvariantCulture),
            ["RateLimiting:PasswordChangePermitLimit"] = PasswordChangePermitLimit.ToString(CultureInfo.InvariantCulture),
            // Never the developer's own cover API keys from the user secrets: the tests must not reach the internet.
            ["Covers:Igdb:ClientId"] = string.Empty,
            ["Covers:Igdb:ClientSecret"] = string.Empty,
            ["Covers:SteamGridDb:ApiKey"] = string.Empty,
        }));
        if (UseEphemeralKeys)
        {
            // Keep the keys that encrypt the auth cookie in memory instead of the database.
            builder.ConfigureTestServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
        }
    }

    /// <summary>
    /// A client that acts like a browser: uses HTTPS, doesn't follow redirects and, unless told
    /// otherwise, keeps cookies.
    /// </summary>
    public HttpClient CreateBrowser(bool handleCookies = true)
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = handleCookies,
            BaseAddress = new Uri("https://localhost"),
        });
    }

    /// <summary>Creates a user through the app's user service, with a unique username.</summary>
    public async Task<TestUser> CreateUserAsync(bool isAdmin = false, bool mustChangePassword = false)
    {
        var user = new TestUser(Unique("user"), UserPassword);
        using var scope = Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<UserService>()
            .CreateUserAsync(user.Username, user.Password, isAdmin, mustChangePassword);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(e => e.Description)));
        return user;
    }

    public async Task<HttpClient> SignInAsync(string username, string password)
    {
        var browser = CreateBrowser();
        var response = await browser.LoginAsync(username, password);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(Browser.AuthCookieName + "=", StringComparison.Ordinal));
        return browser;
    }

    public Task<HttpClient> SignInAsync(TestUser user)
    {
        return SignInAsync(user.Username, user.Password);
    }

    public Task<HttpClient> SignInAsAdminAsync()
    {
        return SignInAsync(AdminUsername, AdminPassword);
    }

    public T Query<T>(Func<AppDbContext, T> query)
    {
        using var scope = Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<T> WithServiceAsync<TService, T>(Func<TService, Task<T>> action)
        where TService : notnull
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    public int GameId(string name)
    {
        return Query(db => db.Games.Single(g => g.Name == name).Id);
    }

    public Guid UserId(string username)
    {
        return Query(db => db.Users.Single(u => u.UserName == username).Id);
    }

    /// <summary>Ends all sessions of the user on the server, as a password change elsewhere would.</summary>
    public async Task EndSessionsAsync(string username)
    {
        var userId = UserId(username);
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SessionService>().EndAllAsync(userId);
    }

    /// <summary>A name no other test uses, since the tests of a class share one database.</summary>
    public static string Unique(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
    }
}
