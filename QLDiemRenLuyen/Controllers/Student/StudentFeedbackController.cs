using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QLDiemRenLuyen.Data.Student;
using QLDiemRenLuyen.Models.ViewModels;
using QLDiemRenLuyen.Models.ViewModels.Student;

namespace QLDiemRenLuyen.Controllers.Student
{
    /// <summary>
    /// Controller quản lý tính năng phản hồi điểm rèn luyện dành cho sinh viên.
    /// </summary>
    [Authorize(Roles = "STUDENT")]
    [Route("student/feedback")]
    public class StudentFeedbackController : Controller
    {
        private readonly StudentFeedbackRepository _repository;
        private readonly ILogger<StudentFeedbackController> _logger;

        public StudentFeedbackController(StudentFeedbackRepository repository, ILogger<StudentFeedbackController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? termId, string? q, int page = 1, int pageSize = 10)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Forbid();
            }

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 50);
            var keyword = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
            var normalizedTermId = string.IsNullOrWhiteSpace(termId) ? null : termId.Trim();
            if (normalizedTermId != null && !int.TryParse(normalizedTermId, out _))
            {
                normalizedTermId = null;
            }

            var terms = (await _repository.GetTermsAsync()).ToList();
            var paged = await _repository.GetFeedbacksAsync(studentId, normalizedTermId, page, pageSize, keyword);

            var model = new StudentFeedbackVm
            {
                StudentId = studentId,
                SelectedTermId = normalizedTermId,
                Keyword = keyword,
                Terms = terms,
                Items = paged
            };

            ViewData["Title"] = "Phản hồi điểm rèn luyện";
            return View("~/Views/Student/StudentFeedback/Index.cshtml", model);
        }

        [HttpGet("create")]
        public async Task<IActionResult> Create()
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            var terms = (await _repository.GetTermsAsync()).ToList();
            if (!terms.Any())
            {
                return BadRequest("Chưa cấu hình học kỳ.");
            }

            var criteria = (await _repository.GetCriteriaAsync()).ToList();
            var defaultTerm = terms.First();

            var vm = new FeedbackEditModalVm
            {
                Feedback = new FeedbackEditVm
                {
                    TermId = defaultTerm.Id,
                    SubmitStatus = "SUBMITTED"
                },
                Terms = terms,
                Criteria = criteria,
                IsEdit = false,
                Status = "DRAFT",
                FormAction = Url.Action("Create") ?? "/student/feedback/create"
            };

            return PartialView("~/Views/Student/StudentFeedback/_EditModal.cshtml", vm);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] FeedbackEditVm vm)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            var desiredStatus = NormalizeStatus(vm.SubmitStatus);
            vm.SubmitStatus = desiredStatus;
            vm.TermId = vm.TermId?.Trim() ?? string.Empty;
            vm.CriterionId = string.IsNullOrWhiteSpace(vm.CriterionId) ? null : vm.CriterionId.Trim();

            var terms = (await _repository.GetTermsAsync()).ToList();
            if (!int.TryParse(vm.TermId, out _))
            {
                ModelState.AddModelError(nameof(vm.TermId), "Học kỳ không hợp lệ.");
            }
            else if (!terms.Any(t => string.Equals(t.Id, vm.TermId, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(nameof(vm.TermId), "Học kỳ không hợp lệ.");
            }

            var criteria = (await _repository.GetCriteriaAsync()).ToList();
            if (!string.IsNullOrWhiteSpace(vm.CriterionId))
            {
                if (!int.TryParse(vm.CriterionId, out _))
                {
                    ModelState.AddModelError(nameof(vm.CriterionId), "Tiêu chí không hợp lệ.");
                }
                else if (!criteria.Any(c => string.Equals(c.Id, vm.CriterionId, StringComparison.OrdinalIgnoreCase)))
                {
                    ModelState.AddModelError(nameof(vm.CriterionId), "Tiêu chí không hợp lệ.");
                }
            }

            if (string.IsNullOrEmpty(desiredStatus))
            {
                ModelState.AddModelError(nameof(vm.SubmitStatus), "Trạng thái không hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Dữ liệu không hợp lệ.",
                    errors = ModelState.Where(x => x.Value?.Errors?.Count > 0)
                        .ToDictionary(k => k.Key, v => v.Value?.Errors.Select(e => e.ErrorMessage).ToArray())
                });
            }

            try
            {
                var newId = await _repository.CreateAsync(vm, studentId, desiredStatus);
                await _repository.WriteAuditAsync(studentId, "FEEDBACK_CREATE", GetClientIp(), GetUserAgent(), new
                {
                    id = newId,
                    vm.TermId,
                    vm.CriterionId,
                    vm.Title,
                    status = desiredStatus
                });

                return Json(new { ok = true, message = "Gửi phản hồi thành công!", id = newId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tạo phản hồi thất bại cho sinh viên {StudentId}", studentId);
                return StatusCode(500, new { ok = false, message = "Không thể gửi phản hồi. Vui lòng thử lại." });
            }
        }

        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(string id)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            var detail = await _repository.GetFeedbackAsync(id, studentId);
            if (detail == null)
            {
                return NotFound();
            }

            if (!string.Equals(detail.Status, "DRAFT", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Chỉ có thể chỉnh sửa phản hồi ở trạng thái nháp.");
            }

            var terms = (await _repository.GetTermsAsync()).ToList();
            var criteria = (await _repository.GetCriteriaAsync()).ToList();

            var vm = new FeedbackEditModalVm
            {
                FeedbackId = detail.Id,
                IsEdit = true,
                Status = detail.Status,
                FormAction = Url.Action("Edit", new { id }) ?? $"/student/feedback/edit/{id}",
                Terms = terms,
                Criteria = criteria,
                Feedback = new FeedbackEditVm
                {
                    TermId = detail.TermId,
                    CriterionId = detail.CriterionId,
                    Title = detail.Title,
                    Content = detail.Content,
                    SubmitStatus = detail.Status
                }
            };

            return PartialView("~/Views/Student/StudentFeedback/_EditModal", vm);
        }

        [HttpPost("edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [FromForm] FeedbackEditVm vm)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            var desiredStatus = NormalizeStatus(vm.SubmitStatus);
            vm.SubmitStatus = desiredStatus;
            vm.TermId = vm.TermId?.Trim() ?? string.Empty;
            vm.CriterionId = string.IsNullOrWhiteSpace(vm.CriterionId) ? null : vm.CriterionId.Trim();

            var detail = await _repository.GetFeedbackAsync(id, studentId);
            if (detail == null)
            {
                return NotFound(new { ok = false, message = "Phản hồi không tồn tại." });
            }

            if (!string.Equals(detail.Status, "DRAFT", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { ok = false, message = "Chỉ có thể chỉnh sửa phản hồi ở trạng thái nháp." });
            }

            var terms = (await _repository.GetTermsAsync()).ToList();
            if (!int.TryParse(vm.TermId, out _))
            {
                ModelState.AddModelError(nameof(vm.TermId), "Học kỳ không hợp lệ.");
            }
            else if (!terms.Any(t => string.Equals(t.Id, vm.TermId, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(nameof(vm.TermId), "Học kỳ không hợp lệ.");
            }

            var criteria = (await _repository.GetCriteriaAsync()).ToList();
            if (!string.IsNullOrWhiteSpace(vm.CriterionId))
            {
                if (!int.TryParse(vm.CriterionId, out _))
                {
                    ModelState.AddModelError(nameof(vm.CriterionId), "Tiêu chí không hợp lệ.");
                }
                else if (!criteria.Any(c => string.Equals(c.Id, vm.CriterionId, StringComparison.OrdinalIgnoreCase)))
                {
                    ModelState.AddModelError(nameof(vm.CriterionId), "Tiêu chí không hợp lệ.");
                }
            }

            if (string.IsNullOrEmpty(desiredStatus))
            {
                ModelState.AddModelError(nameof(vm.SubmitStatus), "Trạng thái không hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    ok = false,
                    message = "Dữ liệu không hợp lệ.",
                    errors = ModelState.Where(x => x.Value?.Errors?.Count > 0)
                        .ToDictionary(k => k.Key, v => v.Value?.Errors.Select(e => e.ErrorMessage).ToArray())
                });
            }

            var success = await _repository.UpdateAsync(id, vm, studentId, desiredStatus);
            if (!success)
            {
                return StatusCode(500, new { ok = false, message = "Không thể cập nhật phản hồi." });
            }

            await _repository.WriteAuditAsync(studentId, "FEEDBACK_UPDATE", GetClientIp(), GetUserAgent(), new
            {
                id,
                vm.TermId,
                vm.CriterionId,
                vm.Title,
                status = desiredStatus
            });

            return Json(new { ok = true, message = "Cập nhật phản hồi thành công." });
        }

        [HttpPost("delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            var success = await _repository.DeleteAsync(id, studentId);
            if (!success)
            {
                return BadRequest(new { ok = false, message = "Không thể xóa phản hồi (chỉ xóa được trạng thái nháp)." });
            }

            await _repository.WriteAuditAsync(studentId, "FEEDBACK_DELETE", GetClientIp(), GetUserAgent(), new { id });
            return Json(new { ok = true, message = "Đã xóa phản hồi." });
        }

        [HttpGet("detail/{id}")]
        public async Task<IActionResult> Detail(string id)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            var detail = await _repository.GetFeedbackAsync(id, studentId);
            if (detail == null)
            {
                return NotFound();
            }

            return PartialView("~/Views/Student/StudentFeedback/_DetailModal.cshtml", detail);
        }

        private string? GetStudentId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private string NormalizeStatus(string? input)
        {
            return string.IsNullOrWhiteSpace(input)
                ? "SUBMITTED"
                : input.Trim().ToUpperInvariant() == "DRAFT" ? "DRAFT" : "SUBMITTED";
        }

        private string GetClientIp()
        {
            return HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private string GetUserAgent()
        {
            return Request?.Headers["User-Agent"].ToString() ?? "unknown";
        }
    }
}
