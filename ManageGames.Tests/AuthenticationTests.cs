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
        public async Task Logout_WorksAfterTheSessionEndedElsewhere()
        {
            var user = _factory.CreateUser();
            var laptop = await _factory.SignInAsync(user);
            var phone = await _factory.SignInAsync(user);
            var phoneToken = await phone.GetFormTokenAsync("/Games");

            // Logging out on the laptop ends the phone's session too; its page still shows "Log out".
            Browser.AssertRedirect(await laptop.LogoutAsync(), "/");

            Browser.AssertRedirect(await phone.PostFormAsync("/Account/Logout", phoneToken), "/");
        }

        [Fact]
        public async Task FormPost_AfterTheSessionEnded_ReturnsToTheStartPage_NotToThePostOnlyAction()
        {
            var user = _factory.CreateUser();
            var browser = await _factory.SignInAsync(user);
            var token = await browser.GetFormTokenAsync("/Games/Create");
            await (await _factory.SignInAsync(user)).LogoutAsync();

            var response = await browser.PostFormAsync("/Games/Create", token, new Dictionary<string, string>
            {
                ["Name"] = ManageGamesFactory.Unique("Late"),
                ["Copies"] = "1",
            });

            Browser.AssertRedirect(response, "/?ReturnUrl=%2F");
        }

        [Fact]
        public async Task Login_FromAFormLoadedBeforeSigningIn_KeepsTheExistingSession()
        {
            var user = _factory.CreateUser();
            var browser = _factory.CreateBrowser();
            // A second tab, loaded anonymously before the user signed in in the first one.
            var staleToken = await browser.GetFormTokenAsync("/Home/Privacy");
            Browser.AssertRedirect(await browser.LoginAsync(user.Username, user.Password), "/Games");

            var response = await browser.PostFormAsync("/Account/Login", staleToken, new Dictionary<string, string>
            {
                ["Username"] = user.Username,
                ["Password"] = user.Password,
                ["ReturnUrl"] = "/Games/Wishlist",
            });

            Browser.AssertRedirect(response, "/Games/Wishlist");
        }

        [Fact]
        public async Task Login_WithoutAntiforgeryToken_IsRejected()
        {
            var user = _factory.CreateUser();

            var response = await _factory.CreateBrowser().PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Username"] = user.Username,
                ["Password"] = user.Password,
            }));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            response.Headers.TryGetValues("Set-Cookie", out var cookies);
            Assert.DoesNotContain(cookies ?? [], c => c.StartsWith(Browser.AuthCookieName + "="));
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
