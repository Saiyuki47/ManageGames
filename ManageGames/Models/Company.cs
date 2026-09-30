namespace ManageGames.Models;

/// <summary>A console maker, shared by all users.</summary>
public class Company : ITimestamped
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
