namespace QLDiemRenLuyen.ViewModels.Lecturer
{
    /// <summary>
    /// Dòng dữ liệu thông tin lớp mà giảng viên quản lý.
    /// </summary>
    public class ClassRowVm
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
    }

    /// <summary>
    /// Dòng dữ liệu điểm rèn luyện của sinh viên trong lớp.
    /// </summary>
    public class StudentScoreRowVm
    {
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
        public string TermName { get; set; } = string.Empty;
    }
}
