using System;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QLDiemRenLuyen.Data;
using QLDiemRenLuyen.Models.ViewModels;
using QLDiemRenLuyen.Services;

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
        private readonly IEmailSender _emailSender;

        public StudentActivitiesController(StudentActivitiesRepository repository, ILogger<StudentActivitiesController> logger, IEmailSender emailSender)
        {
            _repository = repository;
            _logger = logger;
            _emailSender = emailSender;
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
            var reminderSource = updated ?? activity;
            var emailSent = false;
            var recipient = User.FindFirstValue(ClaimTypes.Email);

            if (!string.IsNullOrWhiteSpace(recipient) && reminderSource != null)
            {
                var subject = $"[QLDRL] Nhắc nhở hoạt động {reminderSource.Title}";
                var bodyBuilder = new StringBuilder();
                bodyBuilder.Append($"<p>Chào {HtmlEncoder.Default.Encode(User.Identity?.Name ?? "bạn")},</p>");
                bodyBuilder.Append("<p>Bạn vừa đăng ký tham gia hoạt động:</p><ul>");
                bodyBuilder.Append($"<li><strong>{HtmlEncoder.Default.Encode(reminderSource.Title)}</strong></li>");
                bodyBuilder.Append($"<li>Thời gian: {reminderSource.StartAt:dd/MM/yyyy HH:mm} - {reminderSource.EndAt:dd/MM/yyyy HH:mm}</li>");
                bodyBuilder.Append("</ul>");
                bodyBuilder.Append("<p>Vui lòng có mặt đúng giờ để hoàn thành điểm rèn luyện.</p>");
                bodyBuilder.Append("<p>Trân trọng.</p>");

                try
                {
                    await _emailSender.SendAsync(recipient, subject, bodyBuilder.ToString(), HttpContext.RequestAborted);
                    emailSent = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể gửi email xác nhận đăng ký hoạt động {ActivityId} cho {Email}", id, recipient);
                }
            }

            return Json(new
            {
                ok = true,
                message = emailSent ? "Đăng ký thành công! Đã gửi email nhắc nhở." : "Đăng ký thành công!",
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

        [HttpPost("send-reminders")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendReminders()
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { ok = false, message = "Không tìm thấy email để gửi nhắc nhở.", toastType = "danger" });
            }

            var now = DateTime.Now;
            var windowEnd = now.AddDays(7);
            var upcoming = await _repository.GetUpcomingRegistrationsAsync(studentId, now, windowEnd);
            if (upcoming.Count == 0)
            {
                return Json(new { ok = true, success = false, toastType = "warning", message = "Hiện chưa có hoạt động nào trong 7 ngày tới." });
            }

            var body = new StringBuilder();
            body.Append($"<p>Chào {HtmlEncoder.Default.Encode(User.Identity?.Name ?? "bạn")},</p>");
            body.Append("<p>Đây là các hoạt động bạn đã đăng ký sẽ diễn ra sắp tới:</p><ul>");
            foreach (var item in upcoming)
            {
                body.Append("<li><strong>");
                body.Append(HtmlEncoder.Default.Encode(item.Title));
                body.Append("</strong> - ");
                body.Append(item.StartAt.ToString("dd/MM/yyyy HH:mm"));
                body.Append(" đến ");
                body.Append(item.EndAt.ToString("dd/MM/yyyy HH:mm"));
                body.Append("</li>");
            }
            body.Append("</ul>");
            body.Append("<p>Vui lòng sắp xếp thời gian để tham gia đầy đủ.</p>");
            body.Append("<p>Trân trọng.</p>");

            try
            {
                await _emailSender.SendAsync(email, "[QLDRL] Nhắc nhở hoạt động sắp diễn ra", body.ToString(), HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi email nhắc nhở hoạt động cho {Email}", email);
                return Json(new { ok = false, message = "Không thể gửi email nhắc nhở lúc này.", toastType = "danger" });
            }

            return Json(new { ok = true, message = "Đã gửi email nhắc nhở tới hộp thư của bạn.", toastType = "success" });
        }

        private string? GetStudentId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("mand")?.Value;
        }
    }
}
