using System.Net;
using ManageGames.Data;
using ManageGames.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace ManageGames.Tests.Infrastructure;

public record TestUser(string Username, string Password);

/// <summary>
/// Hosts the real app in memory against its own throw-away SQLite file and a known admin password.
/// </summary>
public class ManageGamesFactory : WebApplicationFactory<Program>
{
    public const string AdminUsername = "admin";
    // Test-only passwords; they meet the password policy (15+ characters, not guessable).
    public const string AdminPassword = "Blue-Ocean-Lantern-42";
    public const string UserPassword = "Quiet-Maple-Rocket-17";

    /// <summary>The SQLite file of this factory's app; deleted when the factory is disposed.</summary>
    protected virtual string DatabasePath { get; } = Path.Combine(Path.GetTempPath(), $"managegames-tests-{Guid.NewGuid():N}.db");

    /// <summary>Password of the seeded admin; null lets the app generate a one-time password.</summary>
    protected virtual string? SeedAdminPassword => AdminPassword;

    protected virtual int LoginPermitLimit => 10_000;

    protected virtual int PasswordChangePermitLimit => 10_000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:ManageGames", $"Data Source={DatabasePath}");
        builder.UseSetting("Seed:AdminUsername", AdminUsername);
        builder.UseSetting("Seed:AdminPassword", SeedAdminPassword ?? string.Empty);
        builder.UseSetting("RateLimiting:LoginPermitLimit", LoginPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("RateLimiting:PasswordChangePermitLimit", PasswordChangePermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
        // Keep the keys that encrypt the auth cookie in memory instead of the user profile.
        builder.ConfigureTestServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
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
    public void EndSessions(string username)
    {
        var userId = UserId(username);
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SessionService>().EndAll(userId);
    }

    /// <summary>A name no other test uses, since the tests of a class share one database.</summary>
    public static string Unique(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            // Pooled connections keep the file open.
            SqliteConnection.ClearAllPools();
            File.Delete(DatabasePath);
        }
    }
}
