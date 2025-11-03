using System;
using QLDiemRenLuyen.Models.ViewModels;

namespace QLDiemRenLuyen.Models.ViewModels.Admin
{
    public class AdminAttendanceVm
    {
        public string? ActivityId { get; set; }
        public string? ActivityTitle { get; set; }
        public string? Keyword { get; set; }
        public PagedList<AttendanceRowVm> Items { get; set; } = new();
    }

    public class AttendanceRowVm
    {
        public string RegistrationId { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsCheckedIn { get; set; }
        public DateTime? CheckedInAt { get; set; }
    }
}