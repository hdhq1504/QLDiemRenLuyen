using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QLDiemRenLuyen.Data.Student;

namespace QLDiemRenLuyen.Controllers.Student
{
    /// <summary>
    /// Controller hiển thị điểm rèn luyện cho sinh viên.
    /// </summary>
    [Authorize(Roles = "STUDENT")]
    [Route("student/scores")]
    public class StudentScoresController : Controller
    {
        private readonly StudentScoresRepository _repository;
        private readonly ILogger<StudentScoresController> _logger;

        public StudentScoresController(StudentScoresRepository repository, ILogger<StudentScoresController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        /// <summary>
        /// Trang chính /student/scores.
        /// </summary>
        [HttpGet("")]
        public async Task<IActionResult> Index(string? termId)
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Forbid();
            }

            ViewData["Title"] = "Điểm rèn luyện";

            var model = await _repository.GetMyScoreAsync(studentId, termId);

            return View("~/Views/Student/StudentScores/Index.cshtml", model);
        }

        /// <summary>
        /// Bảng lịch sử dùng cho modal.
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> History()
        {
            var studentId = GetStudentId();
            if (string.IsNullOrEmpty(studentId))
            {
                return Unauthorized();
            }

            var history = await _repository.GetHistoryAsync(studentId);
            return PartialView("~/Views/Student/StudentScores/_HistoryTable.cshtml", history);
        }

        private string? GetStudentId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("mand")?.Value;
        }
    }
}
