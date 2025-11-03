using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QLDiemRenLuyen.Data.Admin;
using QLDiemRenLuyen.Models.ViewModels.Admin;
using QLDiemRenLuyen.Models.ViewModels;

namespace QLDiemRenLuyen.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "ADMIN,STAFF,ORGANIZER")]
    [Route("[area]/attendance")]
    public class AdminAttendanceController : Controller
    {
        private readonly AdminAttendanceRepository _attendanceRepository;
        private readonly AdminActivitiesRepository _activitiesRepository;
        private readonly ILogger<AdminAttendanceController> _logger;

        public AdminAttendanceController(AdminAttendanceRepository attendanceRepository, AdminActivitiesRepository activitiesRepository, ILogger<AdminAttendanceController> logger)
        {
            _attendanceRepository = attendanceRepository;
            _activitiesRepository = activitiesRepository;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? activityId, string? q, int page = 1, int pageSize = 50)
        {
            ViewData["Title"] = "Điểm danh";
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 10, 200);

            if (string.IsNullOrWhiteSpace(activityId))
            {
                return View(new AdminAttendanceVm
                {
                    Items = new PagedList<AttendanceRowVm>
                    {
                        Data = Array.Empty<AttendanceRowVm>(),
                        Page = 1,
                        PageSize = pageSize,
                        TotalItems = 0,
                        TotalPages = 0
                    }
                });
            }

            var adminId = GetAdminId();
            var role = GetRole();
            var activity = await _activitiesRepository.GetByIdAsync(activityId, adminId, role);
            if (activity == null)
            {
                return NotFound();
            }

            var (items, total) = await _attendanceRepository.GetByActivityAsync(activityId, q, page, pageSize);
            var paged = new PagedList<AttendanceRowVm>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            };

            var vm = new AdminAttendanceVm
            {
                ActivityId = activityId,
                ActivityTitle = activity.Title,
                Keyword = q,
                Items = paged
            };

            return View(vm);
        }

        [HttpPost("mark/{registrationId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Mark(string registrationId)
        {
            var ok = await _attendanceRepository.MarkAsync(registrationId, true);
            return Json(new { ok, message = ok ? "Đã điểm danh." : "Không thể điểm danh." });
        }

        [HttpPost("unmark/{registrationId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unmark(string registrationId)
        {
            var ok = await _attendanceRepository.MarkAsync(registrationId, false);
            return Json(new { ok, message = ok ? "Đã bỏ điểm danh." : "Không thể cập nhật." });
        }

        [HttpPost("importcsv")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportCsv(string activityId, IFormFile? csvFile)
        {
            if (string.IsNullOrWhiteSpace(activityId))
            {
                return Json(new { ok = false, message = "Thiếu hoạt động." });
            }

            if (csvFile == null || csvFile.Length == 0)
            {
                return Json(new { ok = false, message = "Tập tin không hợp lệ." });
            }

            var identifiers = new List<string>();

            try
            {
                using var reader = new StreamReader(csvFile.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (line.Contains("student", StringComparison.OrdinalIgnoreCase) && line.Contains("email", StringComparison.OrdinalIgnoreCase))
                    {
                        // Bỏ qua dòng tiêu đề.
                        continue;
                    }

                    var parts = line.Split(new[] { ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        var value = part.Trim();
                        if (!string.IsNullOrEmpty(value))
                        {
                            identifiers.Add(value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể đọc tập tin CSV điểm danh");
                return Json(new { ok = false, message = "Không thể đọc dữ liệu." });
            }

            var count = await _attendanceRepository.ImportAsync(activityId, identifiers);
            return Json(new { ok = true, message = $"Đã điểm danh {count} sinh viên." });
        }

        private string GetAdminId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("mand") ?? string.Empty;
        }

        private string GetRole()
        {
            return User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        }
    }
}
