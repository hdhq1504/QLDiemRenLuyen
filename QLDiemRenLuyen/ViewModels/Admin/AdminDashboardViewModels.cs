using System;

namespace QLDiemRenLuyen.ViewModels.Admin
{
    /// <summary>
    /// Thống kê tổng quan dành cho trang dashboard của quản trị viên.
    /// </summary>
    public class AdminKpiVm
    {
        public int TotalUsers { get; set; }
        public int OpenActivities { get; set; }
        public int RegistrationsToday { get; set; }
        public int PendingFeedbacks { get; set; }
        public int Notifications7d { get; set; }
    }

    /// <summary>
    /// Điểm dữ liệu dạng chuỗi thời gian cho biểu đồ xu hướng đăng ký.
    /// </summary>
    public class TimeSeriesPoint
    {
        public DateTime Day { get; set; }
        public int Count { get; set; }
    }

    /// <summary>
    /// Biểu diễn hoạt động có số lượt đăng ký cao nhất.
    /// </summary>
    public class TopActivityPoint
    {
        public string ActivityId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>
    /// Dòng audit gần nhất.
    /// </summary>
    public class RecentAuditVm
    {
        public string Id { get; set; } = string.Empty;
        public string Who { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public DateTime EventAtUtc { get; set; }
        public string? ClientIp { get; set; }
    }

    /// <summary>
    /// Phản hồi chờ xử lý của admin.
    /// </summary>
    public class PendingFeedbackVm
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string TermName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
