using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace ManageGames.Tests.Infrastructure;

/// <summary>Browser-like helpers on top of the test client: redirects, forms and antiforgery tokens.</summary>
public static partial class Browser
{
    public const string AuthCookieName = "__Host-ManageGames.Auth";

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryToken();

    /// <summary>GETs a page, follows redirects like a browser does and returns the final HTML.</summary>
    public static async Task<string> GetPageAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        for (var hops = 0; hops < 5 && IsRedirect(response); hops++)
        {
            response = await client.GetAsync(response.Headers.Location);
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Submits a form like a browser: loads <paramref name="formPage"/> for a fresh antiforgery
    /// token, then posts the fields to <paramref name="action"/>.
    /// </summary>
    public static async Task<HttpResponseMessage> SubmitFormAsync(
        this HttpClient client, string formPage, string action, IDictionary<string, string>? fields = null)
    {
        var token = await client.GetFormTokenAsync(formPage);
        return await client.PostFormAsync(action, token, fields);
    }

    /// <summary>The antiforgery token of the form on <paramref name="formPage"/>, as the browser got it then.</summary>
    public static async Task<string> GetFormTokenAsync(this HttpClient client, string formPage)
    {
        var html = await client.GetPageAsync(formPage);
        var token = AntiforgeryToken().Match(html);
        Assert.True(token.Success, $"No antiforgery token found on {formPage}.");
        return WebUtility.HtmlDecode(token.Groups[1].Value);
    }

    /// <summary>Posts a form with a token loaded earlier, like a page that stayed open for a while.</summary>
    public static Task<HttpResponseMessage> PostFormAsync(
        this HttpClient client, string action, string token, IDictionary<string, string>? fields = null)
    {
        var content = new Dictionary<string, string>(fields ?? new Dictionary<string, string>())
        {
            ["__RequestVerificationToken"] = token,
        };
        return client.PostAsync(action, new FormUrlEncodedContent(content));
    }

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string username, string password, string? returnUrl = null)
    {
        var fields = new Dictionary<string, string> { ["Username"] = username, ["Password"] = password };
        if (returnUrl != null)
        {
            fields["ReturnUrl"] = returnUrl;
        }
        return client.SubmitFormAsync("/Home/Privacy", "/Account/Login", fields);
    }

    public static Task<HttpResponseMessage> LogoutAsync(this HttpClient client)
    {
        return client.SubmitFormAsync("/Home/Privacy", "/Account/Logout");
    }

    public static Task<HttpResponseMessage> ChangePasswordAsync(this HttpClient client, string currentPassword, string newPassword)
    {
        return client.SubmitFormAsync("/Account/ChangePassword", "/Account/ChangePassword", new Dictionary<string, string>
        {
            ["CurrentPassword"] = currentPassword,
            ["NewPassword"] = newPassword,
            ["ConfirmPassword"] = newPassword,
        });
    }

    public static Task<HttpResponseMessage> AddGameAsync(this HttpClient client, string name, int copies = 1, int? consoleId = null, bool onWishList = false)
    {
        return client.SubmitFormAsync("/Games/Create", "/Games/Create", new Dictionary<string, string>
        {
            ["Name"] = name,
            ["Copies"] = copies.ToString(CultureInfo.InvariantCulture),
            ["ConsoleId"] = consoleId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            ["OnWishList"] = onWishList ? "true" : "false",
        });
    }

    /// <summary>The value of the auth cookie a login response set, for replaying it later.</summary>
    public static string GetAuthCookie(HttpResponseMessage response)
    {
        var header = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(AuthCookieName + "=", StringComparison.Ordinal));
        return header.Split(';')[0];
    }

    public static bool SetsAuthCookie(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(c => c.StartsWith(AuthCookieName + "=", StringComparison.Ordinal) && !c.StartsWith(AuthCookieName + "=;", StringComparison.Ordinal));
    }

    /// <summary>The lower-cased attributes (secure, samesite=strict, ...) of a cookie the response set.</summary>
    public static List<string> CookieAttributes(HttpResponseMessage response, string cookieName)
    {
        return response.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith(cookieName + "=", StringComparison.Ordinal))
            .Split(';')
            .Skip(1)
            .Select(a => a.Trim().ToLowerInvariant())
            .ToList();
    }

    public static bool IsRedirect(HttpResponseMessage response)
    {
        return (int)response.StatusCode is >= 300 and < 400;
    }

    /// <summary>Asserts a redirect to the given path and query (the host of absolute redirects is ignored).</summary>
    public static void AssertRedirect(HttpResponseMessage response, string expectedPathAndQuery)
    {
        Assert.True(IsRedirect(response), $"Expected a redirect to {expectedPathAndQuery}, got {(int)response.StatusCode}.");
        var location = response.Headers.Location!;
        Assert.Equal(expectedPathAndQuery, location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString);
    }
}
