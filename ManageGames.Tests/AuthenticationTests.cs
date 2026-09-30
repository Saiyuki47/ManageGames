using System.Net;
using ManageGames.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ManageGames.Tests;

public class AuthenticationTests(ManageGamesFactory factory) : IClassFixture<ManageGamesFactory>
{
    [Fact]
    public async Task ProtectedPage_SendsAnonymousVisitorToLogin()
    {
        var response = await factory.CreateBrowser().GetAsync("/Games");

        Browser.AssertRedirect(response, "/?ReturnUrl=%2FGames");
    }

    [Fact]
    public async Task StartPage_OpensLoginForm_WhenAProtectedPageSentTheVisitor()
    {
        var html = await factory.CreateBrowser().GetPageAsync("/?ReturnUrl=%2FGames");

        Assert.Contains("class=\"login-overlay open\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ReturnUrl\" value=\"/Games\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShowsError_AndSignsNobodyIn()
    {
        var browser = factory.CreateBrowser();

        var response = await browser.LoginAsync(ManageGamesFactory.AdminUsername, "wrong-password");

        Browser.AssertRedirect(response, "/");
        Assert.False(Browser.SetsAuthCookie(response));
        Assert.Contains("Wrong username or password.", await browser.GetPageAsync("/"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_IssuesHttpOnlySecureStrictHostCookie()
    {
        var user = await factory.CreateUserAsync();

        var response = await factory.CreateBrowser().LoginAsync(user.Username, user.Password);

        var attributes = Browser.CookieAttributes(response, Browser.AuthCookieName);
        Assert.Contains("httponly", attributes);
        Assert.Contains("secure", attributes);
        Assert.Contains("samesite=strict", attributes);
        Assert.Contains("path=/", attributes);
        // A session cookie: no expiry date, so it ends with the browser session.
        Assert.DoesNotContain(attributes, a => a.StartsWith("expires=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Login_FollowsLocalReturnUrls_Only()
    {
        var user = await factory.CreateUserAsync();

        var local = await factory.CreateBrowser().LoginAsync(user.Username, user.Password, returnUrl: "/Games/Wishlist");
        var external = await factory.CreateBrowser().LoginAsync(user.Username, user.Password, returnUrl: "https://evil.example/");

        Browser.AssertRedirect(local, "/Games/Wishlist");
        Browser.AssertRedirect(external, "/Games");
    }

    [Fact]
    public async Task Login_IgnoresCaseAndSpacesInTheUsername()
    {
        var user = await factory.CreateUserAsync();

        var response = await factory.CreateBrowser().LoginAsync(" " + user.Username.ToUpperInvariant(), user.Password);

        Browser.AssertRedirect(response, "/Games");
    }

    [Fact]
    public async Task Login_LocksTheAccount_AfterFiveWrongPasswords()
    {
        var user = await factory.CreateUserAsync();
        var browser = factory.CreateBrowser();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Browser.AssertRedirect(await browser.LoginAsync(user.Username, $"wrong-password-{attempt}"), "/");
        }

        // Even the right password is refused now, with the same message as a wrong one.
        var response = await browser.LoginAsync(user.Username, user.Password);

        Browser.AssertRedirect(response, "/");
        Assert.False(Browser.SetsAuthCookie(response));
        Assert.Contains("an account is locked for a few minutes", await browser.GetPageAsync("/"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logout_EndsOnlyThisDevice_AndRevokesItsCookieOnTheServer()
    {
        var user = await factory.CreateUserAsync();
        var laptop = factory.CreateBrowser();
        var copiedCookie = Browser.GetAuthCookie(await laptop.LoginAsync(user.Username, user.Password));
        var phone = await factory.SignInAsync(user);
        var replay = factory.CreateBrowser(handleCookies: false);
        replay.DefaultRequestHeaders.Add("Cookie", copiedCookie);
        Assert.Equal(HttpStatusCode.OK, (await replay.GetAsync("/Games")).StatusCode);

        Browser.AssertRedirect(await laptop.LogoutAsync(), "/");

        // A copy of the laptop's cookie taken before the logout is worthless afterwards...
        Browser.AssertRedirect(await replay.GetAsync("/Games"), "/?ReturnUrl=%2FGames");
        Browser.AssertRedirect(await laptop.GetAsync("/Games"), "/?ReturnUrl=%2FGames");
        // ...while the phone stays signed in.
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/Games")).StatusCode);
    }

    [Fact]
    public async Task Session_EndsAtItsAbsoluteLifetime_EvenWhenActive()
    {
        var user = await factory.CreateUserAsync();
        var browser = await factory.SignInAsync(user);
        var userId = factory.UserId(user.Username);

        factory.Query(db => db.UserSessions.Where(s => s.UserId == userId)
            .ExecuteUpdate(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(-1))));

        Browser.AssertRedirect(await browser.GetAsync("/Games"), "/?ReturnUrl=%2FGames");
    }

    [Fact]
    public async Task Logout_WorksAfterTheSessionEndedElsewhere()
    {
        var user = await factory.CreateUserAsync();
        var phone = await factory.SignInAsync(user);
        var phoneToken = await phone.GetFormTokenAsync("/Games");

        // E.g. a password change on another device ended the phone's session; its page still shows "Log out".
        factory.EndSessions(user.Username);

        Browser.AssertRedirect(await phone.PostFormAsync("/Account/Logout", phoneToken), "/");
    }

    [Fact]
    public async Task FormPost_AfterTheSessionEnded_ReturnsToTheStartPage_NotToThePostOnlyAction()
    {
        var user = await factory.CreateUserAsync();
        var browser = await factory.SignInAsync(user);
        var token = await browser.GetFormTokenAsync("/Games/Create");
        factory.EndSessions(user.Username);

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
        var user = await factory.CreateUserAsync();
        var browser = factory.CreateBrowser();
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
        var user = await factory.CreateUserAsync();

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = user.Username,
            ["Password"] = user.Password,
        });
        var response = await factory.CreateBrowser().PostAsync(new Uri("/Account/Login", UriKind.Relative), form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(Browser.SetsAuthCookie(response));
    }

    [Fact]
    public async Task TamperedAuthCookie_IsTreatedAsAnonymous()
    {
        var browser = factory.CreateBrowser(handleCookies: false);
        browser.DefaultRequestHeaders.Add("Cookie", Browser.AuthCookieName + "=not-a-valid-ticket");

        var response = await browser.GetAsync("/Games");

        Browser.AssertRedirect(response, "/?ReturnUrl=%2FGames");
    }

    [Fact]
    public async Task Post_WithoutAntiforgeryToken_IsRejected()
    {
        var browser = await factory.SignInAsync(await factory.CreateUserAsync());
        var name = ManageGamesFactory.Unique("Forged");

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Copies"] = "1",
        });
        var response = await browser.PostAsync(new Uri("/Games/Create", UriKind.Relative), form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(factory.Query(db => db.Games.Any(g => g.Name == name)));
    }
}
