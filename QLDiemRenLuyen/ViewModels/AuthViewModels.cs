using System.ComponentModel.DataAnnotations;

namespace QLDiemRenLuyen.ViewModels
{
    public static class PasswordRules
    {
        public const string Pattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$";
        public const string Message = "Mật khẩu phải có tối thiểu 8 ký tự, bao gồm chữ hoa, chữ thường và số.";
    }

    public class RegisterVM
    {
        [Required(ErrorMessage = "Email là bắt buộc"), EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Họ và tên là bắt buộc"), MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu là bắt buộc"), DataType(DataType.Password),
         RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.Message)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc"), DataType(DataType.Password),
         Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class LoginVM
    {
        [Required(ErrorMessage = "Email là bắt buộc"), EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu là bắt buộc"), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public class ForgotPasswordVM
    {
        [Required(ErrorMessage = "Email là bắt buộc"), EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;
    }

    public class ChangePasswordVM
    {
        [Required(ErrorMessage = "Mật khẩu hiện tại là bắt buộc"), DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu mới là bắt buộc"), DataType(DataType.Password),
         RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.Message)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Xác nhận mật khẩu mới là bắt buộc"), DataType(DataType.Password),
         Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
