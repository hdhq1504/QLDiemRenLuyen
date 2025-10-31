using System;

namespace QLDiemRenLuyen.Models.ViewModels
{
    /// <summary>
    /// ViewModel trang danh sách thông báo dành cho sinh viên.
    /// </summary>
    public class StudentNotificationsVm
    {
        /// <summary>
        /// Mã định danh sinh viên hiện đăng nhập.
        /// </summary>
        public string StudentId { get; set; } = string.Empty;

        /// <summary>
        /// Từ khóa tìm kiếm theo tiêu đề.
        /// </summary>
        public string? Keyword { get; set; }

        /// <summary>
        /// Trạng thái lọc thông báo: all | unread | read.
        /// </summary>
        public string Status { get; set; } = "all";

        /// <summary>
        /// Danh sách phân trang các thông báo hiển thị.
        /// </summary>
        public PagedList<NotificationItemVm> Items { get; set; } = new();

        /// <summary>
        /// Tổng số thông báo chưa đọc (không phụ thuộc bộ lọc hiện tại).
        /// </summary>
        public int UnreadCount { get; set; }
    }

    /// <summary>
    /// Thông tin rút gọn mỗi thông báo trong bảng danh sách.
    /// </summary>
    public class NotificationItemVm
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public bool IsRead { get; set; }
    }

    /// <summary>
    /// Thông tin chi tiết một thông báo.
    /// </summary>
    public class NotificationDetailVm
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Nội dung đã được mã hóa an toàn để render dạng HTML.
        /// </summary>
        public string ContentHtml { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public bool IsRead { get; set; }
    }
}
