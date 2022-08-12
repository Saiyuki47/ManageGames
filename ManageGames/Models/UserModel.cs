namespace ManageGames.Models
{
    public class UserModel
    {
        public Guid UserID { get; set; }
        public Guid ProfilePicturesID { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool IsAdmin { get; set; }
        public string? CookieID { get; set; }
    }
}
