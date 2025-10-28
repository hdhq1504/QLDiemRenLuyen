using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using QLDiemRenLuyen.Models.ViewModels.StudentScores;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QLDiemRenLuyen.Data
{
    /// <summary>
    /// Repository lấy dữ liệu điểm rèn luyện của sinh viên.
    /// </summary>
    public class StudentScoresRepository
    {
        private const decimal BaseScore = 70m;

        private readonly Database _db;
        private readonly ILogger<StudentScoresRepository> _logger;

        private static bool? _hasScoresTable;
        private static bool? _hasActivityPointsColumn;
        private static readonly SemaphoreSlim InitLock = new(1, 1);

        public StudentScoresRepository(Database db, ILogger<StudentScoresRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Đọc danh sách học kỳ để phục vụ dropdown.
        /// </summary>
        public async Task<IEnumerable<TermDto>> GetTermsAsync()
        {
            const string sql = @"SELECT ID, NAME, START_DATE FROM TERMS ORDER BY START_DATE DESC";
            var result = new List<TermDto>();

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            while (await reader.ReadAsync())
            {
                result.Add(new TermDto
                {
                    Id = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1)
                });
            }

            return result;
        }

        /// <summary>
        /// Lấy điểm rèn luyện của sinh viên theo học kỳ.
        /// </summary>
        public async Task<StudentScoreVm> GetMyScoreAsync(string studentId, string? termId)
        {
            if (string.IsNullOrWhiteSpace(studentId))
            {
                throw new ArgumentNullException(nameof(studentId));
            }

            await EnsureMetadataAsync();

            var terms = (await GetTermsAsync()).ToList();
            TermDto? selectedTerm = null;

            if (!string.IsNullOrWhiteSpace(termId))
            {
                selectedTerm = terms.FirstOrDefault(t => t.Id == termId) ?? await GetTermByIdAsync(termId!);
            }
            else
            {
                selectedTerm = await GetCurrentTermAsync() ?? terms.FirstOrDefault();
            }

            var model = new StudentScoreVm
            {
                StudentId = studentId,
                BaseScore = BaseScore,
                Adjustments = 0m,
                Terms = terms,
                SelectedTermId = selectedTerm?.Id,
                SelectedTermName = selectedTerm?.Name,
                FullName = await GetStudentFullNameAsync(studentId) ?? string.Empty
            };

            if (selectedTerm == null)
            {
                model.Total = BaseScore;
                model.Classification = Classify(model.Total);
                return model;
            }

            var breakdown = await GetBreakdownAsync(studentId, selectedTerm.Id);
            model.Breakdown = breakdown;
            model.ActivityScore = HasActivityPoints ? breakdown.Sum(x => x.Earned) : 0m;
            model.RecentActivities = await GetRecentActivitiesAsync(studentId, selectedTerm.Id);

            if (UseScoresTable)
            {
                var scoreRecord = await GetScoreRecordAsync(studentId, selectedTerm.Id);
                if (scoreRecord != null && scoreRecord.Total.HasValue)
                {
                    model.Total = scoreRecord.Total.Value;
                }
                else
                {
                    model.Total = model.BaseScore + model.ActivityScore + model.Adjustments;
                }

                model.Status = TranslateStatus(scoreRecord?.Status);
            }
            else
            {
                model.Total = model.BaseScore + model.ActivityScore + model.Adjustments;
            }

            model.Classification = Classify(model.Total);
            model.HistoryPreview = (await GetHistoryAsync(studentId)).Take(5).ToList();

            return model;
        }

        /// <summary>
        /// Lấy lịch sử điểm rèn luyện các học kỳ.
        /// </summary>
        public async Task<IEnumerable<TermScoreVm>> GetHistoryAsync(string studentId)
        {
            if (string.IsNullOrWhiteSpace(studentId))
            {
                return Array.Empty<TermScoreVm>();
            }

            await EnsureMetadataAsync();

            if (UseScoresTable)
            {
                return await GetHistoryFromScoresAsync(studentId);
            }

            return HasActivityPoints
                ? await GetHistoryFromActivitiesAsync(studentId)
                : await GetHistoryFallbackAsync();
        }

        #region Metadata helpers

        private bool UseScoresTable => _hasScoresTable == true;
        private bool HasActivityPoints => _hasActivityPointsColumn == true;

        /// <summary>
        /// Kiểm tra sự tồn tại của bảng SCORES và cột POINTS trong ACTIVITIES (thực hiện một lần).
        /// </summary>
        private async Task EnsureMetadataAsync()
        {
            if (_hasScoresTable.HasValue && _hasActivityPointsColumn.HasValue)
            {
                return;
            }

            await InitLock.WaitAsync();
            try
            {
                if (_hasScoresTable.HasValue && _hasActivityPointsColumn.HasValue)
                {
                    return;
                }

                await using var conn = (OracleConnection)_db.CreateConnection();
                await conn.OpenAsync();

                // Kiểm tra bảng SCORES
                await using (var cmd = new OracleCommand("SELECT COUNT(*) FROM USER_TABLES WHERE TABLE_NAME = 'SCORES'", conn))
                {
                    var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                    _hasScoresTable = exists;
                }

                // Kiểm tra cột POINTS trong ACTIVITIES
                const string columnSql = "SELECT COUNT(*) FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'ACTIVITIES' AND COLUMN_NAME = 'POINTS'";
                await using (var cmd = new OracleCommand(columnSql, conn))
                {
                    var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                    _hasActivityPointsColumn = exists;
                }

                _logger.LogInformation("StudentScoresRepository metadata - SCORES table: {HasScores}, ACTIVITIES.POINTS: {HasPoints}",
                    _hasScoresTable, _hasActivityPointsColumn);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể kiểm tra metadata điểm rèn luyện");
                _hasScoresTable ??= false;
                _hasActivityPointsColumn ??= false;
            }
            finally
            {
                InitLock.Release();
            }
        }

        #endregion

        #region Term helpers

        private async Task<TermDto?> GetCurrentTermAsync()
        {
            const string sql = @"SELECT ID, NAME FROM TERMS WHERE START_DATE <= TRUNC(SYSDATE)
                                 ORDER BY START_DATE DESC FETCH FIRST 1 ROWS ONLY";
            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (await reader.ReadAsync())
            {
                return new TermDto
                {
                    Id = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1)
                };
            }

            return null;
        }

        private async Task<TermDto?> GetTermByIdAsync(string termId)
        {
            const string sql = @"SELECT ID, NAME FROM TERMS WHERE ID = :id";
            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", termId));
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (await reader.ReadAsync())
            {
                return new TermDto
                {
                    Id = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1)
                };
            }

            return null;
        }

        #endregion

        #region Student helpers

        private async Task<string?> GetStudentFullNameAsync(string studentId)
        {
            const string sql = "SELECT FULL_NAME FROM USERS WHERE MAND = :id";
            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", studentId));
            var result = await cmd.ExecuteScalarAsync();
            return result == null || result is DBNull ? null : Convert.ToString(result);
        }

        #endregion

        #region Breakdown & totals

        private async Task<List<CriterionScoreVm>> GetBreakdownAsync(string studentId, string termId)
        {
            var sql = @"SELECT c.ID, c.GROUP_NO, c.NAME,
                               NVL(SUM(
                                   CASE
                                       WHEN r.STATUS IN ('REGISTERED','CHECKED_IN') THEN {POINT_EXPR}
                                       ELSE 0
                                   END
                               ), 0) AS EARNED,
                               c.MAX_POINT
                          FROM CRITERIA c
                          LEFT JOIN ACTIVITIES a ON a.CRITERION_ID = c.ID AND a.TERM_ID = :termId
                          LEFT JOIN REGISTRATIONS r ON r.ACTIVITY_ID = a.ID AND r.STUDENT_ID = :sid
                          GROUP BY c.ID, c.GROUP_NO, c.NAME, c.MAX_POINT
                          ORDER BY c.GROUP_NO, c.ID";

            var pointExpr = HasActivityPoints ? "NVL(a.POINTS, 0)" : "0 /* TODO: thêm cột POINTS cho ACTIVITIES để chấm điểm */";
            sql = sql.Replace("{POINT_EXPR}", pointExpr);

            var result = new List<CriterionScoreVm>();

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("termId", termId));
            cmd.Parameters.Add(new OracleParameter("sid", studentId));

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new CriterionScoreVm
                {
                    CriterionId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    GroupNo = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetDecimal(1)),
                    Name = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Earned = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                    MaxPoint = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4)
                });
            }

            return result;
        }

        private async Task<ScoreRecord?> GetScoreRecordAsync(string studentId, string termId)
        {
            const string sql = @"SELECT TOTAL, STATUS, APPROVED_BY, APPROVED_AT
                                 FROM SCORES
                                 WHERE STUDENT_ID = :sid AND TERM_ID = :termId";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("sid", studentId));
            cmd.Parameters.Add(new OracleParameter("termId", termId));

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (await reader.ReadAsync())
            {
                return new ScoreRecord
                {
                    Total = reader.IsDBNull(0) ? (decimal?)null : reader.GetDecimal(0),
                    Status = reader.IsDBNull(1) ? null : reader.GetString(1),
                    ApprovedBy = reader.IsDBNull(2) ? null : reader.GetString(2),
                    ApprovedAt = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3)
                };
            }

            return null;
        }

        private async Task<IEnumerable<TermScoreVm>> GetHistoryFromScoresAsync(string studentId)
        {
            const string sql = @"SELECT t.ID, t.NAME, t.START_DATE,
                                         (SELECT s.TOTAL FROM SCORES s WHERE s.STUDENT_ID = :sid AND s.TERM_ID = t.ID) AS TOTAL
                                    FROM TERMS t
                                    ORDER BY t.START_DATE DESC";

            var result = new List<TermScoreVm>();

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("sid", studentId));
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var total = reader.IsDBNull(3) ? (decimal?)null : reader.GetDecimal(3);
                if (!total.HasValue)
                {
                    total = BaseScore; // fallback khi chưa có bản ghi SCORES
                }

                result.Add(new TermScoreVm
                {
                    TermId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    TermName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Total = total.Value,
                    Classification = Classify(total.Value)
                });
            }

            return result;
        }

        private async Task<IEnumerable<TermScoreVm>> GetHistoryFromActivitiesAsync(string studentId)
        {
            const string sql = @"SELECT t.ID, t.NAME, t.START_DATE,
                                         70 + NVL(SUM(CASE WHEN r.STATUS IN ('REGISTERED','CHECKED_IN') THEN NVL(a.POINTS, 0) ELSE 0 END), 0) AS TOTAL
                                    FROM TERMS t
                                    LEFT JOIN ACTIVITIES a ON a.TERM_ID = t.ID
                                    LEFT JOIN REGISTRATIONS r ON r.ACTIVITY_ID = a.ID AND r.STUDENT_ID = :sid AND r.STATUS IN ('REGISTERED','CHECKED_IN')
                                    GROUP BY t.ID, t.NAME, t.START_DATE
                                    ORDER BY t.START_DATE DESC";

            var result = new List<TermScoreVm>();

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("sid", studentId));
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var total = reader.IsDBNull(3) ? BaseScore : reader.GetDecimal(3);
                result.Add(new TermScoreVm
                {
                    TermId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    TermName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Total = total,
                    Classification = Classify(total)
                });
            }

            return result;
        }

        private async Task<IEnumerable<TermScoreVm>> GetHistoryFallbackAsync()
        {
            // Trường hợp chưa có cột POINTS -> tạm thời trả về điểm nền tảng.
            const string sql = @"SELECT ID, NAME, START_DATE FROM TERMS ORDER BY START_DATE DESC";
            var result = new List<TermScoreVm>();

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new TermScoreVm
                {
                    TermId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    TermName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Total = BaseScore,
                    Classification = Classify(BaseScore)
                });
            }

            return result;
        }

        private async Task<IEnumerable<ActivityContributionVm>> GetRecentActivitiesAsync(string studentId, string termId)
        {
            if (!HasActivityPoints)
            {
                // TODO: thêm cột POINTS hoặc bảng mapping để hiển thị hoạt động đóng góp điểm.
                return Array.Empty<ActivityContributionVm>();
            }

            const string sql = @"SELECT * FROM (
                                       SELECT a.TITLE,
                                              a.START_AT,
                                              a.END_AT,
                                              NVL(a.POINTS, 0) AS POINTS,
                                              NVL(r.CHECKED_IN_AT, r.REGISTERED_AT) AS LAST_ACTION
                                         FROM REGISTRATIONS r
                                         JOIN ACTIVITIES a ON a.ID = r.ACTIVITY_ID
                                        WHERE r.STUDENT_ID = :sid
                                          AND a.TERM_ID = :termId
                                          AND r.STATUS IN ('REGISTERED','CHECKED_IN')
                                        ORDER BY NVL(r.CHECKED_IN_AT, r.REGISTERED_AT) DESC
                                   ) WHERE ROWNUM <= 5";

            var result = new List<ActivityContributionVm>();

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("sid", studentId));
            cmd.Parameters.Add(new OracleParameter("termId", termId));
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new ActivityContributionVm
                {
                    Title = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    StartAt = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1),
                    EndAt = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2),
                    Points = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3)
                });
            }

            return result;
        }

        #endregion

        #region Classification helpers

        private static string Classify(decimal total)
        {
            if (total >= 90) return "Xuất sắc";
            if (total >= 80) return "Tốt";
            if (total >= 65) return "Khá";
            if (total >= 50) return "Trung bình";
            if (total >= 35) return "Yếu";
            return "Kém";
        }

        private static string? TranslateStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return null;
            }

            return status.ToUpperInvariant() switch
            {
                "PROVISIONAL" => "Tạm tính",
                "REVIEWED" => "Đã duyệt cố vấn",
                "APPROVED" => "Đã phê duyệt",
                "FINAL" => "Chính thức",
                _ => status
            };
        }

        private sealed class ScoreRecord
        {
            public decimal? Total { get; set; }
            public string? Status { get; set; }
            public string? ApprovedBy { get; set; }
            public DateTime? ApprovedAt { get; set; }
        }

        #endregion
    }
}
