using System.Data;
using Microsoft.Data.SqlClient;

namespace Perpetuum.WikiGenerate;

/// <summary>
/// Read-only access to the Perpetuum database. All queries are static, parameter-free
/// SELECTs against tables verified against docs/db_structure (see idea.md Phase 2).
/// </summary>
public sealed class Db(string connectionString) : IDisposable
{
    public List<Dictionary<string, object?>> Query(string sql)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
        using var reader = cmd.ExecuteReader();
        var schema = reader.FieldCount;
        var names = new string[schema];
        for (var i = 0; i < schema; i++) names[i] = reader.GetName(i);
        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < schema; i++) row[names[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    public T? Scalar<T>(string sql)
    {
        using var conn = new SqlConnection(connectionString);
        conn.Open();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
        var v = cmd.ExecuteScalar();
        return v is null or DBNull ? default : (T)v;
    }

    public void Dispose() { }
}

public static class RowExt
{
    public static int Int(this Dictionary<string, object?> r, string k) => r.TryGetValue(k, out var v) && v is null ? 0 : Convert.ToInt32(v!);
    public static long Lng(this Dictionary<string, object?> r, string k) => r.TryGetValue(k, out var v) && v is null ? 0 : Convert.ToInt64(v!);
    public static double Dbl(this Dictionary<string, object?> r, string k) => r.TryGetValue(k, out var v) && v is null ? 0 : Convert.ToDouble(v!);
    public static string Str(this Dictionary<string, object?> r, string k) => r.TryGetValue(k, out var v) && v is null ? "" : v.ToString() ?? "";
    public static bool Bit(this Dictionary<string, object?> r, string k) => Convert.ToBoolean(r.Lng(k));
    public static bool HasFlag(this Dictionary<string, object?> r, string column, long flag) => (r.Lng(column) & flag) == flag;
}
