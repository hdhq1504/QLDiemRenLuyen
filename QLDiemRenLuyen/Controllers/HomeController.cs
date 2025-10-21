using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QLDiemRenLuyen.Models;
using QLDiemRenLuyen.Models.ViewModels;

namespace QLDiemRenLuyen.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        [Authorize(Roles = "STUDENT")]
        /// Trang dashboard dành cho sinh viên.
        [HttpGet]
        [Route("/student/home")]
        public IActionResult Index()
        {
            // TODO: Thay thế dữ liệu mock bằng dữ liệu thật lấy từ Oracle thông qua repository
            var viewModel = new StudentHomeVm
            {
                FullName = "Nguyễn Văn A",
                TotalScore = 82,
                ActivitiesJoined = 6,
                ProofsUploaded = 4,
                FeedbackCount = 2,
                Notifications = new List<NotificationDto>
                {
                    new NotificationDto
                    {
                        Title = "Thông báo về tuần sinh hoạt công dân",
                        CreatedAt = DateTime.Now.AddDays(-1)
                    },
                    new NotificationDto
                    {
                        Title = "Nhắc nhở hoàn thành minh chứng hoạt động tình nguyện",
                        CreatedAt = DateTime.Now.AddDays(-3)
                    },
                    new NotificationDto
                    {
                        Title = "Cập nhật quy chế đánh giá rèn luyện năm học 2024-2025",
                        CreatedAt = DateTime.Now.AddDays(-5)
                    }
                },
                UpcomingActivities = new List<ActivityDto>
                {
                    new ActivityDto
                    {
                        Title = "Chương trình thiện nguyện Mùa hè xanh",
                        StartAt = DateTime.Today.AddDays(2).AddHours(8),
                        EndAt = DateTime.Today.AddDays(2).AddHours(16),
                        Status = "Sắp diễn ra"
                    },
                    new ActivityDto
                    {
                        Title = "Workshop Kỹ năng mềm - Kỹ năng thuyết trình",
                        StartAt = DateTime.Today.AddDays(5).AddHours(9),
                        EndAt = DateTime.Today.AddDays(5).AddHours(11),
                        Status = "Đang đăng ký"
                    },
                    new ActivityDto
                    {
                        Title = "Giải bóng đá sinh viên HUIT 2024",
                        StartAt = DateTime.Today.AddDays(7).AddHours(18),
                        EndAt = DateTime.Today.AddDays(7).AddHours(21),
                        Status = "Hạn cuối đăng ký"
                    }
                }
            };

            return View(viewModel);
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            // Giữ nguyên trang lỗi chung để không ảnh hưởng tới pipeline hiện tại
            return View("Error", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
