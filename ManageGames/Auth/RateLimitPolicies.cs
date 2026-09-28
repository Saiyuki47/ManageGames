namespace ManageGames.Auth
{
    public static class RateLimitPolicies
    {
        /// <summary>Throttles login attempts per client IP to slow down password guessing.</summary>
        public const string Login = "login";
    }
}
