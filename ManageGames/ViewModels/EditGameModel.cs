using ManageGames.Models;

namespace ManageGames.ViewModels
{
    public class EditGameModel
    { 
        public GameModel Game { get; set; }
        public List<ConsoleModel> ConsoleList { get; set; }

    }
}
