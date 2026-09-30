using System.Buffers.Binary;
using System.Net;
using ManageGames.Auth;
using ManageGames.Models;
using ManageGames.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ManageGames.Tests;

public class SecurityTests(ManageGamesFactory factory) : IClassFixture<ManageGamesFactory>
{
    [Fact]
    public async Task Responses_CarryTheSecurityHeaders()
    {
        var response = await factory.CreateBrowser().GetAsync("/Home/Privacy");

        Assert.Equal(SecurityHeaders.ContentSecurityPolicy, response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("same-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.False(response.Headers.Contains("Server"));
    }

    [Fact]
    public async Task AntiforgeryAndMessageCookies_AreSecureHostCookies()
    {
        var browser = factory.CreateBrowser();
        var page = await browser.GetAsync("/Home/Privacy");
        var failedLogin = await browser.LoginAsync("nobody", "wrong-password");

        Assert.Contains("secure", Browser.CookieAttributes(page, "__Host-ManageGames.Antiforgery"));
        Assert.Contains("path=/", Browser.CookieAttributes(page, "__Host-ManageGames.Antiforgery"));
        Assert.Contains("secure", Browser.CookieAttributes(failedLogin, "__Host-ManageGames.TempData"));
        Assert.Contains("samesite=strict", Browser.CookieAttributes(failedLogin, "__Host-ManageGames.TempData"));
    }

    [Fact]
    public async Task UnknownGame_ShowsAFriendlyNotFoundPage_WithTheSecurityHeaders()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());

        var response = await browser.GetAsync("/Games/Edit/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Page not found", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task Passwords_AreHashedWithTheConfiguredIterationCount()
    {
        var user = await factory.CreateUserAsync();

        var hash = factory.Query(db => db.Users.Single(u => u.UserName == user.Username).PasswordHash)!;

        Assert.Equal(PasswordPolicy.HashIterationCount, IterationCount(hash));
    }

    [Fact]
    public async Task WeakerHashes_AreUpgradedAtTheNextLogin()
    {
        var username = ManageGamesFactory.Unique("legacy-hash");
        var oldHasher = new PasswordHasher<AppUser>(Options.Create(new PasswordHasherOptions { IterationCount = 100_000 }));
        await factory.WithServiceAsync<UserManager<AppUser>, IdentityResult>(async users =>
        {
            var user = new AppUser { UserName = username };
            await users.CreateAsync(user);
            user.PasswordHash = oldHasher.HashPassword(user, ManageGamesFactory.UserPassword);
            return await users.UpdateAsync(user);
        });

        await factory.SignInAsync(username, ManageGamesFactory.UserPassword);

        var hash = factory.Query(db => db.Users.Single(u => u.UserName == username).PasswordHash)!;
        Assert.Equal(PasswordPolicy.HashIterationCount, IterationCount(hash));
    }

    [Fact]
    public async Task Lists_SortNamesWithUmlautsNextToTheirBaseLetter()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        foreach (var name in new[] { "Zelda", "Ökami", "abzû", "Okami" })
        {
            await browser.AddGameAsync(name);
        }

        // Razor encodes non-ASCII letters as character references.
        var html = WebUtility.HtmlDecode(await browser.GetPageAsync("/Games"));

        var positions = new[] { "abzû", "Okami", "Ökami", "Zelda" }.Select(n => html.IndexOf(n, StringComparison.Ordinal)).ToList();
        Assert.Equal(positions.Order().ToList(), positions);
    }

    [Fact]
    public async Task Timestamps_AreReadBackAsUtc()
    {
        var user = await factory.CreateUserAsync();

        var createdAt = factory.Query(db => db.Users.Single(u => u.UserName == user.Username).CreatedAt);

        Assert.Equal(DateTimeKind.Utc, createdAt.Kind);
        Assert.InRange(DateTime.UtcNow - createdAt, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    // Identity v3 hash layout: format marker, PRF, iteration count (big-endian), salt length, salt, subkey.
    private static int IterationCount(string hash)
    {
        var bytes = Convert.FromBase64String(hash);
        Assert.Equal(0x01, bytes[0]);
        return (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5));
    }
}

/// <summary>Identity re-validates the security stamp on every request, which also refreshes the claims.</summary>
public class ConstantStampValidationFactory : ManageGamesFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
            services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero));
    }
}

public class SecurityStampRefreshTests(ConstantStampValidationFactory factory) : IClassFixture<ConstantStampValidationFactory>
{
    [Fact]
    public async Task Session_SurvivesIdentityRefreshingTheClaims()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());

        for (var request = 0; request < 3; request++)
        {
            Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Games")).StatusCode);
        }
    }
}
