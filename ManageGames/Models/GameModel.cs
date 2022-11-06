namespace ManageGames.Models
{
    public class GameModel
    {
        public int GameId { get; set; }
        public ConsoleModel Console { get; set; }
        public string GameName { get; set; }
        public int Anzah_von_Game { get; set; }
        public bool IsOnWishList { get; set; }
        public UserModel User { get; set; }
    }
}
