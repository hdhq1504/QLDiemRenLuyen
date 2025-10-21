using System;
using System.Collections.Generic;

namespace QLDiemRenLuyen.Models.ViewModels
{
    /// <summary>
    /// ViewModel trang tổng quan dành cho sinh viên.
    /// </summary>
    public class StudentHomeVm
    {
        public string FullName { get; set; } = string.Empty;
        public int TotalScore { get; set; }
        public int ActivitiesJoined { get; set; }
        public int ProofsUploaded { get; set; }
        public int FeedbackCount { get; set; }
        public List<NotificationDto> Notifications { get; set; } = new();
        public List<ActivityDto> UpcomingActivities { get; set; } = new();
    }

    /// <summary>
    /// DTO thông báo hiển thị trên dashboard sinh viên.
    /// </summary>
    public class NotificationDto
    {
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// DTO hoạt động chuẩn bị diễn ra cho sinh viên.
    /// </summary>
    public class ActivityDto
    {
        public string Title { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}