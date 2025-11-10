using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QLDiemRenLuyen.Data;

namespace QLDiemRenLuyen.Controllers.Admin
{
    /// <summary>
    /// Controller xử lý các yêu cầu dashboard của quản trị viên.
    /// </summary>
    [Authorize(Roles = "ADMIN")]
    [Route("admin/dashboard")]
    public class AdminDashboardController : Controller
    {
        private readonly AdminDashboardRepository _repository;
        private readonly ILogger<AdminDashboardController> _logger;

        public AdminDashboardController(AdminDashboardRepository repository, ILogger<AdminDashboardController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var kpis = await _repository.GetKpisAsync();
            var terms = (await _repository.GetTermsAsync()).ToList();
            ViewBag.Terms = terms;
            ViewBag.DefaultTermId = terms.FirstOrDefault()?.Id;
            return View("~/Views/Admin/AdminDashboard/Index.cshtml", kpis);
        }

        [HttpGet("registrations-trend")]
        public async Task<IActionResult> RegistrationsTrend([FromQuery] int days = 14)
        {
            if (days <= 0)
            {
                days = 14;
            }

            var data = await _repository.GetRegistrationsTrendAsync(days);
            return Json(data);
        }

        [HttpGet("top-activities")]
        public async Task<IActionResult> TopActivities([FromQuery] string? termId, [FromQuery] int top = 5)
        {
            if (top <= 0)
            {
                top = 5;
            }

            try
            {
                var data = await _repository.GetTopActivitiesAsync(termId, top);
                return Json(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể lấy top hoạt động");
                return StatusCode(500, "Không thể tải dữ liệu top hoạt động");
            }
        }

        [HttpGet("recent-audit")]
        public async Task<IActionResult> RecentAudit()
        {
            var data = await _repository.GetRecentAuditAsync(8);
            return PartialView("~/Views/Admin/AdminDashboard/_RecentAudit", data);
        }

        [HttpGet("pending-feedbacks")]
        public async Task<IActionResult> PendingFeedbacks()
        {
            var data = await _repository.GetPendingFeedbacksAsync(10);
            return PartialView("~/Views/Admin/AdminDashboard/_PendingFeedbacks", data);
        }
    }
}
