namespace ManageGames.Models
{
    public class UserModel : ITimestamped
    {
        public Guid UserID { get; set; }
        public Guid ProfilePicturesID { get; set; }
        public string Username { get; set; }
        // Lower-cased, space-stripped copy of Username. Carries the unique index and is used
        // for the login lookup, so "Max", "max" and "m ax" all resolve to the same account.
        public string NormalizedUsername { get; set; }
        public string Password { get; set; }
        public bool IsAdmin { get; set; }
        public string? CookieID { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
