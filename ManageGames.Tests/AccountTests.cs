using System.Net;
using ManageGames.Tests.Infrastructure;

namespace ManageGames.Tests
{
    public class AccountTests : IClassFixture<ManageGamesFactory>
    {
        private readonly ManageGamesFactory _factory;

        public AccountTests(ManageGamesFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task CreatedUser_MustChooseOwnPassword_BeforeUsingTheApp()
        {
            var admin = await _factory.SignInAsAdminAsync();
            var username = ManageGamesFactory.Unique("newbie");

            Browser.AssertRedirect(await admin.SubmitFormAsync("/Users/Create", "/Users/Create", new Dictionary<string, string>
            {
                ["Username"] = username,
                ["Password"] = "Initial-Password-1",
                ["IsAdmin"] = "false",
            }), "/Users");

            var browser = await _factory.SignInAsync(username, "Initial-Password-1");
            Browser.AssertRedirect(await browser.GetAsync("/Games"), "/Account/ChangePassword");

            Browser.AssertRedirect(await browser.ChangePasswordAsync("Initial-Password-1", "My-Own-Password-1"), "/Games");
            Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Games")).StatusCode);
        }

        [Fact]
        public async Task CreateUser_RejectsAnExistingUsername_IgnoringCaseAndSpaces()
        {
            var existing = _factory.CreateUser();
            var admin = await _factory.SignInAsAdminAsync();

            var response = await admin.SubmitFormAsync("/Users/Create", "/Users/Create", new Dictionary<string, string>
            {
                ["Username"] = " " + existing.Username.ToUpperInvariant(),
                ["Password"] = "Whatever-Password-1",
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("This username is already taken.", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task ChangePassword_RejectsAWrongCurrentPassword()
        {
            var user = _factory.CreateUser();
            var browser = await _factory.SignInAsync(user);

            var response = await browser.ChangePasswordAsync("not-my-password", "Another-Password-1");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("The current password is wrong.", await response.Content.ReadAsStringAsync());
            await _factory.SignInAsync(user);
        }

        [Fact]
        public async Task ChangePassword_EndsOtherSessions_ButKeepsTheCurrentOne()
        {
            var user = _factory.CreateUser();
            var laptop = await _factory.SignInAsync(user);
            var phone = await _factory.SignInAsync(user);

            Browser.AssertRedirect(await laptop.ChangePasswordAsync(user.Password, "Changed-Password-1"), "/Games");

            Assert.Equal(HttpStatusCode.OK, (await laptop.GetAsync("/Games")).StatusCode);
            Browser.AssertRedirect(await phone.GetAsync("/Games"), "/?ReturnUrl=%2FGames");
        }

        [Fact]
        public async Task ResetPassword_EndsTheUsersSessions_AndForcesANewPassword()
        {
            var user = _factory.CreateUser();
            var userBrowser = await _factory.SignInAsync(user);
            var userId = _factory.UserId(user.Username);
            var admin = await _factory.SignInAsAdminAsync();

            Browser.AssertRedirect(await admin.SubmitFormAsync($"/Users/ResetPassword/{userId}", $"/Users/ResetPassword/{userId}",
                new Dictionary<string, string> { ["NewPassword"] = "Temporary-Password-1" }), "/Users");

            Browser.AssertRedirect(await userBrowser.GetAsync("/Games"), "/?ReturnUrl=%2FGames");
            var again = await _factory.SignInAsync(user.Username, "Temporary-Password-1");
            Browser.AssertRedirect(await again.GetAsync("/Games"), "/Account/ChangePassword");
        }

        [Fact]
        public async Task DeleteUser_RemovesTheirGames_AndEndsTheirSession()
        {
            var user = _factory.CreateUser();
            var userBrowser = await _factory.SignInAsync(user);
            var gameName = ManageGamesFactory.Unique("Orphan");
            await userBrowser.AddGameAsync(gameName);
            var userId = _factory.UserId(user.Username);
            var admin = await _factory.SignInAsAdminAsync();

            Browser.AssertRedirect(await admin.SubmitFormAsync("/Users", $"/Users/Delete/{userId}"), "/Users");

            Assert.False(_factory.Query(db => db.Users.Any(u => u.UserID == userId)));
            Assert.False(_factory.Query(db => db.Games.Any(g => g.GameName == gameName)));
            Browser.AssertRedirect(await userBrowser.GetAsync("/Games"), "/?ReturnUrl=%2FGames");
        }

        [Fact]
        public async Task Admin_CannotDeleteTheirOwnAccount()
        {
            var adminUser = _factory.CreateUser(isAdmin: true);
            var admin = await _factory.SignInAsync(adminUser);
            var adminId = _factory.UserId(adminUser.Username);

            Browser.AssertRedirect(await admin.SubmitFormAsync("/Users", $"/Users/Delete/{adminId}"), "/Users");

            Assert.True(_factory.Query(db => db.Users.Any(u => u.UserID == adminId)));
            Assert.Contains("You can&#x27;t delete your own account.", await admin.GetPageAsync("/Users"));
        }
    }
}
