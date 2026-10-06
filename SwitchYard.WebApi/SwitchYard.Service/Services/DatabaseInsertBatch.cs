using System.Text.RegularExpressions;

namespace SwitchYard.Service.Services;

/// <summary>Parameterized multi-row INSERTs on the caller's transaction (also below SQLite's 999-parameter limit).</summary>
internal sealed class DatabaseInsertBatch(DBConnector db)
{
    private readonly Dictionary<string, List<object>> _rows = new(StringComparer.Ordinal);
    public void Add(string sql, object row)
    {
        if (!_rows.TryGetValue(sql, out var rows)) _rows[sql] = rows = [];
        rows.Add(row);
    }
    public void Flush()
    {
        foreach (var (sql, rows) in _rows)
        {
            var match = Regex.Match(sql, @"\bVALUES\s*(\([^;]+\))\s*$", RegexOptions.IgnoreCase);
            if (!match.Success) throw new ArgumentException("Expected a single-row parameterized INSERT.");
            var template = match.Groups[1].Value;
            var names = Regex.Matches(template, @"@(\w+)").Select(value => value.Groups[1].Value).Distinct().ToArray();
            foreach (var chunk in rows.Chunk(Math.Min(100, 900 / names.Length)))
            {
                var parameters = new Dictionary<string, object?>();
                var values = new List<string>();
                for (var i = 0; i < chunk.Length; i++)
                {
                    var row = chunk[i];
                    foreach (var name in names) parameters[$"{name}_{i}"] = row.GetType().GetProperty(name)!.GetValue(row);
                    values.Add(Regex.Replace(template, @"@(\w+)", value => $"@{value.Groups[1].Value}_{i}"));
                }
                db.ExecuteNonQuery(sql[..match.Index] + "VALUES " + string.Join(',', values), parameters);
            }
        }
        _rows.Clear();
    }
}
