using Microsoft.AspNetCore.Identity;

namespace ManageGames.Models;

/// <summary>A user account, stored and managed by ASP.NET Core Identity.</summary>
public class AppUser : IdentityUser<Guid>, ITimestamped
{
    // Set for generated, admin-assigned and reset passwords, and for passwords that no longer meet the
    // policy: the user has to choose a new password before they can use the app.
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
