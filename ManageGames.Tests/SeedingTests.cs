using System.Text.RegularExpressions;
using ManageGames.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ManageGames.Tests
{
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

    public class SeedingTests : IClassFixture<FreshInstallFactory>
    {
        private readonly FreshInstallFactory _factory;

        public SeedingTests(FreshInstallFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task FreshInstall_LogsAOneTimeAdminPassword_ThatMustBeChanged()
        {
            var admin = _factory.Query(db => db.Users.Single());
            Assert.True(admin.IsAdmin);
            Assert.True(admin.MustChangePassword);

            var message = Assert.Single(_factory.Logs.Messages, m => m.Contains("one-time password"));
            var password = Regex.Match(message, "one-time password '([^']+)'").Groups[1].Value;
            var browser = await _factory.SignInAsync(ManageGamesFactory.AdminUsername, password);

            Browser.AssertRedirect(await browser.GetAsync("/Games"), "/Account/ChangePassword");
        }
    }
}
