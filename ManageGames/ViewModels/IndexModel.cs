using ManageGames.Models;

namespace ManageGames.ViewModels
{
    public class IndexModel
    {
        public List<GameModel> GamesList { get; set; }
        public List<ConsoleModel> ConsoleList { get; set; }
        public string UserName { get; set; }

         
    }
}
