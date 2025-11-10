using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QLDiemRenLuyen.Data;
using QLDiemRenLuyen.ViewModels.Admin;

namespace QLDiemRenLuyen.Controllers.Admin
{
    /// <summary>
    /// Controller quản lý hoạt động dành cho quản trị viên.
    /// </summary>
    [Authorize(Roles = "ADMIN")]
    [Route("admin/activities")]
    public class AdminActivitiesController : Controller
    {
        private static readonly string[] AllowedStatuses = { "OPEN", "CLOSED", "FULL", "CANCELLED" };
        private static readonly string[] AllowedApprovalStatuses = { "PENDING", "APPROVED", "REJECTED" };

        private readonly AdminActivitiesRepository _repository;
        private readonly ILogger<AdminActivitiesController> _logger;

        public AdminActivitiesController(AdminActivitiesRepository repository, ILogger<AdminActivitiesController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? termId, string? criterionId, string? status = "all", string? approval = "all", string? q = null, int page = 1, int pageSize = 10)
        {
            var filter = new ActivityFilter
            {
                TermId = string.IsNullOrWhiteSpace(termId) ? null : termId.Trim(),
                CriterionId = string.IsNullOrWhiteSpace(criterionId) ? null : criterionId.Trim(),
                Status = NormalizeStatus(status),
                Approval = NormalizeApproval(approval),
                Keyword = string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
                Page = Math.Max(1, page),
                PageSize = Math.Clamp(pageSize, 5, 50)
            };

            var activities = await _repository.SearchAsync(filter);
            var terms = (await _repository.GetTermsAsync()).ToList();
            var criteria = (await _repository.GetCriteriaAsync()).ToList();

            var vm = new AdminActivitiesIndexVm
            {
                Filter = filter,
                Activities = activities,
                Terms = terms,
                Criteria = criteria
            };

            ViewBag.StatusOptions = AllowedStatuses.Prepend("all").ToArray();
            ViewBag.ApprovalOptions = AllowedApprovalStatuses.Prepend("all").ToArray();
            ViewData["Title"] = "Quản lý hoạt động";

            return View("~/Views/Admin/AdminActivities/Index.cshtml", vm);
        }

