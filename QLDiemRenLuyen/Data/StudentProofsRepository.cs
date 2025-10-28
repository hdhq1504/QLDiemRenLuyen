using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using QLDiemRenLuyen.Models.ViewModels;

namespace QLDiemRenLuyen.Data
{
    /// <summary>
    /// Repository quản lý dữ liệu minh chứng cho sinh viên.
    /// </summary>
    public class StudentProofsRepository
    {
        private readonly Database _db;
        private readonly ILogger<StudentProofsRepository> _logger;

        public StudentProofsRepository(Database db, ILogger<StudentProofsRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Lấy danh sách hoạt động mà sinh viên đã đăng ký.
        /// </summary>
        public async Task<IEnumerable<ActivityLookupDto>> GetMyActivitiesAsync(string studentId)
        {
            const string sql = @"SELECT a.ID, a.TITLE, a.START_AT, a.END_AT
                                  FROM REGISTRATIONS r
                                  JOIN ACTIVITIES a ON a.ID = r.ACTIVITY_ID
                                 WHERE r.STUDENT_ID = :sid
                                   AND r.STATUS IN ('REGISTERED', 'CHECKED_IN')
                                 GROUP BY a.ID, a.TITLE, a.START_AT, a.END_AT
                                 ORDER BY a.START_AT DESC";

            var items = new List<ActivityLookupDto>();

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("sid", OracleDbType.Varchar2, studentId, ParameterDirection.Input));

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new ActivityLookupDto
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    StartAt = reader.GetDateTime(2),
                    EndAt = reader.GetDateTime(3)
                });
            }

            return items;
        }

        /// <summary>
        /// Lấy danh sách minh chứng có phân trang.
        /// </summary>
        public async Task<PagedList<ProofItemVm>> GetMyProofsAsync(string studentId, int page, int pageSize, int? activityId, string? keyword)
        {
            const string listSql = @"
                SELECT p.ID,
                       p.ACTIVITY_ID,
                       a.TITLE,
                       p.FILE_NAME,
                       p.FILE_SIZE,
                       p.CONTENT_TYPE,
                       p.STATUS,
                       p.CREATED_AT_UTC
                  FROM PROOFS p
                  JOIN ACTIVITIES a ON a.ID = p.ACTIVITY_ID
                 WHERE p.STUDENT_ID = :sid
                   AND (:aid IS NULL OR p.ACTIVITY_ID = :aid)
                   AND (:kw IS NULL OR LOWER(p.FILE_NAME) LIKE '%' || :kw || '%' OR LOWER(p.NOTE) LIKE '%' || :kw || '%')
                 ORDER BY p.CREATED_AT_UTC DESC
                 OFFSET :offset ROWS FETCH NEXT :pageSize ROWS ONLY";

            const string countSql = @"
                SELECT COUNT(*)
                  FROM PROOFS p
                 WHERE p.STUDENT_ID = :sid
                   AND (:aid IS NULL OR p.ACTIVITY_ID = :aid)
                   AND (:kw IS NULL OR LOWER(p.FILE_NAME) LIKE '%' || :kw || '%' OR LOWER(p.NOTE) LIKE '%' || :kw || '%')";

            var result = new PagedList<ProofItemVm>
            {
                Page = page,
                PageSize = pageSize,
                Data = new List<ProofItemVm>()
            };

            var offset = (page - 1) * pageSize;
            var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim().ToLowerInvariant();

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();

            await using (var cmd = new OracleCommand(listSql, conn) { BindByName = true })
            {
                cmd.Parameters.Add(new OracleParameter("sid", OracleDbType.Varchar2, studentId, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("aid", OracleDbType.Int32, (object?)activityId ?? DBNull.Value, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("kw", OracleDbType.Varchar2, (object?)kw ?? DBNull.Value, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("offset", OracleDbType.Int32, offset, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("pageSize", OracleDbType.Int32, pageSize, ParameterDirection.Input));

                var list = (List<ProofItemVm>)result.Data;
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new ProofItemVm
                    {
                        Id = reader.GetInt64(0),
                        ActivityId = reader.GetInt32(1),
                        ActivityTitle = reader.GetString(2),
                        FileName = reader.GetString(3),
                        FileSize = reader.GetInt64(4),
                        ContentType = reader.GetString(5),
                        Status = reader.GetString(6),
                        CreatedAtUtc = reader.GetDateTime(7)
                    });
                }
            }

            await using (var countCmd = new OracleCommand(countSql, conn) { BindByName = true })
            {
                countCmd.Parameters.Add(new OracleParameter("sid", OracleDbType.Varchar2, studentId, ParameterDirection.Input));
                countCmd.Parameters.Add(new OracleParameter("aid", OracleDbType.Int32, (object?)activityId ?? DBNull.Value, ParameterDirection.Input));
                countCmd.Parameters.Add(new OracleParameter("kw", OracleDbType.Varchar2, (object?)kw ?? DBNull.Value, ParameterDirection.Input));

                var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                result.TotalItems = total;
                result.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            }

            return result;
        }

        /// <summary>
        /// Kiểm tra đăng ký của sinh viên theo hoạt động.
        /// </summary>
        public async Task<RegistrationContext?> GetRegistrationAsync(string studentId, int activityId)
        {
            const string sql = @"SELECT r.ID, r.STATUS FROM REGISTRATIONS r WHERE r.STUDENT_ID = :sid AND r.ACTIVITY_ID = :aid";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("sid", OracleDbType.Varchar2, studentId, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("aid", OracleDbType.Int32, activityId, ParameterDirection.Input));

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (await reader.ReadAsync())
            {
                return new RegistrationContext
                {
                    RegistrationId = reader.GetInt64(0),
                    RegStatus = reader.GetString(1)
                };
            }

            return null;
        }

        /// <summary>
        /// Tạo bản ghi minh chứng mới.
        /// </summary>
        public async Task<long> CreateAsync(NewProofDto dto)
        {
            const string sql = @"INSERT INTO PROOFS (REGISTRATION_ID, STUDENT_ID, ACTIVITY_ID, FILE_NAME, STORED_PATH, CONTENT_TYPE, FILE_SIZE, SHA256_HEX, NOTE, STATUS)
                                  VALUES (:regId, :sid, :aid, :fileName, :path, :ct, :size, :sha, :note, 'SUBMITTED')
                                  RETURNING ID INTO :newId";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };

            cmd.Parameters.Add(new OracleParameter("regId", OracleDbType.Int64, dto.RegistrationId, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("sid", OracleDbType.Varchar2, dto.StudentId, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("aid", OracleDbType.Int32, dto.ActivityId, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("fileName", OracleDbType.Varchar2, dto.FileName, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("path", OracleDbType.Varchar2, dto.StoredPath, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("ct", OracleDbType.Varchar2, dto.ContentType, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("size", OracleDbType.Int64, dto.FileSize, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("sha", OracleDbType.Varchar2, (object?)dto.Sha256Hex ?? DBNull.Value, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("note", OracleDbType.Varchar2, (object?)dto.Note ?? DBNull.Value, ParameterDirection.Input));
            var idParam = new OracleParameter("newId", OracleDbType.Int64, ParameterDirection.Output);
            cmd.Parameters.Add(idParam);

            await cmd.ExecuteNonQueryAsync();
            return Convert.ToInt64(idParam.Value.ToString());
        }

        /// <summary>
        /// Xóa minh chứng khi còn trạng thái SUBMITTED.
        /// </summary>
        public async Task<bool> DeleteAsync(long proofId, string studentId)
        {
            const string sql = @"DELETE FROM PROOFS WHERE ID = :id AND STUDENT_ID = :sid AND STATUS = 'SUBMITTED'";

            var parameters = new[]
            {
                new OracleParameter("id", OracleDbType.Int64, proofId, ParameterDirection.Input),
                new OracleParameter("sid", OracleDbType.Varchar2, studentId, ParameterDirection.Input)
            };

            var affected = await _db.ExecuteAsync(sql, parameters);
            return affected > 0;
        }

        /// <summary>
        /// Lấy thông tin chi tiết của minh chứng thuộc về sinh viên.
        /// </summary>
        public async Task<ProofFileDescriptor?> GetProofAsync(long proofId, string studentId)
        {
            const string sql = @"SELECT ID, ACTIVITY_ID, FILE_NAME, STORED_PATH, CONTENT_TYPE, FILE_SIZE, STATUS
                                    FROM PROOFS
                                   WHERE ID = :id AND STUDENT_ID = :sid";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Int64, proofId, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("sid", OracleDbType.Varchar2, studentId, ParameterDirection.Input));

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (await reader.ReadAsync())
            {
                return new ProofFileDescriptor
                {
                    Id = reader.GetInt64(0),
                    ActivityId = reader.GetInt32(1),
                    FileName = reader.GetString(2),
                    StoredPath = reader.GetString(3),
                    ContentType = reader.GetString(4),
                    FileSize = reader.GetInt64(5),
                    Status = reader.GetString(6)
                };
            }

            return null;
        }

        /// <summary>
        /// Kiểm tra trùng hash minh chứng đang chờ duyệt.
        /// </summary>
        public async Task<bool> HasDuplicateHashAsync(string studentId, int activityId, string sha256Hex)
        {
            const string sql = @"SELECT COUNT(*) FROM PROOFS WHERE STUDENT_ID = :sid AND ACTIVITY_ID = :aid AND STATUS = 'SUBMITTED' AND SHA256_HEX = :sha";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("sid", OracleDbType.Varchar2, studentId, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("aid", OracleDbType.Int32, activityId, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("sha", OracleDbType.Varchar2, sha256Hex, ParameterDirection.Input));

            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            return count > 0;
        }

        /// <summary>
        /// Ghi log AUDIT_TRAIL.
        /// </summary>
        public async Task WriteAuditAsync(string studentId, string action, string clientIp, string userAgent, object details)
        {
            const string sql = @"INSERT INTO AUDIT_TRAIL (WHO, ACTION, EVENT_AT_UTC, CLIENT_IP, USER_AGENT, DETAILS)
                                  VALUES (:sid, :action, SYS_EXTRACT_UTC(SYSTIMESTAMP), :ip, :ua, :details)";

            var json = JsonSerializer.Serialize(details);
            var parameters = new[]
            {
                new OracleParameter("sid", OracleDbType.Varchar2, studentId, ParameterDirection.Input),
                new OracleParameter("action", OracleDbType.Varchar2, action, ParameterDirection.Input),
                new OracleParameter("ip", OracleDbType.Varchar2, clientIp ?? "unknown", ParameterDirection.Input),
                new OracleParameter("ua", OracleDbType.Varchar2, userAgent ?? "unknown", ParameterDirection.Input),
                new OracleParameter("details", OracleDbType.Clob, json, ParameterDirection.Input)
            };

            try
            {
                await _db.ExecuteAsync(sql, parameters);
            }
            catch (OracleException ex)
            {
                _logger.LogWarning(ex, "Không thể ghi AUDIT_TRAIL cho sinh viên {StudentId} - action {Action}", studentId, action);
            }
        }
    }

    /// <summary>
    /// DTO mô tả tệp minh chứng phục vụ tải xuống/xóa.
    /// </summary>
    public class ProofFileDescriptor
    {
        public long Id { get; set; }
        public int ActivityId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string StoredPath { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
