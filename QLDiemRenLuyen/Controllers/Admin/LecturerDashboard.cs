using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QLDiemRenLuyen.Data;
using QLDiemRenLuyen.Data.Student;

namespace QLDiemRenLuyen.Controllers.Lecturer
{
    /// <summary>
    /// Dashboard giảng viên xem lớp và điểm rèn luyện.
    /// </summary>
    [Authorize(Roles = "LECTURER")]
    [Route("lecturer")] // gộp các endpoint về giảng viên
    public class LecturerDashboardController : Controller
    {
        private readonly LecturerDashboardRepository _repository;
        private readonly StudentScoresRepository _studentScoresRepository;
        private readonly ILogger<LecturerDashboardController> _logger;

        public LecturerDashboardController(
            LecturerDashboardRepository repository,
            StudentScoresRepository studentScoresRepository,
            ILogger<LecturerDashboardController> logger)
        {
            _repository = repository;
            _studentScoresRepository = studentScoresRepository;
            _logger = logger;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> Index()
        {
            var lecturerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(lecturerId))
            {
                _logger.LogWarning("Không tìm thấy mã người dùng trong claims");
                return Forbid();
            }

            var classes = (await _repository.GetMyClassesAsync(lecturerId)).ToList();
            var terms = (await _studentScoresRepository.GetTermsAsync()).ToList();
            ViewBag.Classes = classes;
            ViewBag.Terms = terms;
            ViewBag.DefaultTermId = terms.FirstOrDefault()?.Id;

            return View("~/Views/Lecturer/Dashboard/Index.cshtml");
        }

        [HttpGet("classes")]
        public async Task<IActionResult> GetClasses()
        {
            var lecturerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(lecturerId))
            {
                return Forbid();
            }

            var classes = await _repository.GetMyClassesAsync(lecturerId);
            return Json(classes);
        }

        [HttpGet("classes/{classId}/scores")]
        public async Task<IActionResult> GetClassScores([FromRoute] string classId, [FromQuery] string? termId)
        {
            if (string.IsNullOrWhiteSpace(classId) || string.IsNullOrWhiteSpace(termId))
            {
                return BadRequest("Thiếu tham số bắt buộc");
            }

            var lecturerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(lecturerId))
            {
                return Forbid();
            }

            var classes = (await _repository.GetMyClassesAsync(lecturerId)).ToList();
            if (!classes.Any(c => string.Equals(c.Id, classId, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning("Giảng viên {LecturerId} cố truy cập lớp không thuộc quyền {ClassId}", lecturerId, classId);
                return NotFound();
            }

            var scores = await _repository.GetClassScoresAsync(lecturerId, classId, termId);
            return Json(scores);
        }
    }
}
