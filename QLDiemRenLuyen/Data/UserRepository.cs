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
                SELECT ID, EMAIL, FULL_NAME, ROLE_NAME, IS_ACTIVE, CREATED_AT, PASSWORD_HASH, PASSWORD_SALT
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
                PasswordSalt = rd.GetString(7)
            }, p);
        }

        public async Task CreateAsync(User u)
        {
            const string sql = @"
                INSERT INTO USERS (ID, EMAIL, FULL_NAME, ROLE_NAME, PASSWORD_HASH, PASSWORD_SALT, IS_ACTIVE)
                VALUES (:id, :email, :full, :role, :hash, :salt, 1)";
            var prms = new[] {
                new OracleParameter("id",   u.MaND),
                new OracleParameter("email",u.Email),
                new OracleParameter("full", u.FullName),
                new OracleParameter("role", u.RoleName),
                new OracleParameter("hash", u.PasswordHash),
                new OracleParameter("salt", u.PasswordSalt)
            };
            await _db.ExecuteAsync(sql, prms);
        }

        public async Task AuditAsync(string email, string action, string? clientIp, string? ua)
        {
            const string sql = @"INSERT INTO AUTH_AUDIT(EMAIL, ACTION, CLIENT_IP, USER_AGENT)
                                 VALUES(:e,:a,:ip,:ua)";
            var prms = new[] {
                new OracleParameter("e", email),
                new OracleParameter("a", action),
                new OracleParameter("ip", clientIp ?? (object)DBNull.Value),
                new OracleParameter("ua", ua ?? (object)DBNull.Value)
            };
            await _db.ExecuteAsync(sql, prms);
        }
    }
}
