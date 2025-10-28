using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QLDiemRenLuyen.Data;
using QLDiemRenLuyen.Models.ViewModels;

namespace QLDiemRenLuyen.Controllers
{
    /// <summary>
    /// Controller quản lý trang danh sách hoạt động cho sinh viên.
    /// </summary>
    [Authorize(Roles = "STUDENT")]
    [Route("student/activities")]
    public class StudentActivitiesController : Controller
    {
        private readonly StudentActivitiesRepository _repository;
        private readonly ILogger<StudentActivitiesController> _logger;

        public StudentActivitiesController(StudentActivitiesRepository repository, ILogger<StudentActivitiesController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? termId, string? q, int page = 1, int pageSize = 12)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Forbid();
            }

            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? 12 : pageSize;
            var keyword = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

            var terms = await _repository.GetTermsAsync();
            var pagedResult = await _repository.SearchActivitiesAsync(termId, keyword, page, pageSize, studentId);

            var model = new StudentActivitiesVm
            {
                SelectedTermId = termId,
                Keyword = keyword,
                Terms = terms,
                Items = pagedResult
            };

            return View(model);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Detail(string id)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            var activity = await _repository.GetActivityAsync(id, studentId);
            if (activity == null)
            {
                return NotFound();
            }

            return PartialView("_DetailModal", activity);
        }

        [HttpPost("{id}/register")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string id)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            var activity = await _repository.GetActivityAsync(id, studentId);
            if (activity == null)
            {
                return Json(new { ok = false, message = "Hoạt động không tồn tại." });
            }

            if (!string.Equals(activity.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { ok = false, message = "Hoạt động không còn mở đăng ký." });
            }

            var now = DateTime.Now;
            if (activity.StartAt > now || activity.EndAt < now)
            {
                return Json(new { ok = false, message = "Ngoài khung thời gian đăng ký." });
            }

            if (string.Equals(activity.StudentState, "REGISTERED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(activity.StudentState, "CHECKED_IN", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { ok = false, message = "Bạn đã tham gia hoạt động này." });
            }

            if (activity.MaxSeats.HasValue && activity.RegisteredCount >= activity.MaxSeats.Value)
            {
                return Json(new { ok = false, message = "Hoạt động đã đủ số lượng." });
            }

            var success = await _repository.RegisterAsync(id, studentId);
            if (!success)
            {
                _logger.LogWarning("Đăng ký hoạt động thất bại cho sinh viên {Student} - hoạt động {Activity}", studentId, id);
                return Json(new { ok = false, message = "Không thể đăng ký. Vui lòng thử lại." });
            }

            _logger.LogInformation("Sinh viên {Student} đăng ký hoạt động {Activity} thành công", studentId, id);
            var updated = await _repository.GetActivityAsync(id, studentId);
            return Json(new
            {
                ok = true,
                message = "Đăng ký thành công!",
                activity = updated == null ? null : new
                {
                    id = updated.Id,
                    status = updated.Status,
                    registeredCount = updated.RegisteredCount,
                    studentState = updated.StudentState,
                    maxSeats = updated.MaxSeats
                }
            });
        }

        [HttpPost("{id}/unregister")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unregister(string id)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            var activity = await _repository.GetActivityAsync(id, studentId);
            if (activity == null)
            {
                return Json(new { ok = false, message = "Hoạt động không tồn tại." });
            }

            if (!string.Equals(activity.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { ok = false, message = "Không thể hủy vì hoạt động đã đóng." });
            }

            if (!string.Equals(activity.StudentState, "REGISTERED", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { ok = false, message = "Bạn chưa đăng ký hoạt động này." });
            }

            var now = DateTime.Now;
            if (activity.StartAt <= now)
            {
                return Json(new { ok = false, message = "Không thể hủy khi hoạt động đã bắt đầu." });
            }

            var success = await _repository.UnregisterAsync(id, studentId);
            if (!success)
            {
                _logger.LogWarning("Hủy đăng ký thất bại cho sinh viên {Student} - hoạt động {Activity}", studentId, id);
                return Json(new { ok = false, message = "Không thể hủy đăng ký. Vui lòng thử lại." });
            }

            _logger.LogInformation("Sinh viên {Student} hủy đăng ký hoạt động {Activity}", studentId, id);
            var updated = await _repository.GetActivityAsync(id, studentId);
            return Json(new
            {
                ok = true,
                message = "Đã hủy đăng ký.",
                activity = updated == null ? null : new
                {
                    id = updated.Id,
                    status = updated.Status,
                    registeredCount = updated.RegisteredCount,
                    studentState = updated.StudentState,
                    maxSeats = updated.MaxSeats
                }
            });
        }

        private string? GetStudentId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("mand")?.Value;
        }
    }
}
