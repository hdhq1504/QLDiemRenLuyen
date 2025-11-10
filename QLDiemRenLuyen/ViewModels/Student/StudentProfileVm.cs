using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QLDiemRenLuyen.ViewModels.Student
{
    /// <summary>
    /// ViewModel hiển thị thông tin hồ sơ sinh viên.
    /// </summary>
    public class StudentProfileVm
    {
        public string StudentId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string? StudentCode { get; set; }
        public int? ClassId { get; set; }
        public string? ClassName { get; set; }
        public int? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        public DateTime? Dob { get; set; }
        public string? Gender { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public IEnumerable<LookupDto> Classes { get; set; } = Array.Empty<LookupDto>();
        public IEnumerable<LookupDto> Departments { get; set; } = Array.Empty<LookupDto>();
    }

    /// <summary>
    /// ViewModel phục vụ cập nhật thông tin hồ sơ.
    /// </summary>
    public class StudentProfileEditVm
    {
        [Required(ErrorMessage = "Họ tên bắt buộc")] // Bắt buộc từ 2 đến 100 ký tự
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên phải từ 2-100 ký tự")]
        public string FullName { get; set; } = string.Empty;
        public string? StudentCode { get; set; }
        public int? ClassId { get; set; }
        public int? DepartmentId { get; set; }
        [DataType(DataType.Date)]
        public DateTime? Dob { get; set; }
        [StringLength(10, ErrorMessage = "Giới tính tối đa 10 ký tự")]
        public string? Gender { get; set; }
        [StringLength(30, ErrorMessage = "Số điện thoại tối đa 30 ký tự")]
        public string? Phone { get; set; }
        [StringLength(255, ErrorMessage = "Địa chỉ tối đa 255 ký tự")]
        public string? Address { get; set; }
    }

    /// <summary>
    /// DTO dùng cho dropdown.
    /// </summary>
    public class LookupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
