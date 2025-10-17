namespace QLDiemRenLuyen.Models
{
    public class User
    {
        public string MaND { get; set; } = "";
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string PasswordSalt { get; set; } = "";
        public string RoleName { get; set; } = "STUDENT";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}
