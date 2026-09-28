namespace ManageGames.Models
{
    public class ConsoleModel : ITimestamped
    {
        public int ConsoleId { get; set; }
        public string ConsoleName { get; set; } = string.Empty;
        public int? CompanyId { get; set; }
        public CompanyModel? Company { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
