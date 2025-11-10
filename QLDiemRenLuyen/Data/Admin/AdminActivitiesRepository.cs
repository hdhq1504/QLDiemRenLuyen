using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using QLDiemRenLuyen.ViewModels.Common;
using QLDiemRenLuyen.ViewModels.Admin;

namespace QLDiemRenLuyen.Data
{
    /// <summary>
    /// Repository thao tác dữ liệu cho màn hình quản lý hoạt động của quản trị viên.
    /// </summary>
    public class AdminActivitiesRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly Database _db;
        private readonly ILogger<AdminActivitiesRepository> _logger;

        public AdminActivitiesRepository(Database db, ILogger<AdminActivitiesRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<PagedList<ActivityRowVm>> SearchAsync(ActivityFilter filter)
        {
            const string sql = @"
                SELECT *
                  FROM (
                        SELECT a.ID,
                               a.TERM_ID,
                               t.NAME AS TERM_NAME,
                               a.CRITERION_ID,
                               c.NAME AS CRITERION_NAME,
                               a.TITLE,
                               a.START_AT,
                               a.END_AT,
                               a.STATUS,
                               a.APPROVAL_STATUS,
                               a.MAX_SEATS,
                               (SELECT COUNT(*)
                                  FROM REGISTRATIONS r
                                 WHERE r.ACTIVITY_ID = a.ID
                                   AND r.STATUS IN ('REGISTERED','CHECKED_IN')) AS REG_COUNT,
                               (SELECT COUNT(*)
                                  FROM REGISTRATIONS r
                                 WHERE r.ACTIVITY_ID = a.ID
                                   AND r.STATUS = 'CHECKED_IN') AS CHECKED_COUNT,
                               ROW_NUMBER() OVER (ORDER BY a.START_AT DESC) AS RN
                          FROM ACTIVITIES a
                          LEFT JOIN TERMS t ON t.ID = a.TERM_ID
                          LEFT JOIN CRITERIA c ON c.ID = a.CRITERION_ID
                         WHERE (:termId IS NULL OR a.TERM_ID = :termId)
                           AND (:criterionId IS NULL OR a.CRITERION_ID = :criterionId)
                           AND (:status = 'all' OR a.STATUS = :status)
                           AND (:approval = 'all' OR a.APPROVAL_STATUS = :approval)
                           AND (
                                :kw IS NULL
                                OR LOWER(a.TITLE) LIKE '%' || LOWER(:kw) || '%'
                                OR REGEXP_LIKE(DBMS_LOB.SUBSTR(a.DESCRIPTION, 4000, 1), :kw, 'i')
                           )
                       )
                 WHERE RN BETWEEN :startRow AND :endRow";

            const string countSql = @"
                SELECT COUNT(*)
                  FROM ACTIVITIES a
                 WHERE (:termId IS NULL OR a.TERM_ID = :termId)
                   AND (:criterionId IS NULL OR a.CRITERION_ID = :criterionId)
                   AND (:status = 'all' OR a.STATUS = :status)
                   AND (:approval = 'all' OR a.APPROVAL_STATUS = :approval)
                   AND (
                        :kw IS NULL
                        OR LOWER(a.TITLE) LIKE '%' || LOWER(:kw) || '%'
                        OR REGEXP_LIKE(DBMS_LOB.SUBSTR(a.DESCRIPTION, 4000, 1), :kw, 'i')
                   )";

            var startRow = (filter.Page - 1) * filter.PageSize + 1;
            var endRow = startRow + filter.PageSize - 1;
            var keyword = string.IsNullOrWhiteSpace(filter.Keyword) ? null : filter.Keyword.Trim();

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();

            var items = new List<ActivityRowVm>();

            await using (var cmd = new OracleCommand(sql, conn)
            {
                BindByName = true
            })
            {
                cmd.Parameters.Add(CreateNullableStringParameter("termId", filter.TermId));
                cmd.Parameters.Add(CreateNullableStringParameter("criterionId", filter.CriterionId));
                cmd.Parameters.Add(new OracleParameter("status", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = filter.Status
                });
                cmd.Parameters.Add(new OracleParameter("approval", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = filter.Approval
                });
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
                    items.Add(new ActivityRowVm
                    {
                        Id = reader.GetString(0),
                        TermId = reader.IsDBNull(1) ? null : reader.GetString(1),
                        TermName = reader.IsDBNull(2) ? null : reader.GetString(2),
                        CriterionId = reader.IsDBNull(3) ? null : reader.GetString(3),
                        CriterionName = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Title = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        StartAt = reader.IsDBNull(6) ? DateTime.MinValue : reader.GetDateTime(6),
                        EndAt = reader.IsDBNull(7) ? DateTime.MinValue : reader.GetDateTime(7),
                        Status = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                        ApprovalStatus = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                        MaxSeats = reader.IsDBNull(10) ? null : Convert.ToInt32(reader.GetDecimal(10)),
                        RegisteredCount = reader.IsDBNull(11) ? 0 : Convert.ToInt32(reader.GetDecimal(11)),
                        CheckedInCount = reader.IsDBNull(12) ? 0 : Convert.ToInt32(reader.GetDecimal(12))
                    });
                }
            }

            int total;
            await using (var countCmd = new OracleCommand(countSql, conn)
            {
                BindByName = true
            })
            {
                countCmd.Parameters.Add(CreateNullableStringParameter("termId", filter.TermId));
                countCmd.Parameters.Add(CreateNullableStringParameter("criterionId", filter.CriterionId));
                countCmd.Parameters.Add(new OracleParameter("status", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = filter.Status
                });
                countCmd.Parameters.Add(new OracleParameter("approval", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = filter.Approval
                });
                countCmd.Parameters.Add(CreateNullableStringParameter("kw", keyword));

                total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            }

            var totalPages = (int)Math.Ceiling(total / (double)filter.PageSize);
            return new PagedList<ActivityRowVm>
            {
                Data = items,
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalItems = total,
                TotalPages = totalPages
            };
        }

