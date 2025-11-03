using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using QLDiemRenLuyen.ViewModels;

namespace QLDiemRenLuyen.Data
{
    /// <summary>
    /// Repository phục vụ dashboard cho giảng viên.
    /// </summary>
    public class LecturerDashboardRepository
    {
        private readonly Database _database;
        private readonly ILogger<LecturerDashboardRepository> _logger;

        public LecturerDashboardRepository(Database database, ILogger<LecturerDashboardRepository> logger)
        {
            _database = database;
            _logger = logger;
        }

        public async Task<IEnumerable<ClassRowVm>> GetMyClassesAsync(string lecturerId)
        {
            const string sql = @"
                SELECT c.ID, c.NAME, d.NAME AS DEPT_NAME
                  FROM CLASSES c
                  JOIN CLASS_MANAGERS cm ON cm.CLASS_ID = c.ID
                  LEFT JOIN DEPARTMENTS d ON d.ID = c.DEPARTMENT_ID
                 WHERE cm.LECTURER_ID = :lecturerId
                 ORDER BY c.NAME";

            var result = new List<ClassRowVm>();

            try
            {
                await using var conn = (OracleConnection)_database.CreateConnection();
                await conn.OpenAsync();
                await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add(new OracleParameter("lecturerId", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = lecturerId
                });

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new ClassRowVm
                    {
                        Id = reader.GetString(0),
                        Name = reader.GetString(1),
                        DepartmentName = reader.IsDBNull(2) ? null : reader.GetString(2)
                    });
                }

                return result;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách lớp của giảng viên {LecturerId}", lecturerId);
                throw;
            }
        }

        public async Task<IEnumerable<StudentScoreRowVm>> GetClassScoresAsync(string lecturerId, string classId, string termId)
        {
            const string sql = @"
                SELECT s.USER_ID, u.FULL_NAME, NVL(sc.TOTAL, 70) AS TOTAL, sc.STATUS, t.NAME AS TERM_NAME
                  FROM STUDENTS s
                  JOIN USERS u ON u.MAND = s.USER_ID
                  LEFT JOIN SCORES sc ON sc.STUDENT_ID = s.USER_ID AND sc.TERM_ID = :termId
                  JOIN CLASSES c ON c.ID = s.CLASS_ID
                  JOIN CLASS_MANAGERS cm ON cm.CLASS_ID = c.ID
                  JOIN TERMS t ON t.ID = :termId
                 WHERE cm.LECTURER_ID = :lecturerId AND c.ID = :classId
                 ORDER BY u.FULL_NAME";

            var result = new List<StudentScoreRowVm>();

            try
            {
                await using var conn = (OracleConnection)_database.CreateConnection();
                await conn.OpenAsync();
                await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add(new OracleParameter("termId", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = termId
                });
                cmd.Parameters.Add(new OracleParameter("lecturerId", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = lecturerId
                });
                cmd.Parameters.Add(new OracleParameter("classId", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = classId
                });

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new StudentScoreRowVm
                    {
                        StudentId = reader.GetString(0),
                        FullName = reader.GetString(1),
                        Total = reader.GetDecimal(2),
                        Status = reader.IsDBNull(3) ? "PROVISIONAL" : reader.GetString(3),
                        TermName = reader.GetString(4)
                    });
                }

                return result;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy điểm lớp {ClassId} - kỳ {TermId} cho giảng viên {LecturerId}", classId, termId, lecturerId);
                throw;
            }
        }
    }
}
