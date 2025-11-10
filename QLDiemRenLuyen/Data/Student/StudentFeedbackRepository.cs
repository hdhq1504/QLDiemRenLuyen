using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using QLDiemRenLuyen.ViewModels.Common;
using QLDiemRenLuyen.ViewModels.Student;

namespace QLDiemRenLuyen.Data.Student
{
    /// <summary>
    /// Repository phụ trách truy xuất và thao tác dữ liệu phản hồi điểm rèn luyện của sinh viên.
    /// </summary>
    public class StudentFeedbackRepository
    {
        private readonly Database _db;
        private readonly ILogger<StudentFeedbackRepository> _logger;

        public StudentFeedbackRepository(Database db, ILogger<StudentFeedbackRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Lấy danh sách học kỳ phục vụ dropdown lọc.
        /// </summary>
        public async Task<IEnumerable<TermDto>> GetTermsAsync()
        {
            const string sql = @"SELECT ID, NAME FROM TERMS ORDER BY START_DATE DESC";
            var result = new List<TermDto>();

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn)
            {
                BindByName = true
            };
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
        /// Lấy danh sách tiêu chí cho dropdown.
        /// </summary>
        public async Task<IEnumerable<CriterionDto>> GetCriteriaAsync()
        {
            const string sql = @"SELECT ID, NAME FROM CRITERIA ORDER BY DISPLAY_ORDER NULLS LAST, NAME";
            var result = new List<CriterionDto>();

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn)
            {
                BindByName = true
            };
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            while (await reader.ReadAsync())
            {
                result.Add(new CriterionDto
                {
                    Id = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1)
                });
            }

            return result;
        }

