using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLDiemRenLuyen.Data.Student;
using QLDiemRenLuyen.Models.ViewModels.Student;

namespace QLDiemRenLuyen.Controllers.Student
{
    /// <summary>
    /// Controller phục vụ trang hồ sơ của sinh viên.
    /// </summary>
    [Authorize(Roles = "STUDENT")]
    [Route("student/profile")]
    public class StudentProfileController : Controller
    {
        private static readonly string[] AllowedAvatarExtensions = new[] { ".jpg", ".jpeg", ".png" };
        private const long MaxAvatarSize = 2 * 1024 * 1024; // 2MB

        private readonly StudentProfileRepository _repository;
        private readonly ILogger<StudentProfileController> _logger;
        private readonly IWebHostEnvironment _environment;

        public StudentProfileController(StudentProfileRepository repository, ILogger<StudentProfileController> logger, IWebHostEnvironment environment)
        {
            _repository = repository;
            _logger = logger;
            _environment = environment;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Forbid();
            }

            var profile = await _repository.GetAsync(studentId);
            if (profile == null)
            {
                _logger.LogWarning("Không tìm thấy hồ sơ cho sinh viên {StudentId}", studentId);
                return View("~/Views/Student/StudentProfile/Index.cshtml", new StudentProfileVm
                {
                    StudentId = studentId,
                    Email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                    FullName = User.Identity?.Name ?? string.Empty,
                    RoleName = User.FindFirstValue(ClaimTypes.Role) ?? "STUDENT",
                    Classes = await _repository.GetClassesAsync(),
                    Departments = await _repository.GetDepartmentsAsync()
                });
            }

            return View("~/Views/Student/StudentProfile/Index.cshtml", profile);
        }

        [HttpGet("edit")]
        public async Task<IActionResult> Edit()
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            var profile = await _repository.GetAsync(studentId);
            if (profile == null)
            {
                var classes = await _repository.GetClassesAsync();
                var departments = await _repository.GetDepartmentsAsync();
                profile = new StudentProfileVm
                {
                    StudentId = studentId,
                    Email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                    FullName = User.Identity?.Name ?? string.Empty,
                    RoleName = User.FindFirstValue(ClaimTypes.Role) ?? "STUDENT",
                    Classes = classes,
                    Departments = departments
                };
            }
            else
            {
                profile.Classes ??= await _repository.GetClassesAsync();
                profile.Departments ??= await _repository.GetDepartmentsAsync();
            }

