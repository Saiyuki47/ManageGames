using ManageGames.Tests.Infrastructure;

namespace ManageGames.Tests
{
    public class AdminTests : IClassFixture<ManageGamesFactory>
    {
        private readonly ManageGamesFactory _factory;

        public AdminTests(ManageGamesFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("/Consoles")]
        [InlineData("/Companies")]
        [InlineData("/Users")]
        public async Task NonAdmin_IsDeniedTheAdminPages(string page)
        {
            var browser = await _factory.SignInAsync(_factory.CreateUser());

            var response = await browser.GetAsync(page);

            Browser.AssertRedirect(response, "/Account/AccessDenied?ReturnUrl=" + Uri.EscapeDataString(page));
        }

        [Fact]
        public async Task NonAdmin_CannotCreateSharedData()
        {
            var browser = await _factory.SignInAsync(_factory.CreateUser());
            var name = ManageGamesFactory.Unique("Forbidden");

            var response = await browser.SubmitFormAsync("/Games", "/Consoles/Create", new Dictionary<string, string> { ["Name"] = name });

            Browser.AssertRedirect(response, "/Account/AccessDenied?ReturnUrl=%2FConsoles%2FCreate");
            Assert.False(_factory.Query(db => db.Consoles.Any(c => c.ConsoleName == name)));
        }

        [Fact]
        public async Task Navigation_ShowsAdminLinks_ToAdminsOnly()
        {
            var user = await _factory.SignInAsync(_factory.CreateUser());
            var admin = await _factory.SignInAsAdminAsync();

            Assert.DoesNotContain("href=\"/Consoles\"", await user.GetPageAsync("/Games"));
            Assert.Contains("href=\"/Consoles\"", await admin.GetPageAsync("/Games"));
        }

        [Fact]
        public async Task Admin_ManagesCompaniesAndConsoles_WithoutLosingGames()
        {
            var admin = await _factory.SignInAsAdminAsync();
            var companyName = ManageGamesFactory.Unique("Nintendo");
            var consoleName = ManageGamesFactory.Unique("Switch");
            var gameName = ManageGamesFactory.Unique("Mario");

            Browser.AssertRedirect(await admin.SubmitFormAsync("/Companies/Create", "/Companies/Create",
                new Dictionary<string, string> { ["Name"] = companyName }), "/Companies");
            var companyId = _factory.Query(db => db.Companies.Single(c => c.CompanyName == companyName).CompanyId);

            Browser.AssertRedirect(await admin.SubmitFormAsync("/Consoles/Create", "/Consoles/Create",
                new Dictionary<string, string> { ["Name"] = consoleName, ["CompanyId"] = companyId.ToString() }), "/Consoles");
            var consoleId = _factory.Query(db => db.Consoles.Single(c => c.ConsoleName == consoleName).ConsoleId);
            Assert.Contains(companyName, await admin.GetPageAsync("/Consoles"));

            await admin.AddGameAsync(gameName, consoleId: consoleId);

            // Deleting the company keeps its console; deleting the console keeps its games.
            Browser.AssertRedirect(await admin.SubmitFormAsync("/Companies", $"/Companies/Delete/{companyId}"), "/Companies");
            Assert.Null(_factory.Query(db => db.Consoles.Single(c => c.ConsoleId == consoleId).CompanyId));

            Browser.AssertRedirect(await admin.SubmitFormAsync("/Consoles", $"/Consoles/Delete/{consoleId}"), "/Consoles");
            Assert.Null(_factory.Query(db => db.Games.Single(g => g.GameName == gameName).ConsoleId));
        }
    }
}
