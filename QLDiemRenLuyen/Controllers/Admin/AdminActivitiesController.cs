using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QLDiemRenLuyen.Admin.Data;
using QLDiemRenLuyen.Admin.Models.ViewModels;
using QLDiemRenLuyen.Models.ViewModels;

namespace QLDiemRenLuyen.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "ADMIN,STAFF,ORGANIZER")]
    [Route("[area]/activities")]
    public class AdminActivitiesController : Controller
    {
        private readonly AdminActivitiesRepository _repository;
        private readonly ILogger<AdminActivitiesController> _logger;

        public AdminActivitiesController(AdminActivitiesRepository repository, ILogger<AdminActivitiesController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? termId, string? q, string? approval, string? status, int page = 1, int pageSize = 12)
        {
            ViewData["Title"] = "Quản lý hoạt động";

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 6, 48);

            var adminId = GetAdminId();
            var role = GetRole();

            var (items, total) = await _repository.SearchAsync(termId, q, approval, status, page, pageSize, adminId, role);
            var terms = await _repository.GetTermsAsync();

            var paged = new PagedList<ActivityRowVm>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize)
            };

            var vm = new AdminActivitiesVm
            {
                SelectedTermId = termId,
                Keyword = q,
                ApprovalFilter = string.IsNullOrWhiteSpace(approval) ? "ALL" : approval,
                StatusFilter = string.IsNullOrWhiteSpace(status) ? "ALL" : status,
                Terms = terms,
                Items = paged
            };

            return View(vm);
        }

        [HttpGet("create")]
        public async Task<IActionResult> Create()
        {
            ViewData["Title"] = "Thêm hoạt động";
            ViewBag.Terms = await _repository.GetTermsAsync();
            return View(new ActivityEditVm { Status = "OPEN", OrganizerId = GetAdminId() });
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ActivityEditVm vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Terms = await _repository.GetTermsAsync();
                return View(vm);
            }

            try
            {
                vm.OrganizerId ??= GetAdminId();
                var id = await _repository.CreateAsync(vm, GetAdminId());
                TempData["Success"] = "Tạo hoạt động thành công.";
                return RedirectToAction(nameof(Detail), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể tạo hoạt động");
                ModelState.AddModelError(string.Empty, "Không thể tạo hoạt động. Vui lòng thử lại.");
                ViewBag.Terms = await _repository.GetTermsAsync();
                return View(vm);
            }
        }

        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(string id)
        {
            ViewData["Title"] = "Cập nhật hoạt động";
            var adminId = GetAdminId();
            var role = GetRole();
            var detail = await _repository.GetByIdAsync(id, adminId, role);
            if (detail == null)
            {
                return NotFound();
            }

            ViewBag.Terms = await _repository.GetTermsAsync();
            return View(detail);
        }

        [HttpPost("edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, ActivityEditVm vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Terms = await _repository.GetTermsAsync();
                return View(vm);
            }

            var adminId = GetAdminId();
            var updated = await _repository.UpdateAsync(id, vm, adminId);
            if (!updated)
            {
                ModelState.AddModelError(string.Empty, "Không thể cập nhật hoạt động.");
                ViewBag.Terms = await _repository.GetTermsAsync();
                return View(vm);
            }

            TempData["Success"] = "Đã lưu thay đổi.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        [HttpGet("detail/{id}")]
        public async Task<IActionResult> Detail(string id)
        {
            ViewData["Title"] = "Chi tiết hoạt động";
            var adminId = GetAdminId();
            var role = GetRole();
            var detail = await _repository.GetByIdAsync(id, adminId, role);
            if (detail == null)
            {
                return NotFound();
            }

            return View(detail);
        }

        [HttpPost("submit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(string id)
        {
            var success = await _repository.SetApprovalAsync(id, "PENDING", GetAdminId(), null);
            return Json(new { ok = success, message = success ? "Đã gửi phê duyệt." : "Không thể gửi phê duyệt." });
        }

        [HttpPost("approve/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(string id)
        {
            var success = await _repository.SetApprovalAsync(id, "APPROVED", GetAdminId(), null);
            return Json(new { ok = success, message = success ? "Đã phê duyệt hoạt động." : "Không thể phê duyệt." });
        }

        [HttpPost("reject/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(string id, [FromForm] string? reason)
        {
            var success = await _repository.SetApprovalAsync(id, "REJECTED", GetAdminId(), reason);
            return Json(new { ok = success, message = success ? "Đã từ chối hoạt động." : "Không thể từ chối." });
        }

        [HttpPost("open/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Open(string id)
        {
            var success = await _repository.SetStatusAsync(id, "OPEN");
            return Json(new { ok = success, message = success ? "Đã mở đăng ký." : "Không thể thay đổi trạng thái." });
        }

        [HttpPost("close/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(string id)
        {
            var success = await _repository.SetStatusAsync(id, "CLOSED");
            return Json(new { ok = success, message = success ? "Đã đóng đăng ký." : "Không thể thay đổi trạng thái." });
        }

        [HttpPost("delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var success = await _repository.DeleteAsync(id, GetAdminId(), GetRole());
            return Json(new { ok = success, message = success ? "Đã xoá hoạt động." : "Không thể xoá hoạt động." });
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
