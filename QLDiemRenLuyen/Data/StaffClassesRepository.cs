using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using QLDiemRenLuyen.ViewModels.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace QLDiemRenLuyen.Data
{
    /// <summary>
    /// Repository thao tác dữ liệu phục vụ nghiệp vụ cán bộ phòng công tác SV.
    /// </summary>
    public class StaffClassesRepository
    {
        private readonly Database _database;
        private readonly ILogger<StaffClassesRepository> _logger;

        public StaffClassesRepository(Database database, ILogger<StaffClassesRepository> logger)
        {
            _database = database;
            _logger = logger;
        }

        public async Task<IEnumerable<LookupDto>> GetClassesAsync()
        {
            const string sql = @"SELECT ID, NAME FROM CLASSES ORDER BY NAME";
            var result = new List<LookupDto>();

            try
            {
                await using var conn = (OracleConnection)_database.CreateConnection();
                await conn.OpenAsync();
                await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new LookupDto
                    {
                        Id = reader.GetString(0),
                        Name = reader.GetString(1)
                    });
                }

                return result;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy danh sách lớp cho cán bộ");
                throw;
            }
        }

        public async Task<bool> AddStudentToClassAsync(string studentId, string classId)
        {
            const string sql = @"
                MERGE INTO STUDENTS s
                USING (SELECT :sid AS USER_ID FROM DUAL) x
                   ON (s.USER_ID = x.USER_ID)
                 WHEN MATCHED THEN UPDATE SET CLASS_ID = :classId
                 WHEN NOT MATCHED THEN INSERT (USER_ID, CLASS_ID) VALUES (:sid, :classId)";

            try
            {
                await using var conn = (OracleConnection)_database.CreateConnection();
                await conn.OpenAsync();
                await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                var sidParam = new OracleParameter("sid", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = studentId
                };
                var classParam = new OracleParameter("classId", OracleDbType.Varchar2)
                {
                    Direction = ParameterDirection.Input,
                    Value = classId
                };
                cmd.Parameters.Add(sidParam);
                cmd.Parameters.Add(classParam);

                var affected = await cmd.ExecuteNonQueryAsync();
                return affected > 0;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Lỗi khi gán sinh viên {StudentId} vào lớp {ClassId}", studentId, classId);
                throw;
            }
        }
    }
}
