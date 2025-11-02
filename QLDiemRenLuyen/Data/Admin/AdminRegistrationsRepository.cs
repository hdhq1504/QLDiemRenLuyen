using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using QLDiemRenLuyen.Admin.Models.ViewModels;
using QLDiemRenLuyen.Data;

namespace QLDiemRenLuyen.Admin.Data
{
    /// <summary>
    /// Repository quản lý đăng ký hoạt động cho admin.
    /// </summary>
    public class AdminRegistrationsRepository
    {
        private readonly Database _db;
        private readonly ILogger<AdminRegistrationsRepository> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AdminRegistrationsRepository(Database db, ILogger<AdminRegistrationsRepository> logger, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<(IEnumerable<RegistrationRowVm> items, int total)> GetByActivityAsync(string activityId, string? status, string? keyword, int page, int pageSize)
        {
            const string sql = @"
                SELECT * FROM (
                    SELECT r.ID,
                           r.STUDENT_ID,
                           u.FULL_NAME,
                           u.EMAIL,
                           r.STATUS,
                           r.REGISTERED_AT,
                           r.CHECKED_IN_AT,
                           ROW_NUMBER() OVER (ORDER BY r.REGISTERED_AT DESC NULLS LAST) AS RN
                      FROM REGISTRATIONS r
                      JOIN USERS u ON u.MAND = r.STUDENT_ID
                     WHERE r.ACTIVITY_ID = :activityId
                       AND (:status = 'ALL' OR r.STATUS = :status)
                       AND (:keyword IS NULL OR LOWER(u.FULL_NAME) LIKE '%' || LOWER(:keyword) || '%' OR LOWER(u.EMAIL) LIKE '%' || LOWER(:keyword) || '%')
                )
                WHERE RN BETWEEN :startRow AND :endRow";

            const string countSql = @"
                SELECT COUNT(*)
                  FROM REGISTRATIONS r
                  JOIN USERS u ON u.MAND = r.STUDENT_ID
                 WHERE r.ACTIVITY_ID = :activityId
                   AND (:status = 'ALL' OR r.STATUS = :status)
                   AND (:keyword IS NULL OR LOWER(u.FULL_NAME) LIKE '%' || LOWER(:keyword) || '%' OR LOWER(u.EMAIL) LIKE '%' || LOWER(:keyword) || '%')";

            var items = new List<RegistrationRowVm>();
            var startRow = (page - 1) * pageSize + 1;
            var endRow = page * pageSize;

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();

            await using (var cmd = new OracleCommand(sql, conn) { BindByName = true })
            {
                cmd.Parameters.Add(new OracleParameter("activityId", activityId) { OracleDbType = OracleDbType.Varchar2 });
                cmd.Parameters.Add(new OracleParameter("status", string.IsNullOrWhiteSpace(status) ? "ALL" : status));
                cmd.Parameters.Add(new OracleParameter("keyword", string.IsNullOrWhiteSpace(keyword) ? (object)DBNull.Value : keyword!.Trim()));
                cmd.Parameters.Add(new OracleParameter("startRow", startRow));
                cmd.Parameters.Add(new OracleParameter("endRow", endRow));

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(MapRegistration(reader));
                }
            }

            int total;
            await using (var countCmd = new OracleCommand(countSql, conn) { BindByName = true })
            {
                countCmd.Parameters.Add(new OracleParameter("activityId", activityId) { OracleDbType = OracleDbType.Varchar2 });
                countCmd.Parameters.Add(new OracleParameter("status", string.IsNullOrWhiteSpace(status) ? "ALL" : status));
                countCmd.Parameters.Add(new OracleParameter("keyword", string.IsNullOrWhiteSpace(keyword) ? (object)DBNull.Value : keyword!.Trim()));

                total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            }

            return (items, total);
        }

        public async Task<bool> UpdateStatusAsync(string registrationId, string status)
        {
            const string sql = @"
                UPDATE REGISTRATIONS
                   SET STATUS = :status,
                       CHECKED_IN_AT = CASE WHEN :status = 'CHECKED_IN' THEN NVL(CHECKED_IN_AT, SYSTIMESTAMP)
                                            WHEN :status = 'REGISTERED' THEN NULL
                                            ELSE CHECKED_IN_AT
                                       END
                 WHERE ID = :id";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("status", status));
            cmd.Parameters.Add(new OracleParameter("id", registrationId) { OracleDbType = OracleDbType.Varchar2 });

            var affected = await cmd.ExecuteNonQueryAsync();
            if (affected > 0)
            {
                var action = status switch
                {
                    "CANCELLED" => "REG_CANCEL",
                    "REGISTERED" => "REG_REREGISTER",
                    "CHECKED_IN" => "ATT_MARK",
                    _ => "REG_UPDATE"
                };
                await LogAuditAsync(action, new { registrationId, status });
            }

            return affected > 0;
        }

        public async Task<IEnumerable<RegistrationExportRow>> ExportAsync(string activityId)
        {
            const string sql = @"
                SELECT r.STUDENT_ID,
                       u.FULL_NAME,
                       u.EMAIL,
                       r.STATUS,
                       r.REGISTERED_AT,
                       r.CHECKED_IN_AT
                  FROM REGISTRATIONS r
                  JOIN USERS u ON u.MAND = r.STUDENT_ID
                 WHERE r.ACTIVITY_ID = :activityId
                 ORDER BY r.REGISTERED_AT";

            var list = new List<RegistrationExportRow>();

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("activityId", activityId) { OracleDbType = OracleDbType.Varchar2 });

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new RegistrationExportRow
                {
                    StudentId = reader.GetString(0),
                    FullName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Email = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Status = reader.GetString(3),
                    RegisteredAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    CheckedInAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
                });
            }

            return list;
        }

        private static RegistrationRowVm MapRegistration(OracleDataReader reader)
        {
            return new RegistrationRowVm
            {
                RegistrationId = reader.GetString(reader.GetOrdinal("ID")),
                StudentId = reader.GetString(reader.GetOrdinal("STUDENT_ID")),
                FullName = reader.IsDBNull(reader.GetOrdinal("FULL_NAME")) ? string.Empty : reader.GetString(reader.GetOrdinal("FULL_NAME")),
                Email = reader.IsDBNull(reader.GetOrdinal("EMAIL")) ? string.Empty : reader.GetString(reader.GetOrdinal("EMAIL")),
                Status = reader.GetString(reader.GetOrdinal("STATUS")),
                RegisteredAt = reader.IsDBNull(reader.GetOrdinal("REGISTERED_AT")) ? null : reader.GetDateTime(reader.GetOrdinal("REGISTERED_AT")),
                CheckedInAt = reader.IsDBNull(reader.GetOrdinal("CHECKED_IN_AT")) ? null : reader.GetDateTime(reader.GetOrdinal("CHECKED_IN_AT"))
            };
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
