using QLDiemRenLuyen.ViewModels.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QLDiemRenLuyen.ViewModels.Student
{
    /// <summary>
    /// ViewModel trang danh sách phản hồi điểm rèn luyện của sinh viên.
    /// </summary>
    public class StudentFeedbackVm
    {
        public string StudentId { get; set; } = string.Empty;

        public string? SelectedTermId { get; set; }

        public string? Keyword { get; set; }

        public IEnumerable<TermDto> Terms { get; set; } = new List<TermDto>();

        public PagedList<FeedbackItemVm> Items { get; set; } = new();
    }

    /// <summary>
    /// Bản ghi phản hồi hiển thị trên bảng danh sách.
    /// </summary>
    public class FeedbackItemVm
    {
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string TermName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Dữ liệu form tạo/sửa phản hồi.
    /// </summary>
    public class FeedbackEditVm
    {
        [Required(ErrorMessage = "Vui lòng chọn học kỳ.")]
        public string TermId { get; set; } = string.Empty;

        public string? CriterionId { get; set; }

        [Required(ErrorMessage = "Tiêu đề bắt buộc.")]
        [StringLength(255, ErrorMessage = "Tiêu đề tối đa 255 ký tự.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nội dung bắt buộc.")]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "Nội dung từ 10 đến 1000 ký tự.")]
        public string Content { get; set; } = string.Empty;

        public string SubmitStatus { get; set; } = "SUBMITTED";
    }

    /// <summary>
    /// Dữ liệu chi tiết phản hồi dùng cho modal xem chi tiết.
    /// </summary>
    public class FeedbackDetailVm
    {
        public string Id { get; set; } = string.Empty;

        public string TermId { get; set; } = string.Empty;

        public string? CriterionId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string? Response { get; set; }

        public string TermName { get; set; } = string.Empty;

        public string? CriterionName { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? RespondedAt { get; set; }
    }

    /// <summary>
    /// DTO tiêu chí (tiêu chuẩn/hoạt động) phục vụ dropdown chọn tiêu chí.
    /// </summary>
    public class CriterionDto
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// ViewModel truyền dữ liệu vào modal tạo/sửa.
    /// </summary>
    public class FeedbackEditModalVm
    {
        public string? FeedbackId { get; set; }

        public bool IsEdit { get; set; }

        public string Status { get; set; } = "DRAFT";

        public string FormAction { get; set; } = string.Empty;

        public FeedbackEditVm Feedback { get; set; } = new();

        public IEnumerable<TermDto> Terms { get; set; } = new List<TermDto>();

        public IEnumerable<CriterionDto> Criteria { get; set; } = new List<CriterionDto>();
    }
}