            return PartialView("~/Views/Student/StudentProfile/_EditModal.cshtml", profile);
        }

        [HttpPost("edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([FromForm] StudentProfileEditVm input)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            // Chuẩn hóa dữ liệu nhập.
            input.FullName = input.FullName?.Trim() ?? string.Empty;
            input.StudentCode = string.IsNullOrWhiteSpace(input.StudentCode) ? null : input.StudentCode.Trim();
            input.Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim();
            input.Address = string.IsNullOrWhiteSpace(input.Address) ? null : input.Address.Trim();
            input.Gender = string.IsNullOrWhiteSpace(input.Gender) ? null : input.Gender.Trim();

            if (input.Dob.HasValue)
            {
                var dob = input.Dob.Value.Date;
                if (dob < new DateTime(1900, 1, 1) || dob > DateTime.Today)
                {
                    ModelState.AddModelError(nameof(input.Dob), "Ngày sinh không hợp lệ");
                }
                else
                {
                    input.Dob = dob;
                }
            }

            if (input.Gender != null)
            {
                var allowed = new[] { "MALE", "FEMALE", "OTHER" };
                if (allowed.Contains(input.Gender, StringComparer.OrdinalIgnoreCase))
                {
                    input.Gender = input.Gender.ToUpperInvariant();
                }
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage)).ToList();
                return BadRequest(new { ok = false, message = string.Join("\n", errors) });
            }

            var before = await _repository.GetAsync(studentId);
            var classes = before?.Classes ?? await _repository.GetClassesAsync();
            var departments = before?.Departments ?? await _repository.GetDepartmentsAsync();
            var baseline = before ?? new StudentProfileVm
            {
                StudentId = studentId,
                Email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                FullName = User.Identity?.Name ?? string.Empty,
                RoleName = User.FindFirstValue(ClaimTypes.Role) ?? "STUDENT",
                Classes = classes,
                Departments = departments
            };

            var success = await _repository.UpdateAsync(input, studentId);
            if (!success)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { ok = false, message = "Cập nhật thất bại" });
            }

            var changedFields = CalculateChangedFields(before, input);
            await _repository.WriteAuditAsync(studentId, "PROFILE_UPDATE", GetClientIp(), GetUserAgent(), changedFields);

            var refreshed = await _repository.GetAsync(studentId);
            var responseProfile = refreshed ?? new StudentProfileVm
            {
                StudentId = baseline.StudentId,
                Email = baseline.Email,
                FullName = input.FullName,
                RoleName = baseline.RoleName,
                StudentCode = input.StudentCode,
                ClassId = input.ClassId,
                DepartmentId = input.DepartmentId,
                Dob = input.Dob,
                Gender = input.Gender,
                Phone = input.Phone,
                Address = input.Address,
                ClassName = classes.FirstOrDefault(c => c.Id == input.ClassId)?.Name,
                DepartmentName = departments.FirstOrDefault(d => d.Id == input.DepartmentId)?.Name,
                Classes = classes,
                Departments = departments
            };
            return Json(new
            {
                ok = true,
                message = "Cập nhật hồ sơ thành công",
                profile = new
                {
                    fullName = responseProfile.FullName,
                    email = responseProfile.Email,
                    roleName = responseProfile.RoleName,
                    studentCode = responseProfile.StudentCode,
                    classId = responseProfile.ClassId,
                    className = responseProfile.ClassName,
                    departmentId = responseProfile.DepartmentId,
                    departmentName = responseProfile.DepartmentName,
                    dob = responseProfile.Dob?.ToString("yyyy-MM-dd"),
                    gender = responseProfile.Gender,
                    phone = responseProfile.Phone,
                    address = responseProfile.Address
                }
            });
        }

        [HttpPost("avatar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile? avatar)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            if (avatar == null || avatar.Length == 0)
            {
                return BadRequest(new { ok = false, message = "Vui lòng chọn ảnh hợp lệ" });
            }

            if (avatar.Length > MaxAvatarSize)
            {
                return BadRequest(new { ok = false, message = "Ảnh vượt quá 2MB" });
            }

            var ext = Path.GetExtension(avatar.FileName).ToLowerInvariant();
            if (!AllowedAvatarExtensions.Contains(ext))
            {
                return BadRequest(new { ok = false, message = "Định dạng ảnh không hỗ trợ" });
            }

            var webRoot = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
            {
                _logger.LogError("WebRootPath không khả dụng khi lưu avatar cho {StudentId}", studentId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { ok = false, message = "Máy chủ chưa cấu hình thư mục tĩnh" });
            }

            var uploadsDir = Path.Combine(webRoot, "uploads", "avatars");
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            var fileName = $"{studentId}_{DateTime.UtcNow.Ticks}{ext}";
            var physicalPath = Path.Combine(uploadsDir, fileName);
            try
            {
                await using (var stream = System.IO.File.Create(physicalPath))
                {
                    await avatar.CopyToAsync(stream);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể lưu tệp avatar cho sinh viên {StudentId}", studentId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { ok = false, message = "Không thể lưu ảnh" });
            }

            var relativePath = $"/uploads/avatars/{fileName}";
            var success = await _repository.UpdateAvatarAsync(studentId, relativePath);
            if (!success)
            {
                System.IO.File.Delete(physicalPath);
                return StatusCode(StatusCodes.Status500InternalServerError, new { ok = false, message = "Không thể cập nhật ảnh" });
            }

            await _repository.WriteAuditAsync(studentId, "AVATAR_UPDATE", GetClientIp(), GetUserAgent(), new[] { "avatar" });

            return Json(new { ok = true, message = "Đổi ảnh thành công", avatarUrl = relativePath });
        }

        private string? GetStudentId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private string GetClientIp()
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            return string.IsNullOrWhiteSpace(ip) ? "unknown" : ip;
        }

        private string GetUserAgent()
        {
            var ua = Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(ua) ? "unknown" : ua;
        }

        /// <summary>
        /// So sánh dữ liệu trước và sau để log audit.
        /// </summary>
        private static IEnumerable<string> CalculateChangedFields(StudentProfileVm? before, StudentProfileEditVm after)
        {
            before ??= new StudentProfileVm();
            var changed = new List<string>();

            if (!string.Equals(before.FullName?.Trim(), after.FullName?.Trim(), StringComparison.Ordinal)) changed.Add("fullName");
            if (!string.Equals(before.StudentCode?.Trim(), after.StudentCode?.Trim(), StringComparison.Ordinal)) changed.Add("studentCode");
            if (before.ClassId != after.ClassId) changed.Add("classId");
            if (before.DepartmentId != after.DepartmentId) changed.Add("departmentId");
            if (!Nullable.Equals(before.Dob, after.Dob)) changed.Add("dob");
            if (!string.Equals(before.Gender, after.Gender, StringComparison.OrdinalIgnoreCase)) changed.Add("gender");
            if (!string.Equals(before.Phone, after.Phone, StringComparison.Ordinal)) changed.Add("phone");
            if (!string.Equals(before.Address, after.Address, StringComparison.Ordinal)) changed.Add("address");

            return changed;
        }
    }
}
