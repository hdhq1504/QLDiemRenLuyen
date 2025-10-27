using System;
using System.Collections.Generic;

namespace QLDiemRenLuyen.Models.ViewModels
{
    /// <summary>
    /// ViewModel trang danh sách hoạt động dành cho sinh viên.
    /// </summary>
    public class StudentActivitiesVm
    {
        public string? SelectedTermId { get; set; }
        public string? Keyword { get; set; }
        public IEnumerable<TermDto> Terms { get; set; } = new List<TermDto>();
        public PagedList<ActivityItemVm> Items { get; set; } = new();
    }

    /// <summary>
    /// DTO học kỳ dùng cho dropdown lọc.
    /// </summary>
    public class TermDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO danh sách hoạt động hiển thị trên card.
    /// </summary>
    public class ActivityItemVm
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Organizer { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? MaxSeats { get; set; }
        public string? CriterionName { get; set; }
        public int RegisteredCount { get; set; }
        public string StudentState { get; set; } = "NOT_REGISTERED";
    }

    /// <summary>
    /// Lớp mô tả danh sách phân trang cho UI.
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu của phần tử.</typeparam>
    public class PagedList<T>
    {
        public IEnumerable<T> Data { get; set; } = new List<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }
}