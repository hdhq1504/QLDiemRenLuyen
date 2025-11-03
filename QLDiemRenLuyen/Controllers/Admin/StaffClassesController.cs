using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QLDiemRenLuyen.Data;
using QLDiemRenLuyen.ViewModels;

namespace QLDiemRenLuyen.Controllers
{
    /// <summary>
    /// Controller xử lý tác vụ cán bộ thêm sinh viên vào lớp.
    /// </summary>
    [Authorize(Roles = "STAFF")]
    [Route("staff/classes")]
    public class StaffClassesController : Controller
    {
        private readonly StaffClassesRepository _repository;
        private readonly UserRepository _userRepository;
        private readonly ILogger<StaffClassesController> _logger;

        public StaffClassesController(StaffClassesRepository repository, UserRepository userRepository, ILogger<StaffClassesController> logger)
        {
            _repository = repository;
            _userRepository = userRepository;
            _logger = logger;
        }

        [HttpGet("add-student")]
        public async Task<IActionResult> AddStudent()
        {
            var vm = new AddStudentToClassVm
            {
                Classes = await _repository.GetClassesAsync()
            };
            return View(vm);
        }

        [HttpPost("add-student")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStudent(AddStudentToClassVm vm)
        {
            vm.StudentId = vm.StudentId?.Trim() ?? string.Empty;
            vm.ClassId = vm.ClassId?.Trim() ?? string.Empty;
            vm.Classes = await _repository.GetClassesAsync();

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            try
            {
                var success = await _repository.AddStudentToClassAsync(vm.StudentId, vm.ClassId);
                if (success)
                {
                    await AuditStaffActionAsync(vm.StudentId, vm.ClassId);
                    TempData["SuccessMessage"] = $"Đã thêm sinh viên {vm.StudentId} vào lớp thành công.";
                    return RedirectToAction(nameof(AddStudent));
                }

                ModelState.AddModelError(string.Empty, "Không thể cập nhật lớp cho sinh viên.");
                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cán bộ thêm sinh viên {StudentId} vào lớp {ClassId}", vm.StudentId, vm.ClassId);
                ModelState.AddModelError(string.Empty, "Có lỗi xảy ra khi xử lý yêu cầu.");
                return View(vm);
            }
        }

        private async Task AuditStaffActionAsync(string studentId, string classId)
        {
            var who = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "STAFF";
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers["User-Agent"].FirstOrDefault();
            await _userRepository.AuditAsync(who, "STAFF_ADD_STUDENT_TO_CLASS", clientIp, userAgent);
            _logger.LogInformation("Cán bộ {Who} đã thêm sinh viên {StudentId} vào lớp {ClassId}", who, studentId, classId);
        }
    }
}
