using System.Net;
using ManageGames.Tests.Infrastructure;

namespace ManageGames.Tests;

public class LowLoginLimitFactory : ManageGamesFactory
{
    protected override int LoginPermitLimit => 3;
}

public class LoginRateLimitTests(LowLoginLimitFactory factory) : IClassFixture<LowLoginLimitFactory>
{
    [Fact]
    public async Task Login_IsThrottled_AfterTooManyAttempts()
    {
        var browser = factory.CreateBrowser();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Browser.AssertRedirect(await browser.LoginAsync("nobody", "wrong-password"), "/");
        }

        var response = await browser.LoginAsync("nobody", "wrong-password");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}

public class LowPasswordChangeLimitFactory : ManageGamesFactory
{
    protected override int PasswordChangePermitLimit => 3;
}

public class PasswordChangeRateLimitTests(LowPasswordChangeLimitFactory factory) : IClassFixture<LowPasswordChangeLimitFactory>
{
    [Fact]
    public async Task ChangePassword_IsThrottledPerUser_AfterTooManyAttempts()
    {
        var attacker = await factory.SignInAsync(await factory.CreateUserAsync());
        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.Equal(HttpStatusCode.OK, (await attacker.ChangePasswordAsync($"guess-{attempt}", "Taken-Over-Password-1")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await attacker.ChangePasswordAsync("guess-3", "Taken-Over-Password-1")).StatusCode);

        // Other users are not affected.
        var other = await factory.CreateUserAsync();
        var otherBrowser = await factory.SignInAsync(other);
        Browser.AssertRedirect(await otherBrowser.ChangePasswordAsync(other.Password, "Another-Password-1"), "/Games");
    }
}
