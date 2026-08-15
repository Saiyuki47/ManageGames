using ManageGames.Models;

namespace ManageGames.ViewModels
{
    public class AddEditCategory
    {
        public ConsoleModel Console { get; set; }
        public List<CompanyModel> CompanyList { get; set; } = new();
    }
}
