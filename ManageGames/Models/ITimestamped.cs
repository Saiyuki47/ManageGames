namespace ManageGames.Models;

/// <summary>
/// Entities carrying automatic audit timestamps. Values are set centrally in
/// <see cref="ManageGames.Data.AppDbContext.SaveChanges(bool)"/> — never by hand in services.
/// </summary>
public interface ITimestamped
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
