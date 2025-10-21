using System;
using Oracle.ManagedDataAccess.Client;
using QLDiemRenLuyen.Models;

namespace QLDiemRenLuyen.Data
{
    public class UserRepository
    {
        private readonly Database _db;
        public UserRepository(Database db) => _db = db;

        public Task<User?> GetByEmailAsync(string email)
        {
            const string sql = @"
                SELECT MAND, EMAIL, FULL_NAME, ROLE_NAME, IS_ACTIVE, CREATED_AT,
                       PASSWORD_HASH, PASSWORD_SALT, FAILED_LOGIN_COUNT, LOCKOUT_END_UTC
                  FROM USERS
                 WHERE LOWER(EMAIL) = LOWER(:email)";
            var p = new[] { new OracleParameter("email", email) };
            return _db.QuerySingleAsync(sql, rd => new User
            {
                MaND = rd.GetString(0),
                Email = rd.GetString(1),
                FullName = rd.GetString(2),
                RoleName = rd.GetString(3),
                IsActive = rd.GetDecimal(4) == 1,
                CreatedAt = rd.GetDateTime(5),
                PasswordHash = rd.GetString(6),
                PasswordSalt = rd.GetString(7),
                FailedLoginCount = rd.IsDBNull(8) ? 0 : Convert.ToInt32(rd.GetDecimal(8)),
                LockoutEndUtc = rd.IsDBNull(9) ? null : rd.GetDateTime(9)
            }, p);
        }

        public async Task CreateAsync(User u)
        {
            const string sql = @"
               INSERT INTO USERS (MAND, EMAIL, FULL_NAME, ROLE_NAME, PASSWORD_HASH, PASSWORD_SALT, IS_ACTIVE, FAILED_LOGIN_COUNT)
                VALUES (:mand, :email, :full, :role, :hash, :salt, 1, 0)";
            var prms = new[]
            {
                new OracleParameter("mand", u.MaND),
                new OracleParameter("email", u.Email),
                new OracleParameter("full", u.FullName),
                new OracleParameter("role", u.RoleName),
                new OracleParameter("hash", u.PasswordHash),
                new OracleParameter("salt", u.PasswordSalt)
            };
            await _db.ExecuteAsync(sql, prms);
        }

        public Task UpdateLoginAttemptsAsync(string email, int failedCount, DateTime? lockoutEndUtc)
        {
            const string sql = @"
                UPDATE USERS
                   SET FAILED_LOGIN_COUNT = :failed,
                       LOCKOUT_END_UTC = :lockout
                 WHERE LOWER(EMAIL) = LOWER(:email)";
            var prms = new[]
            {
                new OracleParameter("failed", failedCount),
                new OracleParameter("lockout", lockoutEndUtc.HasValue ? lockoutEndUtc.Value : (object)DBNull.Value),
                new OracleParameter("email", email)
            };
            return _db.ExecuteAsync(sql, prms);
        }

        public Task UpdatePasswordAsync(string userId, string newHash, string newSalt)
        {
            const string sql = @"
                UPDATE USERS
                   SET PASSWORD_HASH = :hash,
                       PASSWORD_SALT = :salt
                 WHERE MAND = :mand";
            var prms = new[]
            {
                new OracleParameter("hash", newHash),
                new OracleParameter("salt", newSalt),
                new OracleParameter("mand", userId)
            };
            return _db.ExecuteAsync(sql, prms);
        }

        public Task StorePasswordResetTokenAsync(string email, string token, DateTime expiresUtc)
        {
            const string sql = @"
                INSERT INTO PASSWORD_RESET_TOKENS (EMAIL, TOKEN, EXPIRES_AT_UTC)
                VALUES (:email, :token, :expires)";
            var prms = new[]
            {
                new OracleParameter("email", email),
                new OracleParameter("token", token),
                new OracleParameter("expires", expiresUtc)
            };
            return _db.ExecuteAsync(sql, prms);
        }

        public async Task AuditAsync(string who, string action, string? clientIp, string? ua)
        {
            const string sql = @"
                INSERT INTO AUDIT_TRAIL (WHO, ACTION, EVENT_AT_UTC, CLIENT_IP, USER_AGENT)
                VALUES (:who, :action, SYS_EXTRACT_UTC(SYSTIMESTAMP), :ip, :ua)";
            var prms = new[]
            {
                new OracleParameter("who", who),
                new OracleParameter("action", action),
                new OracleParameter("ip", clientIp ?? (object)DBNull.Value),
                new OracleParameter("ua", ua ?? (object)DBNull.Value)
            };
            await _db.ExecuteAsync(sql, prms);
        }
    }
}
