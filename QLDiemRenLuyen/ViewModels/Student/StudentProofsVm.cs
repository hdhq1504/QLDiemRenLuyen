using System;
using System.Collections.Generic;
using QLDiemRenLuyen.ViewModels.Common;

namespace QLDiemRenLuyen.ViewModels.Student
{
    /// <summary>
    /// ViewModel trang quản lý minh chứng của sinh viên.
    /// </summary>
    public class StudentProofsVm
    {
        public string StudentId { get; set; } = string.Empty;
        public IEnumerable<ActivityLookupDto> Activities { get; set; } = new List<ActivityLookupDto>();
        public int? SelectedActivityId { get; set; }
        public string? Keyword { get; set; }
        public PagedList<ProofItemVm> Items { get; set; } = new();
    }

    /// <summary>
    /// DTO dùng cho danh sách hoạt động hiển thị trên dropdown.
    /// </summary>
    public class ActivityLookupDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
    }

    /// <summary>
    /// ViewModel của từng bản ghi minh chứng.
    /// </summary>
    public class ProofItemVm
    {
        public long Id { get; set; }
        public int ActivityId { get; set; }
        public string ActivityTitle { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    /// <summary>
    /// DTO tạo mới minh chứng lưu xuống DB.
    /// </summary>
    public class NewProofDto
    {
        public int ActivityId { get; set; }
        public long RegistrationId { get; set; }
        public string StudentId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string StoredPath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string? Sha256Hex { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>
    /// Thông tin đăng ký hoạt động của sinh viên.
    /// </summary>
    public class RegistrationContext
    {
        public long RegistrationId { get; set; }
        public string RegStatus { get; set; } = string.Empty;
    }

    /// <summary>
    /// ViewModel cho modal tải lên minh chứng.
    /// </summary>
    public class ProofUploadModalVm
    {
        public IEnumerable<ActivityLookupDto> Activities { get; set; } = new List<ActivityLookupDto>();
        public int? SelectedActivityId { get; set; }
    }
}
