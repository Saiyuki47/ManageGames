namespace ManageGames.Models;

/// <summary>A game in a user's collection or on their wishlist.</summary>
public class Game : ITimestamped
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Copies { get; set; }
    public bool IsOnWishList { get; set; }
    public int? ConsoleId { get; set; }
    public GameConsole? Console { get; set; }
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
