using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLDiemRenLuyen.Data;
using QLDiemRenLuyen.Models;
using QLDiemRenLuyen.Services;
using QLDiemRenLuyen.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;

namespace QLDiemRenLuyen.Controllers
{
    public class AccountController : Controller
    {
        // Số lần đăng nhập sai tối đa trước khi khóa tạm
        private const int MaxFailedAttempts = 5;
        // Thời gian khóa tài khoản khi vượt quá số lần sai
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        private readonly UserRepository _repo;

        public AccountController(UserRepository repo)
        {
            _repo = repo;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View("~/Views/Auth/Login.cshtml", new LoginVM());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVM vm, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
            {
                return View("~/Views/Auth/Login.cshtml", vm);
            }

            // Tra cứu tài khoản theo email
            var user = await _repo.GetByEmailAsync(vm.Email);
            if (user == null)
            {
                await _repo.AuditAsync(vm.Email, "LOGIN_FAILED", GetIp(), GetUA());
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
                return View("~/Views/Auth/Login.cshtml", vm);
            }

            // Không cho phép đăng nhập khi tài khoản bị khóa dài hạn
            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản đang bị khóa. Vui lòng liên hệ quản trị viên.");
                return View("~/Views/Auth/Login.cshtml", vm);
            }

            // Kiểm tra trạng thái bị khóa tạm do nhập sai nhiều lần
            if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value > DateTime.UtcNow)
            {
                var remaining = user.LockoutEndUtc.Value - DateTime.UtcNow;
                var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
                ModelState.AddModelError(string.Empty, $"Tài khoản đã bị khóa tạm thời. Thử lại sau {minutes} phút.");
                return View("~/Views/Auth/Login.cshtml", vm);
            }

            // So khớp mật khẩu (hash + salt)
            if (!PasswordHasher.Verify(vm.Password, user.PasswordSalt, user.PasswordHash))
            {
                var nextFailedCount = user.FailedLoginCount + 1;
                DateTime? lockoutUntil = null;

                if (nextFailedCount >= MaxFailedAttempts)
                {
                    lockoutUntil = DateTime.UtcNow.Add(LockoutDuration);
                    nextFailedCount = MaxFailedAttempts;
                }

                // Cập nhật số lần sai và ghi log audit
                await _repo.UpdateLoginAttemptsAsync(user.Email, nextFailedCount, lockoutUntil);
                await _repo.AuditAsync(user.Email, "LOGIN_FAILED", GetIp(), GetUA());

                var message = lockoutUntil != null
                    ? "Bạn đã nhập sai quá 5 lần. Tài khoản bị khóa trong 15 phút."
                    : "Email hoặc mật khẩu không đúng.";

                ModelState.AddModelError(string.Empty, message);
                return View("~/Views/Auth/Login.cshtml", vm);
            }

            // Đăng nhập thành công: reset bộ đếm và tạo cookie xác thực
            await _repo.UpdateLoginAttemptsAsync(user.Email, 0, null);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.MaND),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.RoleName)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var props = new AuthenticationProperties
            {
                IsPersistent = vm.RememberMe,
                AllowRefresh = true,
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = vm.RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(7)
                    : DateTimeOffset.UtcNow.AddHours(2)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
            await _repo.AuditAsync(user.Email, "LOGIN_SUCCESS", GetIp(), GetUA());

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return Redirect(GetRedirectUrlByRole(user.RoleName));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!string.IsNullOrWhiteSpace(email))
            {
                await _repo.AuditAsync(email, "LOGOUT", GetIp(), GetUA());
            }

            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View("~/Views/Auth/Register.cshtml", new RegisterVM());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterVM vm)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/Auth/Register.cshtml", vm);
            }

            // Kiểm tra trùng email trước khi tạo mới
            var existed = await _repo.GetByEmailAsync(vm.Email);
            if (existed != null)
            {
                ModelState.AddModelError(nameof(vm.Email), "Email đã tồn tại trong hệ thống.");
                return View("~/Views/Auth/Register.cshtml", vm);
            }

            // Sinh hash + salt để lưu trữ mật khẩu an toàn
            var (hash, salt) = PasswordHasher.HashPassword(vm.Password);
            var user = new User
            {
                MaND = IdGenerator.NewId(),
                Email = vm.Email.Trim(),
                FullName = vm.FullName.Trim(),
                RoleName = "STUDENT",
                PasswordHash = hash,
                PasswordSalt = salt,
                IsActive = true
            };

            await _repo.CreateAsync(user);
            await _repo.AuditAsync(user.Email, "REGISTER", GetIp(), GetUA());

            TempData["AuthSuccess"] = "Đăng ký thành công. Vui lòng đăng nhập.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View("~/Views/Auth/ForgotPassword.cshtml", new ForgotPasswordVM());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordVM vm)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/Auth/ForgotPassword.cshtml", vm);
            }

            // Chỉ lưu token khi email tồn tại, tránh lộ thông tin người dùng
            var user = await _repo.GetByEmailAsync(vm.Email);
            if (user != null)
            {
                var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
                await _repo.StorePasswordResetTokenAsync(user.Email, token, DateTime.UtcNow.AddHours(1));
                await _repo.AuditAsync(user.Email, "FORGOT_PASSWORD_REQUEST", GetIp(), GetUA());
            }

            TempData["AuthInfo"] = "Nếu email tồn tại, hướng dẫn đặt lại mật khẩu đã được gửi.";
            return RedirectToAction(nameof(Login));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordVM vm, string? returnUrl)
        {
            if (!ModelState.IsValid)
            {
                TempData["ChangePasswordError"] = string.Join(" ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                return Redirect(returnUrl ?? Request.Headers["Referer"].ToString() ?? Url.Content("~/"));
            }

            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                return Unauthorized();
            }

            var user = await _repo.GetByEmailAsync(email);
            if (user == null)
            {
                return Unauthorized();
            }

            // Xác minh mật khẩu hiện tại trước khi cập nhật
            if (!PasswordHasher.Verify(vm.CurrentPassword, user.PasswordSalt, user.PasswordHash))
            {
                TempData["ChangePasswordError"] = "Mật khẩu hiện tại không chính xác.";
                return Redirect(returnUrl ?? Request.Headers["Referer"].ToString() ?? Url.Content("~/"));
            }

            // Lưu lại mật khẩu mới và ghi nhận audit
            var (hash, salt) = PasswordHasher.HashPassword(vm.NewPassword);
            await _repo.UpdatePasswordAsync(user.MaND, hash, salt);
            await _repo.AuditAsync(email, "PASSWORD_CHANGED", GetIp(), GetUA());

            TempData["ChangePasswordSuccess"] = "Đổi mật khẩu thành công.";
            return Redirect(returnUrl ?? Request.Headers["Referer"].ToString() ?? Url.Content("~/"));
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

        private string? GetUA() => Request.Headers.UserAgent.ToString();

        private static string GetRedirectUrlByRole(string roleName)
        {
            return roleName.ToUpperInvariant() switch
            {
                "ADMIN" => "/admin/dashboard",
                "LECTURER" => "/advisor/classes",
                "ADVISOR" => "/advisor/classes",
                _ => "/student/home"
            };
        }
    }
}
