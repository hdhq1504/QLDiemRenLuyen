using System;
using System.Collections.Generic;
using System.Data;
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
    /// Repository phục vụ nghiệp vụ hoạt động cho trang quản trị.
    /// </summary>
    public class AdminActivitiesRepository
    {
        private readonly Database _db;
        private readonly ILogger<AdminActivitiesRepository> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AdminActivitiesRepository(Database db, ILogger<AdminActivitiesRepository> logger, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IEnumerable<TermDto>> GetTermsAsync()
        {
            const string sql = @"SELECT ID, NAME FROM TERMS ORDER BY NAME DESC";
            var result = new List<TermDto>();

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            while (await reader.ReadAsync())
            {
                result.Add(new TermDto
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1)
                });
            }

            return result;
        }

        public async Task<(IEnumerable<ActivityRowVm> items, int total)> SearchAsync(
            string? termId,
            string? q,
            string? approval,
            string? status,
            int page,
            int pageSize,
            string adminId,
            string role)
        {
            var items = new List<ActivityRowVm>();
            var filters = new List<string> { "1 = 1" };
            var restrictByOrganizer = string.Equals(role, "ORGANIZER", StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(termId))
            {
                filters.Add("a.TERM_ID = :termId");
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                filters.Add("(LOWER(a.TITLE) LIKE '%' || LOWER(:keyword) || '%' OR REGEXP_LIKE(DBMS_LOB.SUBSTR(a.DESCRIPTION, 4000, 1), :keywordRegex, 'i'))");
            }

            if (!string.IsNullOrWhiteSpace(approval) && !string.Equals(approval, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                filters.Add("a.APPROVAL_STATUS = :approval");
            }

            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                filters.Add("a.STATUS = :status");
            }

            if (restrictByOrganizer)
            {
                filters.Add("a.ORGANIZER_ID = :organizerId");
            }

            var whereClause = string.Join(" AND ", filters);
            var startRow = (page - 1) * pageSize + 1;
            var endRow = page * pageSize;

            var sql = $@"
                SELECT *
                FROM (
                    SELECT
                        a.ID,
                        a.TITLE,
                        t.NAME AS TERM_NAME,
                        a.START_AT,
                        a.END_AT,
                        a.STATUS,
                        a.APPROVAL_STATUS,
                        a.MAX_SEATS,
                        a.POINTS,
                        (SELECT NVL(u.FULL_NAME, u.MAND) FROM USERS u WHERE u.MAND = a.ORGANIZER_ID) AS ORGANIZER_NAME,
                        (SELECT COUNT(*) FROM REGISTRATIONS r WHERE r.ACTIVITY_ID = a.ID AND r.STATUS IN ('REGISTERED','CHECKED_IN')) AS REGISTERED_COUNT,
                        ROW_NUMBER() OVER (ORDER BY a.START_AT DESC NULLS LAST, a.CREATED_AT DESC) AS RN
                    FROM ACTIVITIES a
                    LEFT JOIN TERMS t ON t.ID = a.TERM_ID
                    WHERE {whereClause}
                )
                WHERE RN BETWEEN :startRow AND :endRow";

            var countSql = $"SELECT COUNT(*) FROM ACTIVITIES a WHERE {whereClause}";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();

            await using (var cmd = new OracleCommand(sql, conn) { BindByName = true })
            {
                AddFilterParameters(cmd, termId, q, approval, status, adminId, restrictByOrganizer);
                cmd.Parameters.Add(new OracleParameter("startRow", startRow));
                cmd.Parameters.Add(new OracleParameter("endRow", endRow));

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(MapActivityRow(reader));
                }
            }

            int total;
            await using (var countCmd = new OracleCommand(countSql, conn) { BindByName = true })
            {
                AddFilterParameters(countCmd, termId, q, approval, status, adminId, restrictByOrganizer);
                total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            }

            return (items, total);
        }

        public async Task<ActivityDetailVm?> GetByIdAsync(string id, string adminId, string role)
        {
            const string sql = @"
                SELECT a.ID,
                       a.TITLE,
                       a.DESCRIPTION,
                       a.TERM_ID,
                       t.NAME AS TERM_NAME,
                       a.CRITERION_ID,
                       c.NAME AS CRITERION_NAME,
                       a.START_AT,
                       a.END_AT,
                       a.STATUS,
                       a.MAX_SEATS,
                       a.LOCATION,
                       a.POINTS,
                       a.APPROVAL_STATUS,
                       a.APPROVED_BY,
                       a.APPROVED_AT,
                       a.ORGANIZER_ID,
                       (SELECT NVL(u.FULL_NAME, u.MAND) FROM USERS u WHERE u.MAND = a.ORGANIZER_ID) AS ORGANIZER_NAME,
                       (SELECT COUNT(*) FROM REGISTRATIONS r WHERE r.ACTIVITY_ID = a.ID AND r.STATUS IN ('REGISTERED','CHECKED_IN')) AS REGISTERED_COUNT,
                       (SELECT COUNT(*) FROM REGISTRATIONS r WHERE r.ACTIVITY_ID = a.ID AND r.STATUS = 'CHECKED_IN') AS CHECKIN_COUNT
                  FROM ACTIVITIES a
                  LEFT JOIN TERMS t ON t.ID = a.TERM_ID
                  LEFT JOIN CRITERIA c ON c.ID = a.CRITERION_ID
                 WHERE a.ID = :id";

            var restrictByOrganizer = string.Equals(role, "ORGANIZER", StringComparison.OrdinalIgnoreCase);

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", id) { OracleDbType = OracleDbType.Varchar2 });

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (!await reader.ReadAsync())
            {
                return null;
            }

            var organizerId = reader.IsDBNull(reader.GetOrdinal("ORGANIZER_ID")) ? null : reader.GetString(reader.GetOrdinal("ORGANIZER_ID"));
            if (restrictByOrganizer && !string.Equals(organizerId, adminId, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return MapActivityDetail(reader);
        }

        public async Task<string> CreateAsync(ActivityEditVm vm, string adminId)
        {
            var id = Guid.NewGuid().ToString("N");
            const string sql = @"
                INSERT INTO ACTIVITIES (ID, TITLE, DESCRIPTION, TERM_ID, CRITERION_ID, START_AT, END_AT, STATUS, MAX_SEATS, CREATED_AT, APPROVAL_STATUS, APPROVED_BY, APPROVED_AT, ORGANIZER_ID, LOCATION, POINTS)
                VALUES (:id, :title, :description, :termId, :criterionId, :startAt, :endAt, :status, :maxSeats, SYSTIMESTAMP, :approvalStatus, NULL, NULL, :organizerId, :location, :points)";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };

            cmd.Parameters.Add(new OracleParameter("id", id) { OracleDbType = OracleDbType.Varchar2 });
            cmd.Parameters.Add(new OracleParameter("title", vm.Title));
            cmd.Parameters.Add(new OracleParameter("description", (object?)vm.Description ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("termId", (object?)vm.TermId ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("criterionId", (object?)vm.CriterionId ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("startAt", OracleDbType.TimeStamp) { Value = vm.StartAt.HasValue ? vm.StartAt.Value : (object)DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("endAt", OracleDbType.TimeStamp) { Value = vm.EndAt.HasValue ? vm.EndAt.Value : (object)DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("status", vm.Status));
            cmd.Parameters.Add(new OracleParameter("maxSeats", OracleDbType.Int32) { Value = vm.MaxSeats.HasValue ? vm.MaxSeats.Value : (object)DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("approvalStatus", "PENDING"));
            cmd.Parameters.Add(new OracleParameter("organizerId", (object?)(vm.OrganizerId ?? adminId) ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("location", (object?)vm.Location ?? DBNull.Value));
            cmd.Parameters.Add(new OracleParameter("points", OracleDbType.Decimal) { Value = vm.Points.HasValue ? vm.Points.Value : (object)DBNull.Value });

            await cmd.ExecuteNonQueryAsync();

            await LogAuditAsync(adminId, "ACT_CREATE", new
            {
                activityId = id,
                vm.Title,
                vm.TermId,
                vm.Status
            });

            return id;
        }

        public async Task<bool> UpdateAsync(string id, ActivityEditVm vm, string adminId)
        {
            const string sql = @"
                UPDATE ACTIVITIES
                   SET TITLE = :title,
                       DESCRIPTION = :description,
                       TERM_ID = :termId,
                       CRITERION_ID = :criterionId,
                       START_AT = :startAt,
                       END_AT = :endAt,
                       STATUS = :status,
                       MAX_SEATS = :maxSeats,
                       LOCATION = :location,
                       POINTS = :points
                 WHERE ID = :id";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();

            if (await EnforceOrganizerScopeAsync(conn, id, adminId))
            {
                await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add(new OracleParameter("title", vm.Title));
                cmd.Parameters.Add(new OracleParameter("description", (object?)vm.Description ?? DBNull.Value));
                cmd.Parameters.Add(new OracleParameter("termId", (object?)vm.TermId ?? DBNull.Value));
                cmd.Parameters.Add(new OracleParameter("criterionId", (object?)vm.CriterionId ?? DBNull.Value));
                cmd.Parameters.Add(new OracleParameter("startAt", OracleDbType.TimeStamp) { Value = vm.StartAt.HasValue ? vm.StartAt.Value : (object)DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("endAt", OracleDbType.TimeStamp) { Value = vm.EndAt.HasValue ? vm.EndAt.Value : (object)DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("status", vm.Status));
                cmd.Parameters.Add(new OracleParameter("maxSeats", OracleDbType.Int32) { Value = vm.MaxSeats.HasValue ? vm.MaxSeats.Value : (object)DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("location", (object?)vm.Location ?? DBNull.Value));
                cmd.Parameters.Add(new OracleParameter("points", OracleDbType.Decimal) { Value = vm.Points.HasValue ? vm.Points.Value : (object)DBNull.Value });
                cmd.Parameters.Add(new OracleParameter("id", id) { OracleDbType = OracleDbType.Varchar2 });

                var affected = await cmd.ExecuteNonQueryAsync();
                if (affected > 0)
                {
                    await LogAuditAsync(adminId, "ACT_EDIT", new { activityId = id, vm.Title });
                }
                return affected > 0;
            }

            return false;
        }

        public async Task<bool> SetApprovalAsync(string id, string decision, string adminId, string? reason)
        {
            string sql;
            switch (decision.ToUpperInvariant())
            {
                case "APPROVED":
                    sql = @"UPDATE ACTIVITIES SET APPROVAL_STATUS = 'APPROVED', APPROVED_BY = :adminId, APPROVED_AT = SYSTIMESTAMP WHERE ID = :id";
                    break;
                case "REJECTED":
                    sql = @"UPDATE ACTIVITIES SET APPROVAL_STATUS = 'REJECTED', APPROVED_BY = :adminId, APPROVED_AT = SYSTIMESTAMP WHERE ID = :id";
                    break;
                default:
                    sql = @"UPDATE ACTIVITIES SET APPROVAL_STATUS = 'PENDING', APPROVED_BY = NULL, APPROVED_AT = NULL WHERE ID = :id";
                    break;
            }

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            var role = GetCurrentRole();
            if (string.Equals(role, "ORGANIZER", StringComparison.OrdinalIgnoreCase) && !await BelongsToOrganizerAsync(conn, id, adminId))
            {
                return false;
            }

            var requiresAdmin = sql.Contains(":adminId", StringComparison.OrdinalIgnoreCase);
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            if (requiresAdmin)
            {
                cmd.Parameters.Add(new OracleParameter("adminId", adminId) { OracleDbType = OracleDbType.Varchar2 });
            }
            cmd.Parameters.Add(new OracleParameter("id", id) { OracleDbType = OracleDbType.Varchar2 });

            var affected = await cmd.ExecuteNonQueryAsync();
            if (affected > 0)
            {
                await LogAuditAsync(adminId, decision.ToUpperInvariant() switch
                {
                    "APPROVED" => "ACT_APPROVE",
                    "REJECTED" => "ACT_REJECT",
                    _ => "ACT_SUBMIT"
                }, new { activityId = id, decision, reason });
            }

            return affected > 0;
        }

        public async Task<bool> SetStatusAsync(string id, string status)
        {
            const string sql = @"UPDATE ACTIVITIES SET STATUS = :status WHERE ID = :id";
            var allowed = status == "OPEN" || status == "CLOSED" || status == "FULL";
            if (!allowed)
            {
                return false;
            }

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();

            var adminId = GetCurrentAdminId();
            if (!await EnforceOrganizerScopeAsync(conn, id, adminId))
            {
                return false;
            }

            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("status", status));
            cmd.Parameters.Add(new OracleParameter("id", id) { OracleDbType = OracleDbType.Varchar2 });

            var affected = await cmd.ExecuteNonQueryAsync();
            if (affected > 0)
            {
                var action = status switch
                {
                    "OPEN" => "ACT_OPEN",
                    "CLOSED" => "ACT_CLOSE",
                    "FULL" => "ACT_CLOSE",
                    _ => "ACT_UPDATE_STATUS"
                };
                await LogAuditAsync(adminId, action, new { activityId = id, status });
            }

            return affected > 0;
        }

        public async Task<bool> DeleteAsync(string id, string adminId, string role)
        {
            const string sql = @"DELETE FROM ACTIVITIES WHERE ID = :id"; // TODO: Nếu cần soft delete thì thay bằng cột STATUS.

            var restrictByOrganizer = string.Equals(role, "ORGANIZER", StringComparison.OrdinalIgnoreCase);

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();

            if (restrictByOrganizer && !await BelongsToOrganizerAsync(conn, id, adminId))
            {
                return false;
            }

            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", id) { OracleDbType = OracleDbType.Varchar2 });
            var affected = await cmd.ExecuteNonQueryAsync();

            if (affected > 0)
            {
                await LogAuditAsync(adminId, "ACT_DELETE", new { activityId = id });
            }

            return affected > 0;
        }

        private async Task<bool> EnforceOrganizerScopeAsync(OracleConnection conn, string activityId, string adminId)
        {
            var ctx = _httpContextAccessor.HttpContext;
            var role = ctx?.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (!string.Equals(role, "ORGANIZER", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return await BelongsToOrganizerAsync(conn, activityId, adminId);
        }

        private async Task<bool> BelongsToOrganizerAsync(OracleConnection conn, string activityId, string adminId)
        {
            const string sql = @"SELECT 1 FROM ACTIVITIES WHERE ID = :id AND ORGANIZER_ID = :adminId";
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", activityId) { OracleDbType = OracleDbType.Varchar2 });
            cmd.Parameters.Add(new OracleParameter("adminId", adminId) { OracleDbType = OracleDbType.Varchar2 });

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            return await reader.ReadAsync();
        }

        private static void AddFilterParameters(OracleCommand cmd, string? termId, string? q, string? approval, string? status, string adminId, bool restrictByOrganizer)
        {
            if (!string.IsNullOrWhiteSpace(termId))
            {
                cmd.Parameters.Add(new OracleParameter("termId", termId) { OracleDbType = OracleDbType.Varchar2 });
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                cmd.Parameters.Add(new OracleParameter("keyword", q));
                cmd.Parameters.Add(new OracleParameter("keywordRegex", q));
            }

            if (!string.IsNullOrWhiteSpace(approval) && !string.Equals(approval, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                cmd.Parameters.Add(new OracleParameter("approval", approval));
            }

            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                cmd.Parameters.Add(new OracleParameter("status", status));
            }

            if (restrictByOrganizer)
            {
                cmd.Parameters.Add(new OracleParameter("organizerId", adminId) { OracleDbType = OracleDbType.Varchar2 });
            }
        }

        private static ActivityRowVm MapActivityRow(OracleDataReader reader)
        {
            return new ActivityRowVm
            {
                Id = reader.GetString(reader.GetOrdinal("ID")),
                Title = reader.GetString(reader.GetOrdinal("TITLE")),
                TermName = reader.IsDBNull(reader.GetOrdinal("TERM_NAME")) ? null : reader.GetString(reader.GetOrdinal("TERM_NAME")),
                StartAt = reader.IsDBNull(reader.GetOrdinal("START_AT")) ? null : reader.GetDateTime(reader.GetOrdinal("START_AT")),
                EndAt = reader.IsDBNull(reader.GetOrdinal("END_AT")) ? null : reader.GetDateTime(reader.GetOrdinal("END_AT")),
                Status = reader.IsDBNull(reader.GetOrdinal("STATUS")) ? string.Empty : reader.GetString(reader.GetOrdinal("STATUS")),
                ApprovalStatus = reader.IsDBNull(reader.GetOrdinal("APPROVAL_STATUS")) ? "PENDING" : reader.GetString(reader.GetOrdinal("APPROVAL_STATUS")),
                Seats = reader.IsDBNull(reader.GetOrdinal("MAX_SEATS")) ? null : reader.GetInt32(reader.GetOrdinal("MAX_SEATS")),
                RegisteredCount = reader.IsDBNull(reader.GetOrdinal("REGISTERED_COUNT")) ? 0 : reader.GetInt32(reader.GetOrdinal("REGISTERED_COUNT")),
                OrganizerName = reader.IsDBNull(reader.GetOrdinal("ORGANIZER_NAME")) ? null : reader.GetString(reader.GetOrdinal("ORGANIZER_NAME")),
                Points = reader.IsDBNull(reader.GetOrdinal("POINTS")) ? null : reader.GetDecimal(reader.GetOrdinal("POINTS"))
            };
        }

        private static ActivityDetailVm MapActivityDetail(OracleDataReader reader)
        {
            return new ActivityDetailVm
            {
                Id = reader.GetString(reader.GetOrdinal("ID")),
                Title = reader.GetString(reader.GetOrdinal("TITLE")),
                Description = reader.IsDBNull(reader.GetOrdinal("DESCRIPTION")) ? null : reader.GetString(reader.GetOrdinal("DESCRIPTION")),
                TermId = reader.IsDBNull(reader.GetOrdinal("TERM_ID")) ? null : reader.GetString(reader.GetOrdinal("TERM_ID")),
                TermName = reader.IsDBNull(reader.GetOrdinal("TERM_NAME")) ? null : reader.GetString(reader.GetOrdinal("TERM_NAME")),
                CriterionId = reader.IsDBNull(reader.GetOrdinal("CRITERION_ID")) ? null : reader.GetString(reader.GetOrdinal("CRITERION_ID")),
                CriterionName = reader.IsDBNull(reader.GetOrdinal("CRITERION_NAME")) ? null : reader.GetString(reader.GetOrdinal("CRITERION_NAME")),
                StartAt = reader.IsDBNull(reader.GetOrdinal("START_AT")) ? null : reader.GetDateTime(reader.GetOrdinal("START_AT")),
                EndAt = reader.IsDBNull(reader.GetOrdinal("END_AT")) ? null : reader.GetDateTime(reader.GetOrdinal("END_AT")),
                Status = reader.IsDBNull(reader.GetOrdinal("STATUS")) ? string.Empty : reader.GetString(reader.GetOrdinal("STATUS")),
                MaxSeats = reader.IsDBNull(reader.GetOrdinal("MAX_SEATS")) ? null : reader.GetInt32(reader.GetOrdinal("MAX_SEATS")),
                Location = reader.IsDBNull(reader.GetOrdinal("LOCATION")) ? null : reader.GetString(reader.GetOrdinal("LOCATION")),
                Points = reader.IsDBNull(reader.GetOrdinal("POINTS")) ? null : reader.GetDecimal(reader.GetOrdinal("POINTS")),
                ApprovalStatus = reader.IsDBNull(reader.GetOrdinal("APPROVAL_STATUS")) ? "PENDING" : reader.GetString(reader.GetOrdinal("APPROVAL_STATUS")),
                ApprovedBy = reader.IsDBNull(reader.GetOrdinal("APPROVED_BY")) ? null : reader.GetString(reader.GetOrdinal("APPROVED_BY")),
                ApprovedAt = reader.IsDBNull(reader.GetOrdinal("APPROVED_AT")) ? null : reader.GetDateTime(reader.GetOrdinal("APPROVED_AT")),
                OrganizerId = reader.IsDBNull(reader.GetOrdinal("ORGANIZER_ID")) ? null : reader.GetString(reader.GetOrdinal("ORGANIZER_ID")),
                RegisteredCount = reader.IsDBNull(reader.GetOrdinal("REGISTERED_COUNT")) ? 0 : reader.GetInt32(reader.GetOrdinal("REGISTERED_COUNT")),
                CheckinCount = reader.IsDBNull(reader.GetOrdinal("CHECKIN_COUNT")) ? 0 : reader.GetInt32(reader.GetOrdinal("CHECKIN_COUNT"))
            };
        }

        private async Task LogAuditAsync(string adminId, string action, object details)
        {
            try
            {
                const string sql = @"INSERT INTO AUDIT_TRAIL (WHO, ACTION, EVENT_AT_UTC, CLIENT_IP, USER_AGENT, DETAILS) VALUES (:who, :action, SYS_EXTRACT_UTC(SYSTIMESTAMP), :ip, :ua, :details)";
                var ctx = _httpContextAccessor.HttpContext;
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
                _logger.LogWarning(ex, "Không thể ghi audit trail cho action {Action}", action);
            }
        }

        private string GetCurrentAdminId()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                   ?? _httpContextAccessor.HttpContext?.User?.FindFirst("mand")?.Value
                   ?? string.Empty;
        }

        private string GetCurrentRole()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                   ?? string.Empty;
        }
    }
}
