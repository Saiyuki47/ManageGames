using System.Net;
using System.Text.RegularExpressions;

namespace ManageGames.Tests.Infrastructure
{
    /// <summary>Browser-like helpers on top of the test client: redirects, forms and antiforgery tokens.</summary>
    public static partial class Browser
    {
        public const string AuthCookieName = "ManageGames.Auth";

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
            var html = await client.GetPageAsync(formPage);
            var token = AntiforgeryToken().Match(html);
            Assert.True(token.Success, $"No antiforgery token found on {formPage}.");

            var content = new Dictionary<string, string>(fields ?? new Dictionary<string, string>())
            {
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value),
            };
            return await client.PostAsync(action, new FormUrlEncodedContent(content));
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
                ["Copies"] = copies.ToString(),
                ["ConsoleId"] = consoleId?.ToString() ?? string.Empty,
                ["OnWishList"] = onWishList ? "true" : "false",
            });
        }

        /// <summary>The value of the auth cookie a login response set, for replaying it later.</summary>
        public static string GetAuthCookie(HttpResponseMessage response)
        {
            var header = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(AuthCookieName + "="));
            return header.Split(';')[0];
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
}
