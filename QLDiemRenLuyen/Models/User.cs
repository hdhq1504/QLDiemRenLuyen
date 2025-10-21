namespace QLDiemRenLuyen.Models
{
    public class User
    {
        public string MaND { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
        public string RoleName { get; set; } = "STUDENT";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public int FailedLoginCount { get; set; }
        public DateTime? LockoutEndUtc { get; set; }
    }
}
