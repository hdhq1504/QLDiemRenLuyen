using System;
using System.Collections.Generic;
using QLDiemRenLuyen.Models.ViewModels;

namespace QLDiemRenLuyen.Admin.Models.ViewModels
{
    /// <summary>
    /// ViewModel trang danh sách hoạt động dành cho admin.
    /// </summary>
    public class AdminActivitiesVm
    {
        public string? SelectedTermId { get; set; }

        public string? Keyword { get; set; }

        public string ApprovalFilter { get; set; } = "ALL";

        public string StatusFilter { get; set; } = "ALL";

        public IEnumerable<TermDto> Terms { get; set; } = Array.Empty<TermDto>();

        public PagedList<ActivityRowVm> Items { get; set; } = new();
    }

    public class TermDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class ActivityRowVm
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? TermName { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ApprovalStatus { get; set; } = string.Empty;
        public string? OrganizerName { get; set; }
        public int? MaxSeats { get; set; }
        public int RegisteredCount { get; set; }
        public string? Location { get; set; }
        public decimal? Points { get; set; }
    }

    public class ActivityEditVm
    {
        public string? Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? TermId { get; set; }
        public string? CriterionId { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public string Status { get; set; } = "OPEN";
        public int? MaxSeats { get; set; }
        public string? Location { get; set; }
        public decimal? Points { get; set; }
        public string? OrganizerId { get; set; }
    }

    public class ActivityDetailVm : ActivityEditVm
    {
        public string? TermName { get; set; }
        public string? CriterionName { get; set; }
        public string ApprovalStatus { get; set; } = "PENDING";
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int RegisteredCount { get; set; }
        public int CheckinCount { get; set; }
        public string? OrganizerName { get; set; }
    }
}