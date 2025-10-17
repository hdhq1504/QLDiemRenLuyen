using System.ComponentModel.DataAnnotations;

namespace QLDiemRenLuyen.ViewModels
{
    public class RegisterVM
    {
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required, MinLength(6)] public string Password { get; set; } = "";
        [Required] public string FullName { get; set; } = "";
        public string RoleName { get; set; } = "STUDENT";
    }

    public class LoginVM
    {
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required] public string Password { get; set; } = "";
        public bool RememberMe { get; set; }
    }
}
