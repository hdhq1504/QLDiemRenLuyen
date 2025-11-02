using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using QLDiemRenLuyen.Student.Models.ViewModels;

namespace QLDiemRenLuyen.Data.Student
{
    /// <summary>
    /// Repository thao tác dữ liệu hồ sơ sinh viên.
    /// </summary>
    public class StudentProfileRepository
    {
        private readonly Database _db;
        private readonly ILogger<StudentProfileRepository> _logger;

        public StudentProfileRepository(Database db, ILogger<StudentProfileRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Lấy thông tin hồ sơ đầy đủ của sinh viên.
        /// </summary>
        public async Task<StudentProfileVm?> GetAsync(string studentId)
        {
            const string sql = @"SELECT u.MAND, u.EMAIL, u.FULL_NAME, u.ROLE_NAME, u.AVATAR_URL,
                   s.STUDENT_CODE, s.CLASS_ID, s.DEPARTMENT_ID, s.DOB, s.GENDER, s.PHONE, s.ADDRESS,
                   c.NAME AS CLASS_NAME, d.NAME AS DEPT_NAME
              FROM USERS u
         LEFT JOIN STUDENTS s   ON s.USER_ID = u.MAND
         LEFT JOIN CLASSES  c   ON c.ID = s.CLASS_ID
         LEFT JOIN DEPARTMENTS d ON d.ID = s.DEPARTMENT_ID
             WHERE u.MAND = :sid";

            try
            {
                await using var conn = (OracleConnection)_db.CreateConnection();
                await conn.OpenAsync();
                await using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;
                cmd.Parameters.Add(new OracleParameter("sid", OracleDbType.Varchar2, studentId, ParameterDirection.Input));

                await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
                if (!await reader.ReadAsync())
                {
                    return null;
                }

                var vm = new StudentProfileVm
                {
                    StudentId = reader.GetString(0),
                    Email = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    FullName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    RoleName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    AvatarUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
                    StudentCode = reader.IsDBNull(5) ? null : reader.GetString(5),
                    ClassId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    DepartmentId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    Dob = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                    Gender = reader.IsDBNull(9) ? null : reader.GetString(9),
                    Phone = reader.IsDBNull(10) ? null : reader.GetString(10),
                    Address = reader.IsDBNull(11) ? null : reader.GetString(11),
                    ClassName = reader.IsDBNull(12) ? null : reader.GetString(12),
                    DepartmentName = reader.IsDBNull(13) ? null : reader.GetString(13)
                };

                vm.Classes = await GetClassesAsync();
                vm.Departments = await GetDepartmentsAsync();
                return vm;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Không thể lấy thông tin hồ sơ cho sinh viên {StudentId}", studentId);
                return null;
            }
        }

        /// <summary>
        /// Đọc danh sách lớp phục vụ dropdown.
        /// </summary>
        public async Task<IEnumerable<LookupDto>> GetClassesAsync()
        {
            const string sql = "SELECT ID, NAME FROM CLASSES ORDER BY NAME";
            var items = new List<LookupDto>();

            try
            {
                await using var conn = (OracleConnection)_db.CreateConnection();
                await conn.OpenAsync();
                await using var cmd = new OracleCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
                while (await reader.ReadAsync())
                {
                    items.Add(new LookupDto
                    {
                        Id = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1)
                    });
                }
            }
            catch (OracleException ex)
            {
                _logger.LogWarning(ex, "Không thể đọc danh sách lớp. TODO: kiểm tra bảng CLASSES");
                // TODO: Nếu bảng CLASSES chưa tồn tại, cần bổ sung hoặc mock dữ liệu.
            }

            return items;
        }

        /// <summary>
        /// Đọc danh sách khoa/bộ môn phục vụ dropdown.
        /// </summary>
        public async Task<IEnumerable<LookupDto>> GetDepartmentsAsync()
        {
            const string sql = "SELECT ID, NAME FROM DEPARTMENTS ORDER BY NAME";
            var items = new List<LookupDto>();

            try
            {
                await using var conn = (OracleConnection)_db.CreateConnection();
                await conn.OpenAsync();
                await using var cmd = new OracleCommand(sql, conn);
                await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
                while (await reader.ReadAsync())
                {
                    items.Add(new LookupDto
                    {
                        Id = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1)
                    });
                }
            }
            catch (OracleException ex)
            {
                _logger.LogWarning(ex, "Không thể đọc danh sách khoa. TODO: kiểm tra bảng DEPARTMENTS");
                // TODO: Nếu bảng DEPARTMENTS chưa tồn tại, cần bổ sung hoặc mock dữ liệu.
            }

            return items;
        }

        /// <summary>
        /// Cập nhật thông tin hồ sơ chính (không bao gồm ảnh đại diện).
        /// </summary>
        public async Task<bool> UpdateAsync(StudentProfileEditVm vm, string studentId)
        {
            const string updateUser = "UPDATE USERS SET FULL_NAME = :fullName WHERE MAND = :sid";
            const string mergeStudent = @"
                MERGE INTO STUDENTS s
                USING (SELECT :sid AS USER_ID FROM DUAL) x
                   ON (s.USER_ID = x.USER_ID)
                 WHEN MATCHED THEN UPDATE SET
                      STUDENT_CODE = :studentCode,
                      CLASS_ID     = :classId,
                      DEPARTMENT_ID= :deptId,
                      DOB          = :dob,
                      GENDER       = :gender,
                      PHONE        = :phone,
                      ADDRESS      = :address
                 WHEN NOT MATCHED THEN INSERT (USER_ID, STUDENT_CODE, CLASS_ID, DEPARTMENT_ID, DOB, GENDER, PHONE, ADDRESS)
                      VALUES (:sid, :studentCode, :classId, :deptId, :dob, :gender, :phone, :address)";

            await using var conn = (OracleConnection)_db.CreateConnection();
            await conn.OpenAsync();
            await using var trans = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted);

