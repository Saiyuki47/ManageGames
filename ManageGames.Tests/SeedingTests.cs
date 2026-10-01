using System.Text.RegularExpressions;
using ManageGames.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ManageGames.Tests;

/// <summary>Starts the app like a fresh install: no admin password is configured.</summary>
public class FreshInstallFactory : ManageGamesFactory
{
    public CapturingLoggerProvider Logs { get; } = new();

    protected override string? SeedAdminPassword => null;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<ILoggerProvider>(Logs));
    }
}

public class SeedingTests(FreshInstallFactory factory) : IClassFixture<FreshInstallFactory>
{
    [Fact]
    public async Task FreshInstall_LogsAOneTimeAdminPassword_ThatMustBeChanged()
    {
        var admin = factory.Query(db => db.Users.Single());
        Assert.True(factory.Query(db => db.UserRoles.Any(r => r.UserId == admin.Id)));
        Assert.True(admin.MustChangePassword);

        var message = Assert.Single(factory.Logs.Messages, m => m.Contains("one-time password", StringComparison.Ordinal));
        var password = Regex.Match(message, "one-time password '([^']+)'").Groups[1].Value;
        var browser = await factory.SignInAsync(ManageGamesFactory.AdminUsername, password);

        Browser.AssertRedirect(await browser.GetAsync("/Games"), "/Account/ChangePassword");
    }
}
