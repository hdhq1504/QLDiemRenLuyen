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
    /// Controller quản lý trang thông báo dành cho sinh viên.
    /// </summary>
    [Authorize(Roles = "STUDENT")]
    [Route("student/notifications")]
    public class StudentNotificationsController : Controller
    {
        private readonly StudentNotificationsRepository _repository;
        private readonly ILogger<StudentNotificationsController> _logger;

        public StudentNotificationsController(StudentNotificationsRepository repository, ILogger<StudentNotificationsController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        /// <summary>
        /// Trang danh sách thông báo với bộ lọc và phân trang.
        /// </summary>
        [HttpGet("")]
        public async Task<IActionResult> Index(string? q, string? status = "all", int page = 1, int pageSize = 10)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Forbid();
            }

            var normalizedStatus = NormalizeStatus(status);
            var keyword = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? 10 : pageSize;

            // Lấy danh sách thông báo theo bộ lọc hiện tại.
            var pagedResult = await _repository.SearchAsync(studentId, keyword, normalizedStatus, page, pageSize);

            // Lấy số lượng thông báo chưa đọc để hiển thị badge.
            var unreadResult = await _repository.SearchAsync(studentId, null, "unread", 1, 1);
            var unreadCount = unreadResult.TotalItems;

            var model = new StudentNotificationsVm
            {
                StudentId = studentId,
                Keyword = keyword,
                Status = normalizedStatus,
                Items = pagedResult,
                UnreadCount = unreadCount
            };

            return View(model);
        }

        /// <summary>
        /// Lấy nội dung chi tiết thông báo cho modal.
        /// </summary>
        [HttpGet("detail/{id}")]
        public async Task<IActionResult> Detail(string id)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            var detail = await _repository.GetDetailAsync(id, studentId);
            if (detail == null)
            {
                return NotFound();
            }

            return PartialView("_DetailModal", detail);
        }

        /// <summary>
        /// Đánh dấu một thông báo đã đọc.
        /// </summary>
        [HttpPost("mark-read/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(string id)
        {
            return await UpdateReadState(id, true);
        }

        /// <summary>
        /// Đánh dấu một thông báo chưa đọc.
        /// </summary>
        [HttpPost("mark-unread/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkUnread(string id)
        {
            return await UpdateReadState(id, false);
        }

        /// <summary>
        /// Đánh dấu tất cả thông báo hiển thị là đã đọc.
        /// </summary>
        [HttpPost("mark-all-read")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            try
            {
                var affected = await _repository.MarkAllReadAsync(studentId);
                _logger.LogInformation("Sinh viên {Student} đánh dấu tất cả thông báo đã đọc ({Count} bản ghi)", studentId, affected);

                var unreadResult = await _repository.SearchAsync(studentId, null, "unread", 1, 1);
                return Json(new
                {
                    ok = true,
                    message = "Đã đánh dấu tất cả thông báo là đã đọc.",
                    unread = unreadResult.TotalItems
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể đánh dấu tất cả thông báo đã đọc cho sinh viên {Student}", studentId);
                return Json(new { ok = false, message = "Có lỗi xảy ra, vui lòng thử lại sau." });
            }
        }

        /// <summary>
        /// Hàm dùng chung xử lý đánh dấu đã đọc/chưa đọc.
        /// </summary>
        private async Task<IActionResult> UpdateReadState(string id, bool isRead)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            var success = await _repository.MarkReadAsync(id, studentId, isRead);
            if (!success)
            {
                return Json(new { ok = false, message = "Không thể cập nhật trạng thái thông báo." });
            }

            _logger.LogInformation("Sinh viên {Student} cập nhật trạng thái thông báo {NotificationId} -> {State}", studentId, id, isRead ? "READ" : "UNREAD");
            var unreadResult = await _repository.SearchAsync(studentId, null, "unread", 1, 1);

            return Json(new
            {
                ok = true,
                message = isRead ? "Đã đánh dấu thông báo là đã đọc." : "Đã chuyển thông báo về chưa đọc.",
                unread = unreadResult.TotalItems,
                isRead
            });
        }

        private string? GetStudentId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("mand")?.Value;
        }

        private static string NormalizeStatus(string? status)
        {
            var normalized = (status ?? "all").Trim().ToLowerInvariant();
            return normalized switch
            {
                "unread" => "unread",
                "read" => "read",
                _ => "all"
            };
        }
    }
}
