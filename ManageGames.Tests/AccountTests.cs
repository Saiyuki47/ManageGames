using System.Net;
using ManageGames.Models;
using ManageGames.Services;
using ManageGames.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;

namespace ManageGames.Tests;

public class AccountTests(ManageGamesFactory factory) : IClassFixture<ManageGamesFactory>
{
    [Fact]
    public async Task CreatedUser_MustChooseOwnPassword_BeforeUsingTheApp()
    {
        var admin = await factory.SignInAsAdminAsync();
        var username = ManageGamesFactory.Unique("newbie");

        Browser.AssertRedirect(await admin.SubmitFormAsync("/Users/Create", "/Users/Create", new Dictionary<string, string>
        {
            ["Username"] = username,
            ["Password"] = "Initial-Password-1",
            ["IsAdmin"] = "false",
        }), "/Users");

        var browser = await factory.SignInAsync(username, "Initial-Password-1");
        Browser.AssertRedirect(await browser.GetAsync("/Games"), "/Account/ChangePassword");

        Browser.AssertRedirect(await browser.ChangePasswordAsync("Initial-Password-1", "My-Own-Password-1"), "/Games");
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Games")).StatusCode);
    }

    [Fact]
    public async Task CreatedAdmin_GetsTheAdminRole()
    {
        var admin = await factory.SignInAsAdminAsync();
        var username = ManageGamesFactory.Unique("second-admin");

        Browser.AssertRedirect(await admin.SubmitFormAsync("/Users/Create", "/Users/Create", new Dictionary<string, string>
        {
            ["Username"] = username,
            ["Password"] = "Initial-Password-1",
            ["IsAdmin"] = "true",
        }), "/Users");

        var roles = await factory.WithServiceAsync<UserManager<AppUser>, IList<string>>(async users =>
            await users.GetRolesAsync((await users.FindByNameAsync(username))!));
        Assert.Equal(["Admin"], roles);
    }

