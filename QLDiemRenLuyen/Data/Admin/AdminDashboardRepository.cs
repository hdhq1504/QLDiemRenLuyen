using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using QLDiemRenLuyen.ViewModels;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace QLDiemRenLuyen.Data
{
    /// <summary>
    /// Repository phục vụ dashboard dành cho quản trị viên.
    /// </summary>
    public class AdminDashboardRepository
    {
        private readonly Database _database;
        private readonly ILogger<AdminDashboardRepository> _logger;

        public AdminDashboardRepository(Database database, ILogger<AdminDashboardRepository> logger)
        {
            _database = database;
            _logger = logger;
        }

        public async Task<AdminKpiVm> GetKpisAsync()
        {
            const string sql = @"
                SELECT
                    (SELECT COUNT(*) FROM USERS) AS TOTAL_USERS,
                    (SELECT COUNT(*) FROM ACTIVITIES WHERE STATUS = 'OPEN') AS OPEN_ACTIVITIES,
                    (SELECT COUNT(*) FROM REGISTRATIONS WHERE TRUNC(REGISTERED_AT) = TRUNC(SYSDATE)) AS REGISTRATIONS_TODAY,
                    (SELECT COUNT(*) FROM FEEDBACKS WHERE STATUS = 'SUBMITTED') AS PENDING_FEEDBACKS,
                    (SELECT COUNT(*) FROM NOTIFICATIONS WHERE CREATED_AT >= (SYSTIMESTAMP - INTERVAL '7' DAY)) AS NOTIFICATIONS_7D
                  FROM DUAL";

            try
            {
                await using var conn = (OracleConnection)_database.CreateConnection();
                await conn.OpenAsync();
                await using var cmd = new OracleCommand(sql, conn) { BindByName = true };

                await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if (await reader.ReadAsync())
                {
                    return new AdminKpiVm
                    {
                        TotalUsers = Convert.ToInt32(reader.GetDecimal(0)),
                        OpenActivities = Convert.ToInt32(reader.GetDecimal(1)),
                        RegistrationsToday = Convert.ToInt32(reader.GetDecimal(2)),
                        PendingFeedbacks = Convert.ToInt32(reader.GetDecimal(3)),
                        Notifications7d = Convert.ToInt32(reader.GetDecimal(4))
                    };
                }

                return new AdminKpiVm();
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy KPI dashboard admin");
                throw;
            }
        }

        public async Task<IEnumerable<TimeSeriesPoint>> GetRegistrationsTrendAsync(int days)
        {
            const string sql = @"
                SELECT TRUNC(REGISTERED_AT) AS D, COUNT(*) AS CNT
                  FROM REGISTRATIONS
                 WHERE REGISTERED_AT >= (SYSTIMESTAMP - INTERVAL :days DAY)
                 GROUP BY TRUNC(REGISTERED_AT)
                 ORDER BY D";

            var results = new List<TimeSeriesPoint>();

            try
            {
                await using var conn = (OracleConnection)_database.CreateConnection();
                await conn.OpenAsync();
                await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add(new OracleParameter("days", OracleDbType.Int32)
                {
                    Direction = ParameterDirection.Input,
                    Value = days
                });

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new TimeSeriesPoint
                    {
                        Day = reader.GetDateTime(0),
                        Count = Convert.ToInt32(reader.GetDecimal(1))
                    });
                }

                return results;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy xu hướng đăng ký (days={Days})", days);
                throw;
            }
        }

        public async Task<IEnumerable<TopActivityPoint>> GetTopActivitiesAsync(string? termId, int top)
        {
            var results = new List<TopActivityPoint>();
            try
            {
                await using var conn = (OracleConnection)_database.CreateConnection();
                await conn.OpenAsync();

                var effectiveTermId = termId;
                if (string.IsNullOrWhiteSpace(termId))
                {
                    const string currentTermSql = @"SELECT ID FROM TERMS WHERE START_DATE <= TRUNC(SYSDATE) ORDER BY START_DATE DESC FETCH FIRST 1 ROWS ONLY";
                    await using (var termCmd = new OracleCommand(currentTermSql, conn) { BindByName = true })
                    {
                        var termResult = await termCmd.ExecuteScalarAsync();
                        effectiveTermId = termResult?.ToString();
                    }
                }

                if (string.IsNullOrWhiteSpace(effectiveTermId))
                {
                    return results;
                }

                const string sql = @"
                    SELECT * FROM (
                        SELECT a.ID, a.TITLE, COUNT(r.ID) AS CNT
                          FROM ACTIVITIES a
                          LEFT JOIN REGISTRATIONS r ON r.ACTIVITY_ID = a.ID
                         WHERE a.TERM_ID = :termId
                         GROUP BY a.ID, a.TITLE
                         ORDER BY CNT DESC, a.TITLE
                    )
                    WHERE ROWNUM <= :top";

                await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add(new OracleParameter("termId", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = effectiveTermId
                });
                cmd.Parameters.Add(new OracleParameter("top", OracleDbType.Int32)
                {
                    Direction = ParameterDirection.Input,
                    Value = top
                });

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new TopActivityPoint
                    {
                        ActivityId = reader.GetString(0),
                        Title = reader.GetString(1),
                        Count = Convert.ToInt32(reader.GetDecimal(2))
                    });
                }

                return results;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy top hoạt động theo kỳ {TermId}", termId);
                throw;
            }
        }

        public async Task<IEnumerable<RecentAuditVm>> GetRecentAuditAsync(int take)
        {
            const string sql = @"
                SELECT ID, WHO, ACTION, EVENT_AT_UTC, CLIENT_IP
                  FROM AUDIT_TRAIL
                 ORDER BY EVENT_AT_UTC DESC FETCH FIRST :take ROWS ONLY";

            var results = new List<RecentAuditVm>();

            try
            {
                await using var conn = (OracleConnection)_database.CreateConnection();
                await conn.OpenAsync();
                await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add(new OracleParameter("take", OracleDbType.Int32)
                {
                    Direction = ParameterDirection.Input,
                    Value = take
                });

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new RecentAuditVm
                    {
                        Id = reader.GetString(0),
                        Who = reader.GetString(1),
                        Action = reader.GetString(2),
                        EventAtUtc = reader.GetDateTime(3),
                        ClientIp = reader.IsDBNull(4) ? null : reader.GetString(4)
                    });
                }

                return results;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy audit gần nhất");
                throw;
            }
        }

        public async Task<IEnumerable<PendingFeedbackVm>> GetPendingFeedbacksAsync(int take)
        {
            const string sql = @"
                SELECT f.ID, f.TITLE, u.FULL_NAME, f.CREATED_AT, t.NAME
                  FROM FEEDBACKS f
                  JOIN USERS u ON u.MAND = f.STUDENT_ID
                  JOIN TERMS t ON t.ID = f.TERM_ID
                 WHERE f.STATUS = 'SUBMITTED'
                 ORDER BY f.CREATED_AT DESC FETCH FIRST :take ROWS ONLY";

            var results = new List<PendingFeedbackVm>();

            try
            {
                await using var conn = (OracleConnection)_database.CreateConnection();
                await conn.OpenAsync();
                await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add(new OracleParameter("take", OracleDbType.Int32)
                {
                    Direction = ParameterDirection.Input,
                    Value = take
                });

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new PendingFeedbackVm
                    {
                        Id = reader.GetString(0),
                        Title = reader.GetString(1),
                        StudentName = reader.GetString(2),
                        CreatedAt = reader.GetDateTime(3),
                        TermName = reader.GetString(4)
                    });
                }

                return results;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy phản hồi chờ xử lý");
                throw;
            }
        }

        public async Task<IEnumerable<LookupDto>> GetTermsAsync()
        {
            const string sql = @"SELECT ID, NAME FROM TERMS ORDER BY START_DATE DESC";
            var results = new List<LookupDto>();

            try
            {
                await using var conn = (OracleConnection)_database.CreateConnection();
                await conn.OpenAsync();
                await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new LookupDto
                    {
                        Id = reader.GetString(0),
                        Name = reader.GetString(1)
                    });
                }

                return results;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách kỳ học cho dashboard admin");
                throw;
            }
        }
    }
}
