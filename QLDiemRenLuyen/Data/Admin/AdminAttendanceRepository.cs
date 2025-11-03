using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using QLDiemRenLuyen.Models.ViewModels.Admin;
using QLDiemRenLuyen.Data;

namespace QLDiemRenLuyen.Data.Admin
{
    /// <summary>
    /// Repository quản lý điểm danh hoạt động.
    /// </summary>
    public class AdminAttendanceRepository
    {
        private readonly Database _db;
        private readonly ILogger<AdminAttendanceRepository> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AdminAttendanceRepository(Database db, ILogger<AdminAttendanceRepository> logger, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<(IEnumerable<AttendanceRowVm> items, int total)> GetByActivityAsync(string activityId, string? keyword, int page, int pageSize)
        {
            const string sql = @"
                SELECT * FROM (
                    SELECT r.ID,
                           r.STUDENT_ID,
                           u.FULL_NAME,
                           u.EMAIL,
                           r.STATUS,
                           r.CHECKED_IN_AT,
                           ROW_NUMBER() OVER (ORDER BY r.CHECKED_IN_AT DESC NULLS LAST, r.REGISTERED_AT DESC) AS RN
                      FROM REGISTRATIONS r
                      JOIN USERS u ON u.MAND = r.STUDENT_ID
                     WHERE r.ACTIVITY_ID = :activityId
                       AND (:keyword IS NULL OR LOWER(u.FULL_NAME) LIKE '%' || LOWER(:keyword) || '%' OR LOWER(u.EMAIL) LIKE '%' || LOWER(:keyword) || '%')
                )
                WHERE RN BETWEEN :startRow AND :endRow";

            const string countSql = @"
                SELECT COUNT(*)
                  FROM REGISTRATIONS r
                  JOIN USERS u ON u.MAND = r.STUDENT_ID
                 WHERE r.ACTIVITY_ID = :activityId
                   AND (:keyword IS NULL OR LOWER(u.FULL_NAME) LIKE '%' || LOWER(:keyword) || '%' OR LOWER(u.EMAIL) LIKE '%' || LOWER(:keyword) || '%')";

            var items = new List<AttendanceRowVm>();
            var startRow = (page - 1) * pageSize + 1;
            var endRow = page * pageSize;

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();

            await using (var cmd = new OracleCommand(sql, conn) { BindByName = true })
            {
                cmd.Parameters.Add(new OracleParameter("activityId", activityId) { OracleDbType = OracleDbType.Varchar2 });
                cmd.Parameters.Add(new OracleParameter("keyword", string.IsNullOrWhiteSpace(keyword) ? (object)DBNull.Value : keyword!.Trim()));
                cmd.Parameters.Add(new OracleParameter("startRow", startRow));
                cmd.Parameters.Add(new OracleParameter("endRow", endRow));

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new AttendanceRowVm
                    {
                        RegistrationId = reader.GetString(reader.GetOrdinal("ID")),
                        StudentId = reader.GetString(reader.GetOrdinal("STUDENT_ID")),
                        FullName = reader.IsDBNull(reader.GetOrdinal("FULL_NAME")) ? string.Empty : reader.GetString(reader.GetOrdinal("FULL_NAME")),
                        Email = reader.IsDBNull(reader.GetOrdinal("EMAIL")) ? string.Empty : reader.GetString(reader.GetOrdinal("EMAIL")),
                        IsCheckedIn = string.Equals(reader.GetString(reader.GetOrdinal("STATUS")), "CHECKED_IN", StringComparison.OrdinalIgnoreCase),
                        CheckedInAt = reader.IsDBNull(reader.GetOrdinal("CHECKED_IN_AT")) ? null : reader.GetDateTime(reader.GetOrdinal("CHECKED_IN_AT"))
                    });
                }
            }

            int total;
            await using (var countCmd = new OracleCommand(countSql, conn) { BindByName = true })
            {
                countCmd.Parameters.Add(new OracleParameter("activityId", activityId) { OracleDbType = OracleDbType.Varchar2 });
                countCmd.Parameters.Add(new OracleParameter("keyword", string.IsNullOrWhiteSpace(keyword) ? (object)DBNull.Value : keyword!.Trim()));
                total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            }

