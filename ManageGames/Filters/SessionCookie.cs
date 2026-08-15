namespace ManageGames.Filters
{
    /// <summary>
    /// Parsing helpers for the bespoke "GameSort+" session cookie, whose value is
    /// "{userId}+{cookieId}". Never throws on missing or malformed input — callers get
    /// <c>false</c> instead of an exception, so a bad cookie can't crash a page.
    /// </summary>
    public static class SessionCookie
    {
        public const string CookieName = "GameSort+";

        /// <summary>HttpContext.Items key under which RequireLogin stores the validated user id.</summary>
        public const string UserIdItemKey = "UserId";

        public static bool TryParse(string? cookieValue, out Guid userId, out string cookieId)
        {
            userId = Guid.Empty;
            cookieId = string.Empty;
            if (string.IsNullOrEmpty(cookieValue))
            {
                return false;
            }

            var parts = cookieValue.Split('+');
            if (parts.Length < 2 || !Guid.TryParse(parts[0], out userId))
            {
                return false;
            }

            cookieId = parts[1];
            return true;
        }
    }
}
