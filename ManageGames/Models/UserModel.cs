namespace ManageGames.Models
{
    public class UserModel : ITimestamped
    {
        public Guid UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        // Lower-cased, space-stripped copy of Username. Carries the unique index and is used
        // for the login lookup, so "Max", "max" and "m ax" all resolve to the same account.
        public string NormalizedUsername { get; set; } = string.Empty;
        // Salted hash produced by PasswordHasher, never the plaintext password.
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        // Set for generated, admin-assigned and reset passwords: the user has to choose their
        // own password before they can use the app.
        public bool MustChangePassword { get; set; }
        // Copied into the auth cookie at sign-in. Rotating it (logout, password change or reset)
        // revokes every session issued before, server-side.
        public string SecurityStamp { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