            return (items, total);
        }

        public async Task<bool> MarkAsync(string registrationId, bool isCheckedIn)
        {
            const string sql = @"
                UPDATE REGISTRATIONS
                   SET STATUS = CASE WHEN :checked = 1 THEN 'CHECKED_IN' ELSE 'REGISTERED' END,
                       CHECKED_IN_AT = CASE WHEN :checked = 1 THEN SYSTIMESTAMP ELSE NULL END
                 WHERE ID = :id";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("checked", isCheckedIn ? 1 : 0));
            cmd.Parameters.Add(new OracleParameter("id", registrationId) { OracleDbType = OracleDbType.Varchar2 });

            var affected = await cmd.ExecuteNonQueryAsync();
            if (affected > 0)
            {
                await LogAuditAsync(isCheckedIn ? "ATT_MARK" : "ATT_UNMARK", new { registrationId });
            }

            return affected > 0;
        }

        public async Task<int> ImportAsync(string activityId, IEnumerable<string> studentIdsOrEmails)
        {
            const string findSql = @"
                SELECT r.ID
                  FROM REGISTRATIONS r
                  JOIN USERS u ON u.MAND = r.STUDENT_ID
                 WHERE r.ACTIVITY_ID = :activityId
                   AND (r.STUDENT_ID = :sid OR LOWER(u.EMAIL) = LOWER(:email))
                 FETCH FIRST 1 ROWS ONLY";

            const string updateSql = @"
                UPDATE REGISTRATIONS
                   SET STATUS = 'CHECKED_IN',
                       CHECKED_IN_AT = SYSTIMESTAMP
                 WHERE ID = :id";

            var identifiers = studentIdsOrEmails
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (identifiers.Count == 0)
            {
                return 0;
            }

            var success = 0;
            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();

            foreach (var identifier in identifiers)
            {
                await using var findCmd = new OracleCommand(findSql, conn) { BindByName = true };
                findCmd.Parameters.Add(new OracleParameter("activityId", activityId) { OracleDbType = OracleDbType.Varchar2 });
                findCmd.Parameters.Add(new OracleParameter("sid", identifier));
                findCmd.Parameters.Add(new OracleParameter("email", identifier));

                await using var reader = await findCmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if (!await reader.ReadAsync())
                {
                    continue;
                }

                var registrationId = reader.GetString(0);

                await using var updateCmd = new OracleCommand(updateSql, conn) { BindByName = true };
                updateCmd.Parameters.Add(new OracleParameter("id", registrationId) { OracleDbType = OracleDbType.Varchar2 });
                var affected = await updateCmd.ExecuteNonQueryAsync();
                if (affected > 0)
                {
                    success++;
                }
            }

            if (success > 0)
            {
                await LogAuditAsync("ATT_IMPORT", new { activityId, success, total = identifiers.Count });
            }

            return success;
        }

        private async Task LogAuditAsync(string action, object details)
        {
            try
            {
                const string sql = @"INSERT INTO AUDIT_TRAIL (WHO, ACTION, EVENT_AT_UTC, CLIENT_IP, USER_AGENT, DETAILS)
                                     VALUES (:who, :action, SYS_EXTRACT_UTC(SYSTIMESTAMP), :ip, :ua, :details)";
                var ctx = _httpContextAccessor.HttpContext;
                var adminId = ctx?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? ctx?.User?.FindFirst("mand")?.Value
                              ?? string.Empty;
                var ip = ctx?.Connection?.RemoteIpAddress?.ToString();
                var ua = ctx?.Request?.Headers["User-Agent"].ToString();
                var json = JsonSerializer.Serialize(details, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                var parameters = new[]
                {
                    new OracleParameter("who", adminId) { OracleDbType = OracleDbType.Varchar2 },
                    new OracleParameter("action", action) { OracleDbType = OracleDbType.Varchar2 },
                    new OracleParameter("ip", (object?)ip ?? DBNull.Value),
                    new OracleParameter("ua", (object?)ua ?? DBNull.Value),
                    new OracleParameter("details", json)
                };

                await _db.ExecuteAsync(sql, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể ghi audit trail cho hành động {Action}", action);
            }
        }
    }
}