            try
            {
                await using (var cmd = new OracleCommand(updateUser, conn))
                {
                    cmd.BindByName = true;
                    cmd.Transaction = (OracleTransaction)trans;

                    var pFullName = new OracleParameter("fullName", OracleDbType.NVarchar2) { Value = vm.FullName ?? string.Empty, Direction = ParameterDirection.Input };
                    var pSid = new OracleParameter("sid", OracleDbType.Varchar2) { Value = studentId, Direction = ParameterDirection.Input };
                    cmd.Parameters.Add(pFullName);
                    cmd.Parameters.Add(pSid);

                    await cmd.ExecuteNonQueryAsync();
                }

                await using (var cmd = new OracleCommand(mergeStudent, conn))
                {
                    cmd.BindByName = true;
                    cmd.Transaction = (OracleTransaction)trans;

                    var pSid = new OracleParameter("sid", OracleDbType.Varchar2) { Value = studentId, Direction = ParameterDirection.Input };

                    var pStudentCode = new OracleParameter("studentCode", OracleDbType.Varchar2) { Value = (object?)vm.StudentCode ?? DBNull.Value };
                    var pClassId = new OracleParameter("classId", OracleDbType.Int32) { Value = (object?)vm.ClassId ?? DBNull.Value };
                    var pDeptId = new OracleParameter("deptId", OracleDbType.Int32) { Value = (object?)vm.DepartmentId ?? DBNull.Value };
                    var pDob = new OracleParameter("dob", OracleDbType.Date) { Value = (object?)vm.Dob ?? DBNull.Value };
                    var pGender = new OracleParameter("gender", OracleDbType.Varchar2) { Value = (object?)vm.Gender ?? DBNull.Value };
                    var pPhone = new OracleParameter("phone", OracleDbType.Varchar2) { Value = (object?)vm.Phone ?? DBNull.Value };
                    var pAddress = new OracleParameter("address", OracleDbType.NVarchar2) { Value = (object?)vm.Address ?? DBNull.Value };

                    cmd.Parameters.Add(pSid);
                    cmd.Parameters.Add(pStudentCode);
                    cmd.Parameters.Add(pClassId);
                    cmd.Parameters.Add(pDeptId);
                    cmd.Parameters.Add(pDob);
                    cmd.Parameters.Add(pGender);
                    cmd.Parameters.Add(pPhone);
                    cmd.Parameters.Add(pAddress);

                    await cmd.ExecuteNonQueryAsync();
                }

                await trans.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                try { await trans.RollbackAsync(); } catch { /* ignore */ }
                _logger.LogError(ex, "Cập nhật hồ sơ thất bại cho sinh viên {StudentId}", studentId);
                return false;
            }
        }

        /// <summary>
        /// Cập nhật đường dẫn ảnh đại diện.
        /// </summary>
        public async Task<bool> UpdateAvatarAsync(string studentId, string avatarUrl)
        {
            const string sql = "UPDATE USERS SET AVATAR_URL = :avatar WHERE MAND = :sid";
            var parameters = new[]
            {
                new OracleParameter("avatar", OracleDbType.Varchar2, avatarUrl, ParameterDirection.Input),
                new OracleParameter("sid", OracleDbType.Varchar2, studentId, ParameterDirection.Input)
            };

            try
            {
                var affected = await _db.ExecuteAsync(sql, parameters);
                return affected > 0;
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Cập nhật avatar thất bại cho sinh viên {StudentId}", studentId);
                return false;
            }
        }

        /// <summary>
        /// Ghi nhận lịch sử tác động dữ liệu vào bảng AUDIT_TRAIL.
        /// </summary>
        public async Task WriteAuditAsync(string studentId, string action, string clientIp, string userAgent, IEnumerable<string> changedFields)
        {
            const string sql = @"INSERT INTO AUDIT_TRAIL (WHO, ACTION, EVENT_AT_UTC, CLIENT_IP, USER_AGENT, DETAILS)
                                VALUES (:sid, :action, SYS_EXTRACT_UTC(SYSTIMESTAMP), :ip, :ua, :details)";

            var details = JsonSerializer.Serialize(new
            {
                changed = changedFields?.Distinct().Where(f => !string.IsNullOrWhiteSpace(f)).ToArray() ?? Array.Empty<string>()
            });

            var parameters = new[]
            {
                new OracleParameter("sid", OracleDbType.Varchar2, studentId, ParameterDirection.Input),
                new OracleParameter("action", OracleDbType.Varchar2, action, ParameterDirection.Input),
                new OracleParameter("ip", OracleDbType.Varchar2, clientIp ?? string.Empty, ParameterDirection.Input),
                new OracleParameter("ua", OracleDbType.Varchar2, userAgent ?? string.Empty, ParameterDirection.Input),
                new OracleParameter("details", OracleDbType.Clob, details, ParameterDirection.Input)
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
    }
}
