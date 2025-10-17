using System.Data;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
namespace QLDiemRenLuyen.Data
{
    public class Database
    {
        private readonly string _connStr;
        public Database(IConfiguration cfg)
        {
            _connStr = cfg.GetConnectionString("OracleDb")!;
        }

        public IDbConnection CreateConnection() => new OracleConnection(_connStr);

        // Helper: Execute non-query
        public async Task<int> ExecuteAsync(string sql, IEnumerable<OracleParameter>? prms = null, CommandType type = CommandType.Text)
        {
            await using var conn = (OracleConnection)CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { CommandType = type };
            if (prms != null) cmd.Parameters.AddRange(prms.ToArray());
            return await cmd.ExecuteNonQueryAsync();
        }

        // Helper: Query single row (Func<OracleDataReader,T> mapper)
        public async Task<T?> QuerySingleAsync<T>(string sql, Func<OracleDataReader, T> map, IEnumerable<OracleParameter>? prms = null, CommandType type = CommandType.Text)
        {
            await using var conn = (OracleConnection)CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new OracleCommand(sql, conn) { CommandType = type };
            if (prms != null) cmd.Parameters.AddRange(prms.ToArray());
            await using var rd = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (await rd.ReadAsync()) return map(rd);
            return default;
        }
    }
}
