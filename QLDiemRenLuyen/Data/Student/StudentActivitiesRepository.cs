using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using QLDiemRenLuyen.Models.ViewModels;
using QLDiemRenLuyen.Student.Models.ViewModels;

namespace QLDiemRenLuyen.Data.Student
{
    /// <summary>
    /// Repository phụ trách tương tác dữ liệu hoạt động dành cho sinh viên.
    /// </summary>
    public class StudentActivitiesRepository
    {
        private readonly Database _db;
        private readonly ILogger<StudentActivitiesRepository> _logger;

        public StudentActivitiesRepository(Database db, ILogger<StudentActivitiesRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<IEnumerable<TermDto>> GetTermsAsync()
        {
            const string sql = @"SELECT ID, NAME FROM TERMS ORDER BY ID DESC";
            var result = new List<TermDto>();

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn);
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

        public async Task<PagedList<ActivityItemVm>> SearchActivitiesAsync(
            string? termId, string? keyword, int page, int pageSize, string studentId)
        {
            const string sql = @"
                SELECT *
                FROM (
                    SELECT 
                      a.ID,
               a.TITLE,
                a.DESCRIPTION,
                        a.START_AT,
               a.END_AT,
     a.STATUS,
                a.MAX_SEATS,
                  c.NAME AS CRITERION_NAME,
                   (SELECT COUNT(*) FROM REGISTRATIONS r WHERE r.ACTIVITY_ID = a.ID) AS REG_CNT,
                    (SELECT MAX(STATUS) FROM REGISTRATIONS r2 WHERE r2.ACTIVITY_ID = a.ID AND r2.STUDENT_ID = :sid) AS STUDENT_STATE,
          ROW_NUMBER() OVER (ORDER BY a.START_AT DESC) AS rn
        FROM ACTIVITIES a
      LEFT JOIN CRITERIA c ON c.ID = a.CRITERION_ID
                WHERE
             (:termId IS NULL OR a.TERM_ID = :termId)
        AND (
      :kw IS NULL
        OR LOWER(a.TITLE) LIKE '%' || LOWER(:kw) || '%'
               OR REGEXP_LIKE(DBMS_LOB.SUBSTR(a.DESCRIPTION, 4000, 1), :kw, 'i')
             )
            ) t
            WHERE t.rn BETWEEN :startRow AND :endRow";

            const string countSql = @"
             SELECT COUNT(*)
                FROM ACTIVITIES a
                WHERE
        (:termId IS NULL OR a.TERM_ID = :termId)
                AND (
                  :kw IS NULL
                 OR LOWER(a.TITLE) LIKE '%' || LOWER(:kw) || '%'
                        OR REGEXP_LIKE(DBMS_LOB.SUBSTR(a.DESCRIPTION, 4000, 1), :kw, 'i')
             )";

            var items = new List<ActivityItemVm>();
            var startRow = (page - 1) * pageSize + 1;
            var endRow = page * pageSize;

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();

            // Query danh sách
            await using (var cmd = new OracleCommand(sql, conn))
            {
                cmd.BindByName = true;
                cmd.Parameters.Add(CreateNullableStringParameter("termId", termId));
                cmd.Parameters.Add(CreateNullableStringParameter("kw", keyword));
                cmd.Parameters.Add(new OracleParameter("sid", studentId) { OracleDbType = OracleDbType.Varchar2 });
                cmd.Parameters.Add(new OracleParameter("startRow", startRow));
                cmd.Parameters.Add(new OracleParameter("endRow", endRow));

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(MapActivity(reader));
                }
            }

            // Query tổng số bản ghi
            int total;
            await using (var countCmd = new OracleCommand(countSql, conn))
            {
                countCmd.BindByName = true;
                countCmd.Parameters.Add(CreateNullableStringParameter("termId", termId));
                countCmd.Parameters.Add(CreateNullableStringParameter("kw", keyword));

                total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            }

            return new PagedList<ActivityItemVm>
            {
                Data = items,
                TotalItems = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<ActivityItemVm?> GetActivityAsync(string id, string studentId)
        {
            const string sql = @"
                SELECT a.ID,
                       a.TITLE,
                       a.DESCRIPTION,
                       a.START_AT,
                       a.END_AT,
                       a.STATUS,
                       a.MAX_SEATS,
                       c.NAME AS CRITERION_NAME,
                       (SELECT COUNT(*)
                          FROM REGISTRATIONS r
                         WHERE r.ACTIVITY_ID = a.ID
                           AND r.STATUS IN ('REGISTERED', 'CHECKED_IN')) AS REGISTERED_COUNT,
                       (SELECT CASE
                                 WHEN MAX(CASE WHEN r.STATUS = 'CHECKED_IN' THEN 1 ELSE 0 END) = 1 THEN 'CHECKED_IN'
                                 WHEN MAX(CASE WHEN r.STATUS = 'REGISTERED' THEN 1 ELSE 0 END) = 1 THEN 'REGISTERED'
                                 ELSE 'NOT_REGISTERED'
                               END
                          FROM REGISTRATIONS r
                         WHERE r.ACTIVITY_ID = a.ID
                           AND r.STUDENT_ID = :sid) AS STUDENT_STATE
                  FROM ACTIVITIES a
                  LEFT JOIN CRITERIA c ON c.ID = a.CRITERION_ID
                 WHERE a.ID = :id
            ";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("sid", studentId) { OracleDbType = OracleDbType.Varchar2 });
            cmd.Parameters.Add(new OracleParameter("id", id) { OracleDbType = OracleDbType.Varchar2 });

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (await reader.ReadAsync())
            {
                return MapActivity(reader);
            }

            return null;
        }

        public async Task<bool> RegisterAsync(string activityId, string studentId)
        {
            const string sql = @"
                INSERT INTO REGISTRATIONS (ACTIVITY_ID, STUDENT_ID, STATUS, REGISTERED_AT)
                VALUES (:aid, :sid, 'REGISTERED', SYSTIMESTAMP)";

            try
            {
                var affected = await _db.ExecuteAsync(sql, new[]
                {
                    new OracleParameter("aid", activityId) { OracleDbType = OracleDbType.Varchar2 },
                    new OracleParameter("sid", studentId)  { OracleDbType = OracleDbType.Varchar2 }
                });

                return affected > 0;
            }
            catch (OracleException ex) when (ex.Number == 1) // unique/dup
            {
                _logger.LogWarning(ex, "Sinh viên {Student} đã đăng ký hoạt động {Activity}", studentId, activityId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đăng ký hoạt động {Activity} cho sinh viên {Student}", activityId, studentId);
                return false;
            }
        }

        public async Task<bool> UnregisterAsync(string activityId, string studentId)
        {
            const string sql = @"DELETE FROM REGISTRATIONS
                                 WHERE ACTIVITY_ID = :aid AND STUDENT_ID = :sid AND STATUS = 'REGISTERED'";

            var affected = await _db.ExecuteAsync(sql, new[]
            {
                new OracleParameter("aid", activityId) { OracleDbType = OracleDbType.Varchar2 },
                new OracleParameter("sid", studentId)  { OracleDbType = OracleDbType.Varchar2 }
            });

            return affected > 0;
        }

        public async Task<IReadOnlyList<ActivityReminderDto>> GetUpcomingRegistrationsAsync(string studentId, DateTime fromUtc, DateTime toUtc)
        {
            const string sql = @"SELECT a.ID, a.TITLE, a.START_AT, a.END_AT, a.STATUS
                                    FROM ACTIVITIES a
                                    JOIN REGISTRATIONS r ON r.ACTIVITY_ID = a.ID
                                   WHERE r.STUDENT_ID = :sid
                                     AND r.STATUS IN ('REGISTERED', 'CHECKED_IN')
                                     AND a.START_AT BETWEEN :fromDate AND :toDate
                                ORDER BY a.START_AT";

            var result = new List<ActivityReminderDto>();

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("sid", OracleDbType.Varchar2, studentId, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("fromDate", OracleDbType.Date, fromUtc, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("toDate", OracleDbType.Date, toUtc, ParameterDirection.Input));

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new ActivityReminderDto
                {
                    Id = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    StartAt = reader.IsDBNull(2) ? DateTime.MinValue : reader.GetDateTime(2),
                    EndAt = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3),
                    Status = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
                });
            }

            return result;
        }

        private static OracleParameter CreateNullableStringParameter(string name, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new OracleParameter(name, DBNull.Value) { OracleDbType = OracleDbType.Varchar2 };
            }
            return new OracleParameter(name, value) { OracleDbType = OracleDbType.Varchar2 };
        }

        private static ActivityItemVm MapActivity(OracleDataReader reader)
        {
            var item = new ActivityItemVm
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                StartAt = reader.GetDateTime(3),
                EndAt = reader.GetDateTime(4),
                Status = reader.GetString(5),
                MaxSeats = reader.IsDBNull(6) ? null : Convert.ToInt32(reader.GetDecimal(6)),
                CriterionName = reader.IsDBNull(7) ? null : reader.GetString(7),
                RegisteredCount = reader.IsDBNull(8) ? 0 : Convert.ToInt32(reader.GetDecimal(8)),
                StudentState = reader.IsDBNull(9) ? "NOT_REGISTERED" : reader.GetString(9),
                Organizer = null // TODO: bổ sung khi schema có
            };

            return item;
        }
    }
}