        [HttpGet("create")]
        public async Task<IActionResult> Create()
        {
            var adminId = GetAdminId();
            if (string.IsNullOrEmpty(adminId))
            {
                return Unauthorized();
            }

            var terms = (await _repository.GetTermsAsync()).ToList();
            var criteria = (await _repository.GetCriteriaAsync()).ToList();

            if (!terms.Any() || !criteria.Any())
            {
                return BadRequest("Vui lòng cấu hình học kỳ và tiêu chí trước.");
            }

            var vm = new ActivityEditModalVm
            {
                IsEdit = false,
                FormAction = Url.Action("Create") ?? "/admin/activities/create",
                Terms = terms,
                Criteria = criteria,
                Activity = new ActivityEditVm
                {
                    TermId = terms.First().Id,
                    CriterionId = criteria.First().Id,
                    StartAt = DateTime.Now,
                    EndAt = DateTime.Now.AddHours(2),
                    Status = "OPEN"
                }
            };

            return PartialView("~/Views/Admin/AdminActivities/_EditModal.cshtml", vm);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] ActivityEditVm vm)
        {
            var adminId = GetAdminId();
            if (string.IsNullOrEmpty(adminId))
            {
                return Unauthorized();
            }

            NormalizeEditVm(vm);
            await ValidateEditVmAsync(vm);

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
                var newId = await _repository.CreateAsync(vm, adminId);
                await _repository.WriteAuditAsync(adminId, "ACTIVITY_CREATE_META", new
                {
                    id = newId,
                    ip = GetClientIp(),
                    agent = GetUserAgent()
                });

                return Json(new { ok = true, message = "Đã tạo hoạt động thành công.", id = newId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin {AdminId} tạo hoạt động thất bại", adminId);
                return StatusCode(500, new { ok = false, message = "Không thể tạo hoạt động. Vui lòng thử lại." });
            }
        }

        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(string id)
        {
            var adminId = GetAdminId();
            if (string.IsNullOrEmpty(adminId))
            {
                return Unauthorized();
            }

            var detail = await _repository.GetAsync(id);
            if (detail == null)
            {
                return NotFound();
            }

            var terms = (await _repository.GetTermsAsync()).ToList();
            var criteria = (await _repository.GetCriteriaAsync()).ToList();

            var vm = new ActivityEditModalVm
            {
                IsEdit = true,
                FormAction = Url.Action("Edit", new { id }) ?? $"/admin/activities/edit/{id}",
                Terms = terms,
                Criteria = criteria,
                Activity = new ActivityEditVm
                {
                    Id = detail.Id,
                    Title = detail.Title,
                    Description = detail.Description,
                    TermId = detail.TermId ?? string.Empty,
                    CriterionId = detail.CriterionId ?? string.Empty,
                    StartAt = detail.StartAt,
                    EndAt = detail.EndAt,
                    Status = detail.Status,
                    MaxSeats = detail.MaxSeats,
                    Location = detail.Location,
                    Points = detail.Points
                }
            };

            return PartialView("~/Views/Admin/AdminActivities/_EditModal.cshtml", vm);
        }

        [HttpPost("edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [FromForm] ActivityEditVm vm)
        {
            var adminId = GetAdminId();
            if (string.IsNullOrEmpty(adminId))
            {
                return Unauthorized();
            }

            var existing = await _repository.GetAsync(id);
            if (existing == null)
            {
                return NotFound(new { ok = false, message = "Hoạt động không tồn tại." });
            }

            NormalizeEditVm(vm);
            await ValidateEditVmAsync(vm);

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
                var success = await _repository.UpdateAsync(id, vm, adminId);
                if (!success)
                {
                    return StatusCode(500, new { ok = false, message = "Không thể cập nhật hoạt động." });
                }

                await _repository.WriteAuditAsync(adminId, "ACTIVITY_UPDATE_META", new
                {
                    id,
                    ip = GetClientIp(),
                    agent = GetUserAgent()
                });

                return Json(new { ok = true, message = "Đã cập nhật hoạt động." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin {AdminId} cập nhật hoạt động {ActivityId} thất bại", adminId, id);
                return StatusCode(500, new { ok = false, message = "Không thể cập nhật hoạt động." });
            }
        }

        [HttpPost("delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var adminId = GetAdminId();
            if (string.IsNullOrEmpty(adminId))
            {
                return Unauthorized();
            }

            try
            {
                var success = await _repository.DeleteAsync(id, adminId);
                if (!success)
                {
                    return NotFound(new { ok = false, message = "Hoạt động không tồn tại." });
                }

                await _repository.WriteAuditAsync(adminId, "ACTIVITY_DELETE_META", new
                {
                    id,
                    ip = GetClientIp(),
                    agent = GetUserAgent()
                });

                return Json(new { ok = true, message = "Đã xóa hoạt động." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin {AdminId} xóa hoạt động {ActivityId} thất bại", adminId, id);
                return StatusCode(500, new { ok = false, message = "Không thể xóa hoạt động." });
            }
        }

        [HttpPost("approve/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(string id)
        {
            var adminId = GetAdminId();
            if (string.IsNullOrEmpty(adminId))
            {
                return Unauthorized();
            }

            var detail = await _repository.GetAsync(id);
            if (detail == null)
            {
                return NotFound(new { ok = false, message = "Hoạt động không tồn tại." });
            }

            if (!string.Equals(detail.ApprovalStatus, "PENDING", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { ok = false, message = "Chỉ phê duyệt hoạt động đang chờ duyệt." });
            }

            var success = await _repository.ApproveAsync(id, adminId);
            if (!success)
            {
                return StatusCode(500, new { ok = false, message = "Không thể phê duyệt hoạt động." });
            }

            await _repository.WriteAuditAsync(adminId, "ACTIVITY_APPROVE_META", new
            {
                id,
                ip = GetClientIp(),
                agent = GetUserAgent()
            });

            return Json(new { ok = true, message = "Đã phê duyệt hoạt động." });
        }

        [HttpPost("reject/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(string id)
        {
            var adminId = GetAdminId();
            if (string.IsNullOrEmpty(adminId))
            {
                return Unauthorized();
            }

            var detail = await _repository.GetAsync(id);
            if (detail == null)
            {
                return NotFound(new { ok = false, message = "Hoạt động không tồn tại." });
            }

            if (!string.Equals(detail.ApprovalStatus, "PENDING", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { ok = false, message = "Chỉ từ chối hoạt động đang chờ duyệt." });
            }

            var success = await _repository.RejectAsync(id, adminId);
            if (!success)
            {
                return StatusCode(500, new { ok = false, message = "Không thể từ chối hoạt động." });
            }

            await _repository.WriteAuditAsync(adminId, "ACTIVITY_REJECT_META", new
            {
                id,
                ip = GetClientIp(),
                agent = GetUserAgent()
            });

            return Json(new { ok = true, message = "Đã từ chối hoạt động." });
        }

        [HttpPost("open/{id}")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Open(string id) => ChangeStatusAsync(id, "OPEN", "Đã mở lại hoạt động.");

        [HttpPost("close/{id}")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Close(string id) => ChangeStatusAsync(id, "CLOSED", "Đã đóng hoạt động.");

        [HttpPost("cancel/{id}")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Cancel(string id) => ChangeStatusAsync(id, "CANCELLED", "Đã hủy hoạt động.");

        [HttpPost("full/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkFull(string id)
        {
            var adminId = GetAdminId();
            if (string.IsNullOrEmpty(adminId))
            {
                return Unauthorized();
            }

            var detail = await _repository.GetAsync(id);
            if (detail == null)
            {
                return NotFound(new { ok = false, message = "Hoạt động không tồn tại." });
            }

            var counts = await _repository.GetRegCountsAsync(id);
            if (!detail.MaxSeats.HasValue || counts.registered < detail.MaxSeats.Value)
            {
                return BadRequest(new { ok = false, message = "Số lượng đăng ký chưa đạt tối đa." });
            }

            var success = await _repository.SetStatusAsync(id, "FULL", adminId);
            if (!success)
            {
                return StatusCode(500, new { ok = false, message = "Không thể cập nhật trạng thái." });
            }

            var auditBase = MapStatusToAuditBase("FULL");
            await _repository.WriteAuditAsync(adminId, $"{auditBase}_META", new
            {
                id,
                ip = GetClientIp(),
                agent = GetUserAgent()
            });

            return Json(new { ok = true, message = "Đã đánh dấu hoạt động đủ chỗ." });
        }

        [HttpGet("detail/{id}")]
        public async Task<IActionResult> Detail(string id)
        {
            var adminId = GetAdminId();
            if (string.IsNullOrEmpty(adminId))
            {
                return Unauthorized();
            }

            var detail = await _repository.GetAsync(id);
            if (detail == null)
            {
                return NotFound();
            }

            var counts = await _repository.GetRegCountsAsync(id);
            detail.RegisteredCount = counts.registered;
            detail.CheckedInCount = counts.checkedIn;

            return PartialView("~/Views/Admin/AdminActivities/_DetailModal.cshtml", detail);
        }

        private async Task<IActionResult> ChangeStatusAsync(string id, string status, string successMessage)
        {
            var adminId = GetAdminId();
            if (string.IsNullOrEmpty(adminId))
            {
                return Unauthorized();
            }

            if (!AllowedStatuses.Contains(status))
            {
                return BadRequest(new { ok = false, message = "Trạng thái không hợp lệ." });
            }

            var success = await _repository.SetStatusAsync(id, status, adminId);
            if (!success)
            {
                return StatusCode(500, new { ok = false, message = "Không thể cập nhật trạng thái." });
            }

            var auditBase = MapStatusToAuditBase(status);
            await _repository.WriteAuditAsync(adminId, $"{auditBase}_META", new
            {
                id,
                ip = GetClientIp(),
                agent = GetUserAgent()
            });

            return Json(new { ok = true, message = successMessage });
        }

        private void NormalizeEditVm(ActivityEditVm vm)
        {
            vm.Title = vm.Title?.Trim() ?? string.Empty;
            vm.Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim();
            vm.TermId = vm.TermId?.Trim() ?? string.Empty;
            vm.CriterionId = vm.CriterionId?.Trim() ?? string.Empty;
            vm.Status = NormalizeStatus(vm.Status);
            vm.Location = string.IsNullOrWhiteSpace(vm.Location) ? null : vm.Location.Trim();
        }

        private async Task ValidateEditVmAsync(ActivityEditVm vm)
        {
            if (!AllowedStatuses.Contains(vm.Status))
            {
                ModelState.AddModelError(nameof(vm.Status), "Trạng thái hoạt động không hợp lệ.");
            }

            if (vm.StartAt >= vm.EndAt)
            {
                ModelState.AddModelError(nameof(vm.EndAt), "Thời gian kết thúc phải sau thời gian bắt đầu.");
            }

            if (vm.Points.HasValue && vm.Points.Value < 0)
            {
                ModelState.AddModelError(nameof(vm.Points), "Điểm thưởng phải lớn hơn hoặc bằng 0.");
            }

            var terms = (await _repository.GetTermsAsync()).ToList();
            if (!terms.Any(t => string.Equals(t.Id, vm.TermId, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(nameof(vm.TermId), "Học kỳ không hợp lệ.");
            }

            var criteria = (await _repository.GetCriteriaAsync()).ToList();
            if (!criteria.Any(c => string.Equals(c.Id, vm.CriterionId, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(nameof(vm.CriterionId), "Tiêu chí không hợp lệ.");
            }
        }

        private string NormalizeStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return "all";
            }

            var normalized = status.Trim().ToUpperInvariant();
            return AllowedStatuses.Contains(normalized) ? normalized : "all";
        }

        private string NormalizeApproval(string? approval)
        {
            if (string.IsNullOrWhiteSpace(approval))
            {
                return "all";
            }

            var normalized = approval.Trim().ToUpperInvariant();
            return AllowedApprovalStatuses.Contains(normalized) ? normalized : "all";
        }

        private string? GetAdminId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private string GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        private string GetUserAgent() => Request.Headers["User-Agent"].ToString();

        private static string MapStatusToAuditBase(string status)
        {
            return status?.ToUpperInvariant() switch
            {
                "OPEN" => "ACTIVITY_OPEN",
                "CLOSED" => "ACTIVITY_CLOSE",
                "FULL" => "ACTIVITY_FULL",
                "CANCELLED" => "ACTIVITY_CANCEL",
                _ => "ACTIVITY_STATUS_CHANGE"
            };
        }
    }
}
