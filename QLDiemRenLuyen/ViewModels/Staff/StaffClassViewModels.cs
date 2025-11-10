using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using QLDiemRenLuyen.ViewModels.Common;

namespace QLDiemRenLuyen.ViewModels.Staff
{
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
}