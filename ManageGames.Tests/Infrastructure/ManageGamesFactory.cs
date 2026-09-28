using System.Net;
using ManageGames.Data;
using ManageGames.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace ManageGames.Tests.Infrastructure
{
    public record TestUser(string Username, string Password);

    /// <summary>
    /// Hosts the real app in memory against its own throw-away SQLite file and a known admin password.
    /// </summary>
    public class ManageGamesFactory : WebApplicationFactory<Program>
    {
        public const string AdminUsername = "admin";
        // Test-only password of the admin seeded into the throw-away test database.
        public const string AdminPassword = "Test-Admin-Password-1";
        public const string UserPassword = "Test-User-Password-1";

        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"managegames-tests-{Guid.NewGuid():N}.db");

        /// <summary>Password of the seeded admin; null lets the app generate a one-time password.</summary>
        protected virtual string? SeedAdminPassword => AdminPassword;

        protected virtual int LoginPermitLimit => 10_000;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:ManageGames", $"Data Source={_databasePath}");
            builder.UseSetting("Seed:AdminUsername", AdminUsername);
            builder.UseSetting("Seed:AdminPassword", SeedAdminPassword ?? string.Empty);
            builder.UseSetting("RateLimiting:LoginPermitLimit", LoginPermitLimit.ToString());
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

        /// <summary>Creates a user directly in the database, with a unique username.</summary>
        public TestUser CreateUser(bool isAdmin = false, bool mustChangePassword = false)
        {
            var user = new TestUser(Unique("user"), UserPassword);
            using var scope = Services.CreateScope();
            var result = scope.ServiceProvider.GetRequiredService<UserService>()
                .CreateUser(user.Username, user.Password, isAdmin, mustChangePassword);
            Assert.Equal(CreateUserResult.Created, result);
            return user;
        }

        public async Task<HttpClient> SignInAsync(string username, string password)
        {
            var browser = CreateBrowser();
            var response = await browser.LoginAsync(username, password);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(Browser.AuthCookieName + "="));
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

        public int GameId(string name)
        {
            return Query(db => db.Games.Single(g => g.GameName == name).GameId);
        }

        public Guid UserId(string username)
        {
            return Query(db => db.Users.Single(u => u.Username == username).UserID);
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
                File.Delete(_databasePath);
            }
        }
    }
}