        public async Task<ActivityDetailVm?> GetAsync(string id)
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
                       a.APPROVAL_STATUS,
                       a.MAX_SEATS,
                       a.LOCATION,
                       a.POINTS,
                       a.APPROVED_BY,
                       approver.FULL_NAME AS APPROVER_NAME,
                       a.APPROVED_AT,
                       a.ORGANIZER_ID,
                       organizer.FULL_NAME AS ORGANIZER_NAME,
                       a.CREATED_AT
                  FROM ACTIVITIES a
                  LEFT JOIN TERMS t ON t.ID = a.TERM_ID
                  LEFT JOIN CRITERIA c ON c.ID = a.CRITERION_ID
                  LEFT JOIN USERS approver ON approver.MAND = a.APPROVED_BY
                  LEFT JOIN USERS organizer ON organizer.MAND = a.ORGANIZER_ID
                 WHERE a.ID = :id";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn)
            {
                BindByName = true
            };
            cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Varchar2)
            {
                Direction = ParameterDirection.Input,
                Value = id
            });

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (await reader.ReadAsync())
            {
                return new ActivityDetailVm
                {
                    Id = reader.GetString(0),
                    Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    TermId = reader.IsDBNull(3) ? null : reader.GetString(3),
                    TermName = reader.IsDBNull(4) ? null : reader.GetString(4),
                    CriterionId = reader.IsDBNull(5) ? null : reader.GetString(5),
                    CriterionName = reader.IsDBNull(6) ? null : reader.GetString(6),
                    StartAt = reader.IsDBNull(7) ? DateTime.MinValue : reader.GetDateTime(7),
                    EndAt = reader.IsDBNull(8) ? DateTime.MinValue : reader.GetDateTime(8),
                    Status = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                    ApprovalStatus = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                    MaxSeats = reader.IsDBNull(11) ? null : Convert.ToInt32(reader.GetDecimal(11)),
                    Location = reader.IsDBNull(12) ? null : reader.GetString(12),
                    Points = reader.IsDBNull(13) ? null : reader.GetDecimal(13),
                    ApprovedBy = reader.IsDBNull(14) ? null : reader.GetString(14),
                    ApprovedByName = reader.IsDBNull(15) ? null : reader.GetString(15),
                    ApprovedAt = reader.IsDBNull(16) ? null : reader.GetDateTime(16),
                    OrganizerId = reader.IsDBNull(17) ? null : reader.GetString(17),
                    OrganizerName = reader.IsDBNull(18) ? null : reader.GetString(18),
                    CreatedAt = reader.IsDBNull(19) ? DateTime.MinValue : reader.GetDateTime(19)
                };
            }

            return null;
        }

        public async Task<IEnumerable<LookupDto>> GetTermsAsync()
        {
            const string sql = "SELECT ID, NAME FROM TERMS ORDER BY START_DATE DESC";
            var result = new List<LookupDto>();

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn)
            {
                BindByName = true
            };

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new LookupDto
                {
                    Id = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1)
                });
            }

            return result;
        }

        public async Task<IEnumerable<LookupDto>> GetCriteriaAsync()
        {
            const string sql = "SELECT ID, NAME FROM CRITERIA ORDER BY NAME";
            var result = new List<LookupDto>();

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn)
            {
                BindByName = true
            };

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                result.Add(new LookupDto
                {
                    Id = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1)
                });
            }

            return result;
        }

        public async Task<string> CreateAsync(ActivityEditVm vm, string adminId)
        {
            const string sql = @"
                INSERT INTO ACTIVITIES (ID, TITLE, DESCRIPTION, TERM_ID, CRITERION_ID, START_AT, END_AT,
                                        STATUS, MAX_SEATS, LOCATION, POINTS, APPROVAL_STATUS, ORGANIZER_ID, CREATED_AT)
                VALUES (RAWTOHEX(SYS_GUID()), :title, :desc, :termId, :critId, :startAt, :endAt,
                        :status, :maxSeats, :location, :points, 'PENDING', :organizerId, SYSTIMESTAMP)
                RETURNING ID INTO :id";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                await using var cmd = new OracleCommand(sql, conn)
                {
                    BindByName = true,
                    Transaction = tx
                };

                cmd.Parameters.Add(new OracleParameter("title", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.Title
                });
                cmd.Parameters.Add(new OracleParameter("desc", OracleDbType.Clob)
                {
                    Direction = ParameterDirection.Input,
                    Value = (object?)vm.Description ?? DBNull.Value
                });
                cmd.Parameters.Add(new OracleParameter("termId", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.TermId
                });
                cmd.Parameters.Add(new OracleParameter("critId", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.CriterionId
                });
                cmd.Parameters.Add(new OracleParameter("startAt", OracleDbType.TimeStamp)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.StartAt
                });
                cmd.Parameters.Add(new OracleParameter("endAt", OracleDbType.TimeStamp)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.EndAt
                });
                cmd.Parameters.Add(new OracleParameter("status", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.Status
                });
                cmd.Parameters.Add(new OracleParameter("maxSeats", OracleDbType.Int32)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.MaxSeats.HasValue ? vm.MaxSeats.Value : (object)DBNull.Value
                });
                cmd.Parameters.Add(new OracleParameter("location", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = string.IsNullOrWhiteSpace(vm.Location) ? (object)DBNull.Value : vm.Location.Trim()
                });
                cmd.Parameters.Add(new OracleParameter("points", OracleDbType.Decimal)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.Points.HasValue ? vm.Points.Value : (object)DBNull.Value
                });
                cmd.Parameters.Add(new OracleParameter("organizerId", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = adminId
                });
                var idOutput = new OracleParameter("id", OracleDbType.Varchar2, 32)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(idOutput);

                await cmd.ExecuteNonQueryAsync();
                var newId = Convert.ToString(idOutput.Value);

                await WriteAuditAsync(conn, tx, adminId, "ACTIVITY_CREATE", new
                {
                    id = newId,
                    vm.Title,
                    vm.TermId,
                    vm.CriterionId,
                    vm.Status
                });

                tx.Commit();
                return newId ?? string.Empty;
            }
            catch (OracleException ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Không thể tạo hoạt động mới cho admin {AdminId}", adminId);
                throw;
            }
        }

        public async Task<bool> UpdateAsync(string id, ActivityEditVm vm, string adminId)
        {
            const string sql = @"
                UPDATE ACTIVITIES
                   SET TITLE = :title,
                       DESCRIPTION = :desc,
                       TERM_ID = :termId,
                       CRITERION_ID = :critId,
                       START_AT = :startAt,
                       END_AT = :endAt,
                       STATUS = :status,
                       MAX_SEATS = :maxSeats,
                       LOCATION = :location,
                       POINTS = :points
                 WHERE ID = :id";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                await using var cmd = new OracleCommand(sql, conn)
                {
                    BindByName = true,
                    Transaction = tx
                };

                cmd.Parameters.Add(new OracleParameter("title", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.Title
                });
                cmd.Parameters.Add(new OracleParameter("desc", OracleDbType.Clob)
                {
                    Direction = ParameterDirection.Input,
                    Value = (object?)vm.Description ?? DBNull.Value
                });
                cmd.Parameters.Add(new OracleParameter("termId", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.TermId
                });
                cmd.Parameters.Add(new OracleParameter("critId", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.CriterionId
                });
                cmd.Parameters.Add(new OracleParameter("startAt", OracleDbType.TimeStamp)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.StartAt
                });
                cmd.Parameters.Add(new OracleParameter("endAt", OracleDbType.TimeStamp)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.EndAt
                });
                cmd.Parameters.Add(new OracleParameter("status", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.Status
                });
                cmd.Parameters.Add(new OracleParameter("maxSeats", OracleDbType.Int32)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.MaxSeats.HasValue ? vm.MaxSeats.Value : (object)DBNull.Value
                });
                cmd.Parameters.Add(new OracleParameter("location", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = string.IsNullOrWhiteSpace(vm.Location) ? (object)DBNull.Value : vm.Location.Trim()
                });
                cmd.Parameters.Add(new OracleParameter("points", OracleDbType.Decimal)
                {
                    Direction = ParameterDirection.Input,
                    Value = vm.Points.HasValue ? vm.Points.Value : (object)DBNull.Value
                });
                cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = id
                });

                var affected = await cmd.ExecuteNonQueryAsync();
                if (affected > 0)
                {
                    await WriteAuditAsync(conn, tx, adminId, "ACTIVITY_UPDATE", new
                    {
                        id,
                        vm.Title,
                        vm.Status
                    });
                }

                tx.Commit();
                return affected > 0;
            }
            catch (OracleException ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Không thể cập nhật hoạt động {ActivityId}", id);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(string id, string adminId)
        {
            const string sql = "DELETE FROM ACTIVITIES WHERE ID = :id";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                await using var cmd = new OracleCommand(sql, conn)
                {
                    BindByName = true,
                    Transaction = tx
                };
                cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = id
                });

                var affected = await cmd.ExecuteNonQueryAsync();
                if (affected > 0)
                {
                    await WriteAuditAsync(conn, tx, adminId, "ACTIVITY_DELETE", new { id });
                }

                tx.Commit();
                return affected > 0;
            }
            catch (OracleException ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Không thể xóa hoạt động {ActivityId}", id);
                throw;
            }
        }

        public async Task<bool> SetStatusAsync(string id, string status, string adminId)
        {
            const string sql = "UPDATE ACTIVITIES SET STATUS = :status WHERE ID = :id";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                await using var cmd = new OracleCommand(sql, conn)
                {
                    BindByName = true,
                    Transaction = tx
                };
                cmd.Parameters.Add(new OracleParameter("status", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = status
                });
                cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = id
                });

                var affected = await cmd.ExecuteNonQueryAsync();
                if (affected > 0)
                {
                    var auditAction = MapStatusToAudit(status);
                    await WriteAuditAsync(conn, tx, adminId, auditAction, new { id, status });
                }

                tx.Commit();
                return affected > 0;
            }
            catch (OracleException ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Không thể cập nhật trạng thái hoạt động {ActivityId} -> {Status}", id, status);
                throw;
            }
        }

        public async Task<bool> ApproveAsync(string id, string adminId)
        {
            const string sql = @"
                UPDATE ACTIVITIES
                   SET APPROVAL_STATUS = 'APPROVED',
                       APPROVED_BY = :adminId,
                       APPROVED_AT = SYSTIMESTAMP
                 WHERE ID = :id AND APPROVAL_STATUS <> 'APPROVED'";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                await using var cmd = new OracleCommand(sql, conn)
                {
                    BindByName = true,
                    Transaction = tx
                };
                cmd.Parameters.Add(new OracleParameter("adminId", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = adminId
                });
                cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = id
                });

                var affected = await cmd.ExecuteNonQueryAsync();
                if (affected > 0)
                {
                    await WriteAuditAsync(conn, tx, adminId, "ACTIVITY_APPROVE", new { id });
                }

                tx.Commit();
                return affected > 0;
            }
            catch (OracleException ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Không thể phê duyệt hoạt động {ActivityId}", id);
                throw;
            }
        }

        public async Task<bool> RejectAsync(string id, string adminId)
        {
            const string sql = @"
                UPDATE ACTIVITIES
                   SET APPROVAL_STATUS = 'REJECTED',
                       APPROVED_BY = :adminId,
                       APPROVED_AT = SYSTIMESTAMP
                 WHERE ID = :id AND APPROVAL_STATUS <> 'REJECTED'";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                await using var cmd = new OracleCommand(sql, conn)
                {
                    BindByName = true,
                    Transaction = tx
                };
                cmd.Parameters.Add(new OracleParameter("adminId", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = adminId
                });
                cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = id
                });

                var affected = await cmd.ExecuteNonQueryAsync();
                if (affected > 0)
                {
                    await WriteAuditAsync(conn, tx, adminId, "ACTIVITY_REJECT", new { id });
                }

                tx.Commit();
                return affected > 0;
            }
            catch (OracleException ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Không thể từ chối hoạt động {ActivityId}", id);
                throw;
            }
        }

        public async Task<(int registered, int checkedIn)> GetRegCountsAsync(string id)
        {
            const string sql = @"
                SELECT
                    NVL(SUM(CASE WHEN r.STATUS IN ('REGISTERED','CHECKED_IN') THEN 1 ELSE 0 END), 0) AS REGISTERED,
                    NVL(SUM(CASE WHEN r.STATUS = 'CHECKED_IN' THEN 1 ELSE 0 END), 0) AS CHECKED_IN
                  FROM REGISTRATIONS r
                 WHERE r.ACTIVITY_ID = :id";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn)
            {
                BindByName = true
            };
            cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Varchar2)
            {
                Direction = ParameterDirection.Input,
                Value = id
            });

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (await reader.ReadAsync())
            {
                var registered = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetDecimal(0));
                var checkedIn = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetDecimal(1));
                return (registered, checkedIn);
            }

            return (0, 0);
        }

        public async Task WriteAuditAsync(string who, string action, object details)
        {
            const string sql = @"
                INSERT INTO AUDIT_TRAIL (WHO, ACTION, EVENT_AT_UTC, DETAILS)
                VALUES (:who, :action, SYS_EXTRACT_UTC(SYSTIMESTAMP), :details)";

            var json = JsonSerializer.Serialize(details ?? new { }, JsonOptions);
            var parameters = new[]
            {
                new OracleParameter("who", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = who
                },
                new OracleParameter("action", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = action
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
                _logger.LogWarning(ex, "Ghi AUDIT_TRAIL thất bại cho admin {AdminId} - action {Action}", who, action);
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

        private static async Task WriteAuditAsync(OracleConnection conn, OracleTransaction tx, string who, string action, object details)
        {
            const string sql = @"
                INSERT INTO AUDIT_TRAIL (WHO, ACTION, EVENT_AT_UTC, DETAILS)
                VALUES (:who, :action, SYS_EXTRACT_UTC(SYSTIMESTAMP), :details)";

            var json = JsonSerializer.Serialize(details ?? new { }, JsonOptions);
            await using var cmd = new OracleCommand(sql, conn)
            {
                BindByName = true,
                Transaction = tx
            };
            cmd.Parameters.Add(new OracleParameter("who", OracleDbType.Varchar2)
            {
                Direction = ParameterDirection.Input,
                Value = who
            });
            cmd.Parameters.Add(new OracleParameter("action", OracleDbType.Varchar2)
            {
                Direction = ParameterDirection.Input,
                Value = action
            });
            cmd.Parameters.Add(new OracleParameter("details", OracleDbType.Clob)
            {
                Direction = ParameterDirection.Input,
                Value = json
            });

            await cmd.ExecuteNonQueryAsync();
        }

        private static string MapStatusToAudit(string status)
        {
            return status?.ToUpperInvariant() switch
            {
                "OPEN" => "ACTIVITY_OPEN",
                "CLOSED" => "ACTIVITY_CLOSE",
                "FULL" => "ACTIVITY_FULL",
                "CANCELLED" => "ACTIVITY_CANCEL",
                _ => "ACTIVITY_STATUS_CHANGE"
            };
        }
    }
}
