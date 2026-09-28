using System.Net;
using ManageGames.Tests.Infrastructure;

namespace ManageGames.Tests
{
    public class LowLoginLimitFactory : ManageGamesFactory
    {
        protected override int LoginPermitLimit => 3;
    }

    public class LoginRateLimitTests : IClassFixture<LowLoginLimitFactory>
    {
        private readonly LowLoginLimitFactory _factory;

        public LoginRateLimitTests(LowLoginLimitFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Login_IsThrottled_AfterTooManyAttempts()
        {
            var browser = _factory.CreateBrowser();
            for (var attempt = 0; attempt < 3; attempt++)
            {
                Browser.AssertRedirect(await browser.LoginAsync("nobody", "wrong-password"), "/");
            }

            var response = await browser.LoginAsync("nobody", "wrong-password");

            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }
}
