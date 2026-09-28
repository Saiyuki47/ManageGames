using System.Net;
using ManageGames.Tests.Infrastructure;

namespace ManageGames.Tests
{
    public class AuthenticationTests : IClassFixture<ManageGamesFactory>
    {
        private readonly ManageGamesFactory _factory;

        public AuthenticationTests(ManageGamesFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task ProtectedPage_SendsAnonymousVisitorToLogin()
        {
            var response = await _factory.CreateBrowser().GetAsync("/Games");

            Browser.AssertRedirect(response, "/?ReturnUrl=%2FGames");
        }

        [Fact]
        public async Task StartPage_OpensLoginForm_WhenAProtectedPageSentTheVisitor()
        {
            var html = await _factory.CreateBrowser().GetPageAsync("/?ReturnUrl=%2FGames");

            Assert.Contains("class=\"login-overlay open\"", html);
            Assert.Contains("name=\"ReturnUrl\" value=\"/Games\"", html);
        }

        [Fact]
        public async Task Login_WithWrongPassword_ShowsError_AndSignsNobodyIn()
        {
            var browser = _factory.CreateBrowser();

            var response = await browser.LoginAsync(ManageGamesFactory.AdminUsername, "wrong-password");

            Browser.AssertRedirect(response, "/");
            response.Headers.TryGetValues("Set-Cookie", out var cookies);
            Assert.DoesNotContain(cookies ?? [], c => c.StartsWith(Browser.AuthCookieName + "="));
            Assert.Contains("Wrong username or password.", await browser.GetPageAsync("/"));
        }

        [Fact]
        public async Task Login_IssuesHttpOnlySecureStrictCookie()
        {
            var user = _factory.CreateUser();

            var response = await _factory.CreateBrowser().LoginAsync(user.Username, user.Password);

            var attributes = response.Headers.GetValues("Set-Cookie")
                .Single(c => c.StartsWith(Browser.AuthCookieName + "="))
                .Split(';')
                .Select(a => a.Trim().ToLowerInvariant())
                .ToList();
            Assert.Contains("httponly", attributes);
            Assert.Contains("secure", attributes);
            Assert.Contains("samesite=strict", attributes);
        }

        [Fact]
        public async Task Login_FollowsLocalReturnUrls_Only()
        {
            var user = _factory.CreateUser();

            var local = await _factory.CreateBrowser().LoginAsync(user.Username, user.Password, returnUrl: "/Games/Wishlist");
            var external = await _factory.CreateBrowser().LoginAsync(user.Username, user.Password, returnUrl: "https://evil.example/");

            Browser.AssertRedirect(local, "/Games/Wishlist");
            Browser.AssertRedirect(external, "/Games");
        }

        [Fact]
        public async Task Logout_RevokesTheSessionOnTheServer()
        {
            var user = _factory.CreateUser();
            var browser = _factory.CreateBrowser();
            var copiedCookie = Browser.GetAuthCookie(await browser.LoginAsync(user.Username, user.Password));
            var replay = _factory.CreateBrowser(handleCookies: false);
            replay.DefaultRequestHeaders.Add("Cookie", copiedCookie);
            Assert.Equal(HttpStatusCode.OK, (await replay.GetAsync("/Games")).StatusCode);

            Browser.AssertRedirect(await browser.LogoutAsync(), "/");

            // A copy of the cookie taken before the logout must be worthless afterwards.
            Browser.AssertRedirect(await replay.GetAsync("/Games"), "/?ReturnUrl=%2FGames");
            Browser.AssertRedirect(await browser.GetAsync("/Games"), "/?ReturnUrl=%2FGames");
        }

        [Fact]
        public async Task TamperedAuthCookie_IsTreatedAsAnonymous()
        {
            var browser = _factory.CreateBrowser(handleCookies: false);
            browser.DefaultRequestHeaders.Add("Cookie", Browser.AuthCookieName + "=not-a-valid-ticket");

            var response = await browser.GetAsync("/Games");

            Browser.AssertRedirect(response, "/?ReturnUrl=%2FGames");
        }

        [Fact]
        public async Task Post_WithoutAntiforgeryToken_IsRejected()
        {
            var browser = await _factory.SignInAsync(_factory.CreateUser());
            var name = ManageGamesFactory.Unique("Forged");

            var response = await browser.PostAsync("/Games/Create", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Name"] = name,
                ["Copies"] = "1",
            }));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(_factory.Query(db => db.Games.Any(g => g.GameName == name)));
        }
    }
}