    [Fact]
    public async Task CreateUser_RejectsAnExistingUsername_IgnoringCaseAndSpaces()
    {
        var existing = await factory.CreateUserAsync();
        var admin = await factory.SignInAsAdminAsync();

        var response = await admin.SubmitFormAsync("/Users/Create", "/Users/Create", new Dictionary<string, string>
        {
            ["Username"] = " " + existing.Username.ToUpperInvariant(),
            ["Password"] = "Whatever-Password-1",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("This username is already taken.", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Short-Pass-1")]            // shorter than 15 characters
    [InlineData("password1234567")]         // common word padded with digits
    [InlineData("123456789012345")]         // sequence
    [InlineData("hallo123hallo123")]        // repetition
    public async Task ChangePassword_EnforcesThePasswordPolicy(string weakPassword)
    {
        var user = await factory.CreateUserAsync();
        var browser = await factory.SignInAsync(user);

        var response = await browser.ChangePasswordAsync(user.Password, weakPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("field-error", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await factory.SignInAsync(user);
    }

    [Fact]
    public async Task ChangePassword_RejectsAWrongCurrentPassword()
    {
        var user = await factory.CreateUserAsync();
        var browser = await factory.SignInAsync(user);

        var response = await browser.ChangePasswordAsync("not-my-password", "Another-Password-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("The current password is wrong.", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await factory.SignInAsync(user);
    }

    [Fact]
    public async Task ChangePassword_EndsOtherSessions_AndRenewsTheCurrentOne()
    {
        var user = await factory.CreateUserAsync();
        var laptop = factory.CreateBrowser();
        var copiedCookie = Browser.GetAuthCookie(await laptop.LoginAsync(user.Username, user.Password));
        var phone = await factory.SignInAsync(user);

        Browser.AssertRedirect(await laptop.ChangePasswordAsync(user.Password, "Changed-Password-1"), "/Games");

        Assert.Equal(HttpStatusCode.OK, (await laptop.GetAsync("/Games")).StatusCode);
        Browser.AssertRedirect(await phone.GetAsync("/Games"), "/?ReturnUrl=%2FGames");
        // The laptop continues with a new session, so a copy of its old cookie is worthless.
        var replay = factory.CreateBrowser(handleCookies: false);
        replay.DefaultRequestHeaders.Add("Cookie", copiedCookie);
        Browser.AssertRedirect(await replay.GetAsync("/Games"), "/?ReturnUrl=%2FGames");
    }

    [Fact]
    public async Task ResetPassword_EndsTheUsersSessions_AndForcesANewPassword()
    {
        var user = await factory.CreateUserAsync();
        var userBrowser = await factory.SignInAsync(user);
        var userId = factory.UserId(user.Username);
        var admin = await factory.SignInAsAdminAsync();

        Browser.AssertRedirect(await admin.SubmitFormAsync($"/Users/ResetPassword/{userId}", $"/Users/ResetPassword/{userId}",
            new Dictionary<string, string> { ["NewPassword"] = "Temporary-Password-1" }), "/Users");

        Browser.AssertRedirect(await userBrowser.GetAsync("/Games"), "/?ReturnUrl=%2FGames");
        var again = await factory.SignInAsync(user.Username, "Temporary-Password-1");
        Browser.AssertRedirect(await again.GetAsync("/Games"), "/Account/ChangePassword");
    }

    [Fact]
    public async Task ResetPassword_LiftsALockout()
    {
        var user = await factory.CreateUserAsync();
        var attacker = factory.CreateBrowser();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await attacker.LoginAsync(user.Username, $"wrong-password-{attempt}");
        }
        var userId = factory.UserId(user.Username);
        var admin = await factory.SignInAsAdminAsync();

        Browser.AssertRedirect(await admin.SubmitFormAsync($"/Users/ResetPassword/{userId}", $"/Users/ResetPassword/{userId}",
            new Dictionary<string, string> { ["NewPassword"] = "Temporary-Password-1" }), "/Users");

        await factory.SignInAsync(user.Username, "Temporary-Password-1");
    }

    [Fact]
    public async Task CommandLineReset_IssuesAOneTimePassword_ThatMustBeReplaced()
    {
        var user = await factory.CreateUserAsync();
        var oldSession = await factory.SignInAsync(user);
        var locker = factory.CreateBrowser();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await locker.LoginAsync(user.Username, $"wrong-password-{attempt}");
        }

        var oneTimePassword = await factory.WithServiceAsync<UserService, string?>(users => users.ResetToOneTimePasswordAsync(user.Username));

        Assert.NotNull(oneTimePassword);
        Browser.AssertRedirect(await oldSession.GetAsync("/Games"), "/?ReturnUrl=%2FGames");
        var browser = await factory.SignInAsync(user.Username, oneTimePassword);
        Browser.AssertRedirect(await browser.GetAsync("/Games"), "/Account/ChangePassword");
    }

    [Fact]
    public async Task CommandLineReset_OfAnUnknownUser_ChangesNothing()
    {
        Assert.Null(await factory.WithServiceAsync<UserService, string?>(users => users.ResetToOneTimePasswordAsync("nobody-at-all")));
    }

    [Fact]
    public async Task DeleteUser_RemovesTheirGames_AndEndsTheirSession()
    {
        var user = await factory.CreateUserAsync();
        var userBrowser = await factory.SignInAsync(user);
        var gameName = ManageGamesFactory.Unique("Orphan");
        await userBrowser.AddGameAsync(gameName);
        var userId = factory.UserId(user.Username);
        var admin = await factory.SignInAsAdminAsync();

        Browser.AssertRedirect(await admin.SubmitFormAsync("/Users", $"/Users/Delete/{userId}"), "/Users");

        Assert.False(factory.Query(db => db.Users.Any(u => u.Id == userId)));
        Assert.False(factory.Query(db => db.Games.Any(g => g.Name == gameName)));
        Assert.False(factory.Query(db => db.UserSessions.Any(s => s.UserId == userId)));
        Browser.AssertRedirect(await userBrowser.GetAsync("/Games"), "/?ReturnUrl=%2FGames");
    }

    [Fact]
    public async Task Admin_CannotDeleteTheirOwnAccount()
    {
        var adminUser = await factory.CreateUserAsync(isAdmin: true);
        var admin = await factory.SignInAsync(adminUser);
        var adminId = factory.UserId(adminUser.Username);

        Browser.AssertRedirect(await admin.SubmitFormAsync("/Users", $"/Users/Delete/{adminId}"), "/Users");

        Assert.True(factory.Query(db => db.Users.Any(u => u.Id == adminId)));
        Assert.Contains("You can&#x27;t delete your own account.", await admin.GetPageAsync("/Users"), StringComparison.Ordinal);
    }
}
