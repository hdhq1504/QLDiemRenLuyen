using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QLDiemRenLuyen.Admin.Data;
using QLDiemRenLuyen.Admin.Models.ViewModels;
using QLDiemRenLuyen.Models.ViewModels;

namespace QLDiemRenLuyen.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "ADMIN,STAFF,ORGANIZER")]
    [Route("[area]/registrations")]
    public class AdminRegistrationsController : Controller
    {
        private readonly AdminRegistrationsRepository _registrationsRepository;
        private readonly AdminActivitiesRepository _activitiesRepository;

        public AdminRegistrationsController(AdminRegistrationsRepository registrationsRepository, AdminActivitiesRepository activitiesRepository)
        {
            _registrationsRepository = registrationsRepository;
            _activitiesRepository = activitiesRepository;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? activityId, string? status, string? q, int page = 1, int pageSize = 20)
        {
            ViewData["Title"] = "Đăng ký & tham gia";
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 10, 100);

            if (string.IsNullOrWhiteSpace(activityId))
            {
                return View(new AdminRegistrationsVm
                {
                    Items = new PagedList<RegistrationRowVm>
                    {
                        Data = Array.Empty<RegistrationRowVm>(),
                        Page = 1,
                        PageSize = pageSize,
                        TotalItems = 0,
                        TotalPages = 0
                    },
                    Status = string.IsNullOrWhiteSpace(status) ? "ALL" : status
                });
            }

            var adminId = GetAdminId();
            var role = GetRole();
            var activity = await _activitiesRepository.GetByIdAsync(activityId, adminId, role);
            if (activity == null)
            {
                return NotFound();
            }

            var (items, total) = await _registrationsRepository.GetByActivityAsync(activityId, status, q, page, pageSize);
            var paged = new PagedList<RegistrationRowVm>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            };

            var vm = new AdminRegistrationsVm
            {
                ActivityId = activityId,
                ActivityTitle = activity.Title,
                Status = string.IsNullOrWhiteSpace(status) ? "ALL" : status!,
                Keyword = q,
                Items = paged
            };

            return View(vm);
        }

        [HttpPost("cancel/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(string id)
        {
            var ok = await _registrationsRepository.UpdateStatusAsync(id, "CANCELLED");
            return Json(new { ok, message = ok ? "Đã huỷ đăng ký." : "Không thể huỷ đăng ký." });
        }

        [HttpPost("reregister/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reregister(string id)
        {
            var ok = await _registrationsRepository.UpdateStatusAsync(id, "REGISTERED");
            return Json(new { ok, message = ok ? "Đã khôi phục đăng ký." : "Không thể cập nhật." });
        }

        [HttpGet("exportcsv")]
        public async Task<IActionResult> ExportCsv(string activityId)
        {
            var adminId = GetAdminId();
            var role = GetRole();
            var activity = await _activitiesRepository.GetByIdAsync(activityId, adminId, role);
            if (activity == null)
            {
                return NotFound();
            }

            var rows = await _registrationsRepository.ExportAsync(activityId);
            var builder = new StringBuilder();
            builder.AppendLine("StudentId,FullName,Email,Status,RegisteredAt,CheckedInAt");

            foreach (var row in rows)
            {
                builder.AppendLine(string.Join(',', new[]
                {
                    EscapeCsv(row.StudentId),
                    EscapeCsv(row.FullName),
                    EscapeCsv(row.Email),
                    EscapeCsv(row.Status),
                    EscapeCsv(FormatDate(row.RegisteredAt)),
                    EscapeCsv(FormatDate(row.CheckedInAt))
                }));
            }

            var fileName = $"registrations_{activityId}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", fileName);
        }

        private string EscapeCsv(string? input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            if (input.Contains('"') || input.Contains(','))
            {
                return $"\"{input.Replace("\"", "\"\"")}\"";
            }

            return input;
        }

        private string FormatDate(DateTime? date)
        {
            return date.HasValue ? date.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : string.Empty;
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
