namespace ManageGames.Models
{
    public class CompanyModel : ITimestamped
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