        /// <summary>
        /// Lấy danh sách phản hồi theo phân trang.
        /// </summary>
        public async Task<PagedList<FeedbackItemVm>> GetFeedbacksAsync(string studentId, string? termId, int page, int pageSize, string? keyword)
        {
            const string sql = @"
                SELECT *
                  FROM (
                        SELECT f.ID,
                               f.TITLE,
                               f.STATUS,
                               f.CREATED_AT,
                               t.NAME AS TERM_NAME,
                               ROW_NUMBER() OVER (ORDER BY f.CREATED_AT DESC) AS RN
                          FROM FEEDBACKS f
                          JOIN TERMS t ON t.ID = f.TERM_ID
                         WHERE f.STUDENT_ID = :sid
                           AND (:termId IS NULL OR f.TERM_ID = :termId)
                           AND (:kw IS NULL OR LOWER(f.TITLE) LIKE '%' || LOWER(:kw) || '%')
                       )
                 WHERE RN BETWEEN :startRow AND :endRow";

            const string countSql = @"
                SELECT COUNT(*)
                  FROM FEEDBACKS f
                 WHERE f.STUDENT_ID = :sid
                   AND (:termId IS NULL OR f.TERM_ID = :termId)
                   AND (:kw IS NULL OR LOWER(f.TITLE) LIKE '%' || LOWER(:kw) || '%')";

            var items = new List<FeedbackItemVm>();
            var startRow = (page - 1) * pageSize + 1;
            var endRow = startRow + pageSize - 1;

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();

            await using (var cmd = new OracleCommand(sql, conn)
            {
                BindByName = true
            })
            {
                cmd.Parameters.Add(new OracleParameter("sid", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = studentId
                });
                cmd.Parameters.Add(CreateNullableStringParameter("termId", termId));
                cmd.Parameters.Add(CreateNullableStringParameter("kw", keyword));
                cmd.Parameters.Add(new OracleParameter("startRow", OracleDbType.Int32)
                {
                    Direction = ParameterDirection.Input,
                    Value = startRow
                });
                cmd.Parameters.Add(new OracleParameter("endRow", OracleDbType.Int32)
                {
                    Direction = ParameterDirection.Input,
                    Value = endRow
                });

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new FeedbackItemVm
                    {
                        Id = reader.GetString(0),
                        Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        Status = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        CreatedAt = reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3),
                        TermName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
                    });
                }
            }

            int total;
            await using (var countCmd = new OracleCommand(countSql, conn)
            {
                BindByName = true
            })
            {
                countCmd.Parameters.Add(new OracleParameter("sid", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = studentId
                });
                countCmd.Parameters.Add(CreateNullableStringParameter("termId", termId));
                countCmd.Parameters.Add(CreateNullableStringParameter("kw", keyword));

                total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            }

            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            return new PagedList<FeedbackItemVm>
            {
                Data = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                TotalPages = totalPages
            };
        }

        /// <summary>
        /// Lấy chi tiết phản hồi của sinh viên.
        /// </summary>
        public async Task<FeedbackDetailVm?> GetFeedbackAsync(string id, string studentId)
        {
            const string sql = @"
                SELECT f.ID,
                       f.TERM_ID,
                       f.CRITERION_ID,
                       f.TITLE,
                       f.CONTENT,
                       f.STATUS,
                       f.RESPONSE,
                       f.CREATED_AT,
                       f.UPDATED_AT,
                       f.RESPONDED_AT,
                       t.NAME AS TERM_NAME,
                       c.NAME AS CRITERION_NAME
                  FROM FEEDBACKS f
                  JOIN TERMS t ON t.ID = f.TERM_ID
                  LEFT JOIN CRITERIA c ON c.ID = f.CRITERION_ID
                 WHERE f.ID = :id AND f.STUDENT_ID = :sid";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Varchar2)
            {
                Direction = ParameterDirection.Input,
                Value = id
            });
            cmd.Parameters.Add(new OracleParameter("sid", OracleDbType.Varchar2)
            {
                Direction = ParameterDirection.Input,
                Value = studentId
            });

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (await reader.ReadAsync())
            {
                return new FeedbackDetailVm
                {
                    Id = reader.GetString(0),
                    TermId = reader.IsDBNull(1)
                        ? string.Empty
                        : reader.GetString(1),
                    CriterionId = reader.IsDBNull(2)
                        ? null
                        : reader.GetString(2),
                    Title = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Content = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Status = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    Response = reader.IsDBNull(6) ? null : reader.GetString(6),
                    CreatedAt = reader.IsDBNull(7) ? DateTime.MinValue : reader.GetDateTime(7),
                    UpdatedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                    RespondedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    TermName = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    CriterionName = reader.IsDBNull(11) ? null : reader.GetString(11)
                };
            }

            return null;
        }

        /// <summary>
        /// Tạo mới phản hồi.
        /// </summary>
        public async Task<string> CreateAsync(FeedbackEditVm vm, string studentId, string status = "SUBMITTED")
        {
            const string sql = @"
                INSERT INTO FEEDBACKS (ID, STUDENT_ID, TERM_ID, CRITERION_ID, TITLE, CONTENT, STATUS, CREATED_AT)
                VALUES (:id, :sid, :termId, :critId, :title, :content, :status, SYSTIMESTAMP)";

            var id = Guid.NewGuid().ToString("N");
            var parameters = new[]
            {
                new OracleParameter("id", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = id
                },
                new OracleParameter("sid", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = studentId
                },
                new OracleParameter("termId", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.TermId
                },
                CreateNullableStringParameter("critId", vm.CriterionId),
                new OracleParameter("title", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.Title
                },
                new OracleParameter("content", OracleDbType.Clob)
                {
                    Direction = ParameterDirection.Input,
                    Value = (object?)vm.Content ?? DBNull.Value
                },
                new OracleParameter("status", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = status
                }
            };

            try
            {
                await _db.ExecuteAsync(sql, parameters);
                return id;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Không thể tạo phản hồi mới cho sinh viên {Student}", studentId);
                throw;
            }
        }

        /// <summary>
        /// Cập nhật phản hồi ở trạng thái nháp.
        /// </summary>
        public async Task<bool> UpdateAsync(string id, FeedbackEditVm vm, string studentId, string status = "SUBMITTED")
        {
            const string sql = @"
                UPDATE FEEDBACKS
                   SET TERM_ID = :termId,
                       CRITERION_ID = :critId,
                       TITLE = :title,
                       CONTENT = :content,
                       STATUS = :status,
                       UPDATED_AT = SYSTIMESTAMP
                 WHERE ID = :id AND STUDENT_ID = :sid AND STATUS = 'DRAFT'";

            var parameters = new[]
            {
                new OracleParameter("termId", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.TermId
                },
                CreateNullableStringParameter("critId", vm.CriterionId),
                new OracleParameter("title", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.Title
                },
                new OracleParameter("content", OracleDbType.Clob)
                {
                    Direction = ParameterDirection.Input,
                    Value = (object?)vm.Content ?? DBNull.Value
                },
                new OracleParameter("status", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = status
                },
                new OracleParameter("id", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = id
                },
                new OracleParameter("sid", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = studentId
                }
            };

            try
            {
                var affected = await _db.ExecuteAsync(sql, parameters);
                return affected > 0;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Không thể cập nhật phản hồi {FeedbackId} của sinh viên {StudentId}", id, studentId);
                return false;
            }
        }

        /// <summary>
        /// Xóa phản hồi đang ở trạng thái nháp.
        /// </summary>
        public async Task<bool> DeleteAsync(string id, string studentId)
        {
            const string sql = @"DELETE FROM FEEDBACKS WHERE ID = :id AND STUDENT_ID = :sid AND STATUS = 'DRAFT'";

            var parameters = new[]
            {
                new OracleParameter("id", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = id
                },
                new OracleParameter("sid", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = studentId
                }
            };

            try
            {
                var affected = await _db.ExecuteAsync(sql, parameters);
                return affected > 0;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Không thể xóa phản hồi {FeedbackId} của sinh viên {StudentId}", id, studentId);
                return false;
            }
        }

        /// <summary>
        /// Ghi log vào bảng AUDIT_TRAIL.
        /// </summary>
        public async Task WriteAuditAsync(string studentId, string action, string clientIp, string userAgent, object details)
        {
            const string sql = @"
                INSERT INTO AUDIT_TRAIL (WHO, ACTION, EVENT_AT_UTC, CLIENT_IP, USER_AGENT, DETAILS)
                VALUES (:sid, :action, SYS_EXTRACT_UTC(SYSTIMESTAMP), :ip, :ua, :details)";

            var json = JsonSerializer.Serialize(details ?? new { });
            var parameters = new[]
            {
                new OracleParameter("sid", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = studentId
                },
                new OracleParameter("action", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = action
                },
                new OracleParameter("ip", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = clientIp ?? string.Empty
                },
                new OracleParameter("ua", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = userAgent ?? string.Empty
                },
                new OracleParameter("details", OracleDbType.Clob)
                {
                    Direction = ParameterDirection.Input,
                    Value = json
                }
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

        private static OracleParameter CreateNullableStringParameter(string name, string? value)
        {
            return new OracleParameter(name, OracleDbType.Varchar2)
            {
                Direction = ParameterDirection.Input,
                Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value
            };
        }
    }
}
