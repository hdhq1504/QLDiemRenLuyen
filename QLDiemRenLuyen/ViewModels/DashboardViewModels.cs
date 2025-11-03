using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QLDiemRenLuyen.ViewModels
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

    /// <summary>
    /// Dòng dữ liệu thông tin lớp mà giảng viên quản lý.
    /// </summary>
    public class ClassRowVm
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
    }

    /// <summary>
    /// Dòng dữ liệu điểm rèn luyện của sinh viên trong lớp.
    /// </summary>
    public class StudentScoreRowVm
    {
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
        public string TermName { get; set; } = string.Empty;
    }

    /// <summary>
    /// ViewModel dùng cho màn hình thêm sinh viên vào lớp của cán bộ.
    /// </summary>
    public class AddStudentToClassVm
    {
        [Required(ErrorMessage = "Vui lòng nhập mã sinh viên")]
        public string StudentId { get; set; } = string.Empty;
        [Required(ErrorMessage = "Vui lòng chọn lớp")]
        public string ClassId { get; set; } = string.Empty;
        public IEnumerable<LookupDto> Classes { get; set; } = new List<LookupDto>();
    }

    /// <summary>
    /// DTO tra cứu dùng chung (id, name).
    /// </summary>
    public class LookupDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
