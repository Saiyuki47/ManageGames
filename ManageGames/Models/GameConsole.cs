namespace ManageGames.Models;

/// <summary>A console or handheld, shared by all users. Not named Console to avoid clashing with System.Console.</summary>
public class GameConsole : ITimestamped
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
