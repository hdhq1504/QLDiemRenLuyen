using System.ComponentModel.DataAnnotations;

namespace QLDiemRenLuyen.ViewModels.Auth
{
    /// <summary>
    /// Quy tắc mật khẩu dùng chung cho các màn hình xác thực.
    /// </summary>
    public static class PasswordRules
    {
        public const string Pattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$";
        public const string Message = "Mật khẩu phải có tối thiểu 8 ký tự, bao gồm chữ hoa, chữ thường và số.";
    }

    /// <summary>
    /// ViewModel dùng cho màn hình đăng ký tài khoản.
    /// </summary>
    public class RegisterVm
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

    /// <summary>
    /// ViewModel đăng nhập.
    /// </summary>
    public class LoginVm
    {
        [Required(ErrorMessage = "Email là bắt buộc"), EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu là bắt buộc"), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    /// <summary>
    /// ViewModel quên mật khẩu.
    /// </summary>
    public class ForgotPasswordVm
    {
        [Required(ErrorMessage = "Email là bắt buộc"), EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// ViewModel đổi mật khẩu.
    /// </summary>
    public class ChangePasswordVm
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
