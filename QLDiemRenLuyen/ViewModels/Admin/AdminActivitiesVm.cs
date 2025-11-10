using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using QLDiemRenLuyen.ViewModels.Common;

namespace QLDiemRenLuyen.ViewModels.Admin
{
    /// <summary>
    /// Bộ lọc tìm kiếm hoạt động quản trị.
    /// </summary>
    public class ActivityFilter
    {
        public string? TermId { get; set; }

        public string? CriterionId { get; set; }

        public string Status { get; set; } = "all";

        public string Approval { get; set; } = "all";

        public string? Keyword { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 10;
    }

    /// <summary>
    /// Thông tin hoạt động hiển thị trên bảng danh sách.
    /// </summary>
    public class ActivityRowVm
    {
        public string Id { get; set; } = string.Empty;

        public string? TermId { get; set; }

        public string? TermName { get; set; }

        public string? CriterionId { get; set; }

        public string? CriterionName { get; set; }

        public string Title { get; set; } = string.Empty;

        public DateTime StartAt { get; set; }

        public DateTime EndAt { get; set; }

        public string Status { get; set; } = string.Empty;

        public string ApprovalStatus { get; set; } = string.Empty;

        public int? MaxSeats { get; set; }

        public int RegisteredCount { get; set; }

        public int CheckedInCount { get; set; }
    }

    /// <summary>
    /// View model dùng cho form tạo/chỉnh sửa hoạt động.
    /// </summary>
    public class ActivityEditVm
    {
        public string? Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên hoạt động.")]
        [StringLength(200, ErrorMessage = "Tên hoạt động tối đa 200 ký tự.")]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "Mô tả chi tiết")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn học kỳ.")]
        public string TermId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn tiêu chí.")]
        public string CriterionId { get; set; } = string.Empty;

        [Display(Name = "Thời gian bắt đầu")]
        [DataType(DataType.DateTime)]
        public DateTime StartAt { get; set; }

        [Display(Name = "Thời gian kết thúc")]
        [DataType(DataType.DateTime)]
        public DateTime EndAt { get; set; }

        [Required]
        public string Status { get; set; } = "OPEN";

        [Range(0, int.MaxValue, ErrorMessage = "Số chỗ tối đa phải >= 0.")]
        public int? MaxSeats { get; set; }

        [StringLength(500, ErrorMessage = "Địa điểm tối đa 500 ký tự.")]
        public string? Location { get; set; }

        public decimal? Points { get; set; }
    }

    /// <summary>
    /// View model hiển thị chi tiết hoạt động.
    /// </summary>
    public class ActivityDetailVm
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? TermId { get; set; }

        public string? TermName { get; set; }

        public string? CriterionId { get; set; }

        public string? CriterionName { get; set; }

        public DateTime StartAt { get; set; }

        public DateTime EndAt { get; set; }

        public string Status { get; set; } = string.Empty;

        public string ApprovalStatus { get; set; } = string.Empty;

        public int? MaxSeats { get; set; }

        public string? Location { get; set; }

        public decimal? Points { get; set; }

        public string? ApprovedBy { get; set; }

        public string? ApprovedByName { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public string? OrganizerId { get; set; }

        public string? OrganizerName { get; set; }

        public DateTime CreatedAt { get; set; }

        public int RegisteredCount { get; set; }

        public int CheckedInCount { get; set; }
    }

    /// <summary>
    /// View model tổng hợp cho modal chỉnh sửa.
    /// </summary>
    public class ActivityEditModalVm
    {
        public bool IsEdit { get; set; }

        public string FormAction { get; set; } = string.Empty;

        public ActivityEditVm Activity { get; set; } = new();

        public IEnumerable<LookupDto> Terms { get; set; } = new List<LookupDto>();

        public IEnumerable<LookupDto> Criteria { get; set; } = new List<LookupDto>();
    }

    /// <summary>
    /// View model trang danh sách hoạt động.
    /// </summary>
    public class AdminActivitiesIndexVm
    {
        public ActivityFilter Filter { get; set; } = new();

        public PagedList<ActivityRowVm> Activities { get; set; } = new();

        public IEnumerable<LookupDto> Terms { get; set; } = new List<LookupDto>();

        public IEnumerable<LookupDto> Criteria { get; set; } = new List<LookupDto>();
    }
}
