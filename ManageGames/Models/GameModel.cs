namespace ManageGames.Models
{
    public class GameModel : ITimestamped
    {
        public int GameId { get; set; }
        public int? ConsoleId { get; set; }
        public ConsoleModel? Console { get; set; }
        public string GameName { get; set; }
        public int Copies { get; set; }
        public bool IsOnWishList { get; set; }
        public Guid UserId { get; set; }
        public UserModel User { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
