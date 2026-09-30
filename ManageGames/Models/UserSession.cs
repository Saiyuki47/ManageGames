namespace ManageGames.Models;

/// <summary>
/// A signed-in browser. The auth cookie carries the session id; logging out deletes the row, so a copy
/// of that cookie is worthless afterwards, while the user's other devices stay signed in.
/// </summary>
public class UserSession
{
    public string Id { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    // Absolute end of the session, however active it is (the cookie itself expires after inactivity).
    public DateTime ExpiresAt { get; set; }
}
