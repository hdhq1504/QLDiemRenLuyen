using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using QLDiemRenLuyen.Data;
using QLDiemRenLuyen.Models;
using QLDiemRenLuyen.Services;
using QLDiemRenLuyen.ViewModels;
using System.Reflection.Emit;
using System.Security.Claims;

namespace QLDiemRenLuyen.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserRepository _repo;
        private readonly IHttpContextAccessor _http;
        public AccountController(UserRepository repo, IHttpContextAccessor? http = null)
        {
            _repo = repo;
            _http = http ?? new HttpContextAccessor();
        }

        [HttpGet]
        public IActionResult Register() => View(new RegisterVM());

        [HttpPost]
        public async Task<IActionResult> Register(RegisterVM vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var existed = await _repo.GetByEmailAsync(vm.Email);
            if (existed != null)
            {
                ModelState.AddModelError(nameof(vm.Email), "Email đã tồn tại.");
                return View(vm);
            }

            var (hash, salt) = PasswordHasher.HashPassword(vm.Password);
            var user = new User
            {
                MaND = IdGenerator.NewId(),   // <== thêm dòng này
                Email = vm.Email,
                FullName = vm.FullName,
                RoleName = vm.RoleName,
                PasswordHash = hash,
                PasswordSalt = salt
            };
            await _repo.CreateAsync(user);
            await _repo.AuditAsync(vm.Email, "REGISTER", GetIp(), GetUA());

            TempData["msg"] = "Đăng ký thành công. Hãy đăng nhập.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult Login() => View(new LoginVM());

        [HttpPost]
        public async Task<IActionResult> Login(LoginVM vm, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(vm);
            var user = await _repo.GetByEmailAsync(vm.Email);

            if (user == null || !user.IsActive || !PasswordHasher.Verify(vm.Password, user.PasswordSalt, user.PasswordHash))
            {
                await _repo.AuditAsync(vm.Email, "LOGIN_FAILED", GetIp(), GetUA());
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
                return View(vm);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.MaND),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.RoleName)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var props = new AuthenticationProperties { IsPersistent = vm.RememberMe };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
            await _repo.AuditAsync(user.Email, "LOGIN_SUCCESS", GetIp(), GetUA());

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            var email = User?.FindFirstValue(ClaimTypes.Email);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!string.IsNullOrEmpty(email))
                await _repo.AuditAsync(email!, "LOGOUT", GetIp(), GetUA());
            return RedirectToAction(nameof(Login));
        }

        public IActionResult AccessDenied() => View();

        private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
        private string? GetUA() => Request.Headers.UserAgent.ToString();
    }
}
