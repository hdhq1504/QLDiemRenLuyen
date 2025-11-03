using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using QLDiemRenLuyen.Models.ViewModels;
using QLDiemRenLuyen.Models.ViewModels.Student;

namespace QLDiemRenLuyen.Data.Student
{
    /// <summary>
    /// Repository truy xuất dữ liệu thông báo cho sinh viên.
    /// </summary>
    public class StudentNotificationsRepository
    {
        private readonly Database _db;
        private readonly ILogger<StudentNotificationsRepository> _logger;

        public StudentNotificationsRepository(Database db, ILogger<StudentNotificationsRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Tìm kiếm danh sách thông báo có phân trang theo sinh viên.
        /// </summary>
        public async Task<PagedList<NotificationItemVm>> SearchAsync(string studentId, string? keyword, string status, int page, int pageSize)
        {
            const string listSql = @"
                WITH VISIBLE AS (
                    SELECT n.ID,
                           n.TITLE,
                           n.CREATED_AT,
                           CASE WHEN r.IS_READ = 1 THEN 1 ELSE 0 END AS IS_READ
                      FROM NOTIFICATIONS n
                      LEFT JOIN NOTIFICATION_READS r
                        ON r.NOTIFICATION_ID = n.ID AND r.STUDENT_ID = :sid
                     WHERE (n.TARGET_ROLE IN ('ALL','STUDENT') OR n.TO_USER_ID = :sid)
                       AND (:kw IS NULL OR LOWER(n.TITLE) LIKE '%' || LOWER(:kw) || '%')
                )
                SELECT *
                  FROM (
                        SELECT ID,
                               TITLE,
                               CREATED_AT,
                               IS_READ,
                               ROW_NUMBER() OVER (ORDER BY CREATED_AT DESC) RN
                          FROM VISIBLE
                         WHERE (:st = 'all')
                            OR (:st = 'unread' AND IS_READ = 0)
                            OR (:st = 'read'   AND IS_READ = 1)
                       )
                 WHERE RN BETWEEN :startRow AND :endRow";

            const string countSql = @"
                WITH VISIBLE AS (
                    SELECT n.ID,
                           CASE WHEN r.IS_READ = 1 THEN 1 ELSE 0 END AS IS_READ
                      FROM NOTIFICATIONS n
                      LEFT JOIN NOTIFICATION_READS r
                        ON r.NOTIFICATION_ID = n.ID AND r.STUDENT_ID = :sid
                     WHERE (n.TARGET_ROLE IN ('ALL','STUDENT') OR n.TO_USER_ID = :sid)
                       AND (:kw IS NULL OR LOWER(n.TITLE) LIKE '%' || LOWER(:kw) || '%')
                )
                SELECT COUNT(*)
                  FROM VISIBLE
                 WHERE (:st = 'all')
                    OR (:st = 'unread' AND IS_READ = 0)
                    OR (:st = 'read'   AND IS_READ = 1)";

            var items = new List<NotificationItemVm>();
            var startRow = (page - 1) * pageSize + 1;
            var endRow = page * pageSize;

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();

            await using (var cmd = new OracleCommand(listSql, conn) { BindByName = true })
            {
                cmd.Parameters.Add(new OracleParameter("sid", OracleDbType.Varchar2, studentId, ParameterDirection.Input));
                cmd.Parameters.Add(CreateNullableStringParameter("kw", keyword));
                cmd.Parameters.Add(new OracleParameter("st", OracleDbType.Varchar2, status, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("startRow", OracleDbType.Int32, startRow, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter("endRow", OracleDbType.Int32, endRow, ParameterDirection.Input));

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new NotificationItemVm
                    {
                        Id = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                        Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        CreatedAt = reader.IsDBNull(2) ? DateTime.MinValue : reader.GetDateTime(2),
                        IsRead = !reader.IsDBNull(3) && reader.GetInt32(3) == 1
                    });
                }
            }

            int total;
            await using (var countCmd = new OracleCommand(countSql, conn) { BindByName = true })
            {
                countCmd.Parameters.Add(new OracleParameter("sid", OracleDbType.Varchar2, studentId, ParameterDirection.Input));
                countCmd.Parameters.Add(CreateNullableStringParameter("kw", keyword));
                countCmd.Parameters.Add(new OracleParameter("st", OracleDbType.Varchar2, status, ParameterDirection.Input));

                var scalar = await countCmd.ExecuteScalarAsync();
                total = Convert.ToInt32(scalar);
            }

            return new PagedList<NotificationItemVm>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)total / pageSize) : 0
            };
        }

        /// <summary>
        /// Lấy chi tiết một thông báo nếu thuộc phạm vi xem của sinh viên.
        /// </summary>
        public async Task<NotificationDetailVm?> GetDetailAsync(string id, string studentId)
        {
            const string sql = @"
                SELECT n.ID,
                       n.TITLE,
                       n.CONTENT,
                       n.CREATED_AT,
                       CASE WHEN r.IS_READ = 1 THEN 1 ELSE 0 END AS IS_READ
                  FROM NOTIFICATIONS n
                  LEFT JOIN NOTIFICATION_READS r
                    ON r.NOTIFICATION_ID = n.ID AND r.STUDENT_ID = :sid
                 WHERE n.ID = :id
                   AND (n.TARGET_ROLE IN ('ALL','STUDENT') OR n.TO_USER_ID = :sid)";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("sid", OracleDbType.Varchar2, studentId, ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Varchar2, id, ParameterDirection.Input));

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (await reader.ReadAsync())
            {
                var rawContent = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                return new NotificationDetailVm
                {
                    Id = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    ContentHtml = SanitizeContent(rawContent),
                    CreatedAt = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3),
                    IsRead = !reader.IsDBNull(4) && reader.GetInt32(4) == 1
                };
            }

            return null;
        }

        /// <summary>
        /// Cập nhật trạng thái đọc của một thông báo (true = đã đọc).
        /// </summary>
        public async Task<bool> MarkReadAsync(string id, string studentId, bool isRead)
        {
            const string sql = @"
                MERGE INTO NOTIFICATION_READS d
                USING (SELECT :sid AS STUDENT_ID, :id AS NOTIFICATION_ID FROM DUAL) x
                   ON (d.STUDENT_ID = x.STUDENT_ID AND d.NOTIFICATION_ID = x.NOTIFICATION_ID)
                 WHEN MATCHED THEN
                   UPDATE SET IS_READ = :isRead,
                              READ_AT = CASE WHEN :isRead = 1 THEN SYSTIMESTAMP ELSE NULL END
                 WHEN NOT MATCHED THEN
                   INSERT (STUDENT_ID, NOTIFICATION_ID, IS_READ, READ_AT)
                   VALUES (:sid, :id, :isRead, CASE WHEN :isRead = 1 THEN SYSTIMESTAMP ELSE NULL END)";

            var parameters = new[]
            {
                new OracleParameter("sid", OracleDbType.Varchar2, studentId, ParameterDirection.Input),
                new OracleParameter("id", OracleDbType.Varchar2, id, ParameterDirection.Input),
                new OracleParameter("isRead", OracleDbType.Int16, isRead ? 1 : 0, ParameterDirection.Input)
            };

            try
            {
                var affected = await _db.ExecuteAsync(sql, parameters);
                return affected > 0;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Không thể cập nhật trạng thái đọc thông báo {NotificationId} cho sinh viên {StudentId}", id, studentId);
                return false;
            }
        }

        /// <summary>
        /// Đánh dấu toàn bộ thông báo nhìn thấy được là đã đọc.
        /// </summary>
        public async Task<int> MarkAllReadAsync(string studentId)
        {
            const string insertSql = @"
                INSERT /*+ IGNORE_ROW_ON_DUPKEY_INDEX (NOTIFICATION_READS (NOTIFICATION_ID, STUDENT_ID)) */
                  INTO NOTIFICATION_READS (NOTIFICATION_ID, STUDENT_ID, IS_READ, READ_AT)
                  SELECT n.ID, :sid, 1, SYSTIMESTAMP
                    FROM NOTIFICATIONS n
                   WHERE (n.TARGET_ROLE IN ('ALL','STUDENT') OR n.TO_USER_ID = :sid)";

            const string updateSql = @"
                UPDATE NOTIFICATION_READS
                   SET IS_READ = 1,
                       READ_AT = SYSTIMESTAMP
                 WHERE STUDENT_ID = :sid
                   AND NOTIFICATION_ID IN (
                       SELECT n.ID
                         FROM NOTIFICATIONS n
                        WHERE (n.TARGET_ROLE IN ('ALL','STUDENT') OR n.TO_USER_ID = :sid)
                   )";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var tx = await conn.BeginTransactionAsync();

            try
            {
                // INSERT missing rows
                await using (var insertCmd = new OracleCommand(insertSql, conn) { BindByName = true })
                {
                    insertCmd.Transaction = (OracleTransaction)tx;
                    insertCmd.Parameters.Add(new OracleParameter("sid", OracleDbType.Varchar2, studentId, ParameterDirection.Input));
                    await insertCmd.ExecuteNonQueryAsync();
                }

                // UPDATE all to read
                int updated;
                await using (var updateCmd = new OracleCommand(updateSql, conn) { BindByName = true })
                {
                    updateCmd.Transaction = (OracleTransaction)tx;
                    updateCmd.Parameters.Add(new OracleParameter("sid", OracleDbType.Varchar2, studentId, ParameterDirection.Input));
                    updated = await updateCmd.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();
                return updated;
            }
            catch (OracleException ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Không thể đánh dấu tất cả thông báo đã đọc cho sinh viên {StudentId}", studentId);
                throw;
            }
        }

        private static OracleParameter CreateNullableStringParameter(string name, string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? new OracleParameter(name, OracleDbType.Varchar2, DBNull.Value, ParameterDirection.Input)
                : new OracleParameter(name, OracleDbType.Varchar2, value, ParameterDirection.Input);
        }

        private static string SanitizeContent(string? content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return string.Empty;
            }

            var encoded = HtmlEncoder.Default.Encode(content);
            return encoded.Replace("\r\n", "<br />").Replace("\n", "<br />").Replace("\r", "<br />");
        }
    }
}
