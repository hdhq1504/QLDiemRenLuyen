using System;
using System.Collections.Generic;
using QLDiemRenLuyen.Models.ViewModels;

namespace QLDiemRenLuyen.Admin.Models.ViewModels
{
    public class AdminRegistrationsVm
    {
        public string? ActivityId { get; set; }
        public string? ActivityTitle { get; set; }
        public string Status { get; set; } = "ALL";
        public string? Keyword { get; set; }
        public PagedList<RegistrationRowVm> Items { get; set; } = new();
    }

    public class RegistrationRowVm
    {
        public string RegistrationId { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? RegisteredAt { get; set; }
        public DateTime? CheckedInAt { get; set; }
    }

    public class RegistrationExportRow
    {
        public string StudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? RegisteredAt { get; set; }
        public DateTime? CheckedInAt { get; set; }
    }
}