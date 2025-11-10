using System.Collections.Generic;

namespace QLDiemRenLuyen.ViewModels.Common
{
    /// <summary>
    /// Danh sách phân trang dùng chung cho các view.
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu.</typeparam>
    public class PagedList<T>
    {
        public IEnumerable<T> Data { get; set; } = new List<T>();

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalItems { get; set; }

        public int TotalPages { get; set; }
    }
}