using System;
using System.Collections.Generic;

namespace QLDiemRenLuyen.Student.Models.ViewModels
{
    /// <summary>
    /// ViewModel tổng hợp thông tin điểm rèn luyện cho sinh viên.
    /// </summary>
    public class StudentScoreVm
    {
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? SelectedTermId { get; set; }
        public string? SelectedTermName { get; set; }
        public decimal BaseScore { get; set; } = 70m;
        public decimal ActivityScore { get; set; } = 0m;
        public decimal Adjustments { get; set; } = 0m;
        public decimal Total { get; set; } = 0m;
        public string? Status { get; set; }
        public string Classification { get; set; } = string.Empty;
        public IEnumerable<CriterionScoreVm> Breakdown { get; set; } = new List<CriterionScoreVm>();
        public IEnumerable<TermDto> Terms { get; set; } = new List<TermDto>();
        public IEnumerable<TermScoreVm> HistoryPreview { get; set; } = new List<TermScoreVm>();
        public IEnumerable<ActivityContributionVm> RecentActivities { get; set; } = new List<ActivityContributionVm>();
    }

    /// <summary>
    /// Điểm đạt được theo từng tiêu chí.
    /// </summary>
    public class CriterionScoreVm
    {
        public string CriterionId { get; set; } = string.Empty;
        public int GroupNo { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Earned { get; set; }
        public decimal MaxPoint { get; set; }
    }

    /// <summary>
    /// DTO mô tả học kỳ.
    /// </summary>
    public class TermDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO lịch sử điểm theo từng học kỳ.
    /// </summary>
    public class TermScoreVm
    {
        public string TermId { get; set; } = string.Empty;
        public string TermName { get; set; } = string.Empty;
        public decimal Total { get; set; } = 0m;
        public string Classification { get; set; } = string.Empty;
    }

    /// <summary>
    /// Hoạt động đóng góp vào điểm rèn luyện gần đây.
    /// </summary>
    public class ActivityContributionVm
    {
        public string Title { get; set; } = string.Empty;
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public decimal Points { get; set; }
    }
}
