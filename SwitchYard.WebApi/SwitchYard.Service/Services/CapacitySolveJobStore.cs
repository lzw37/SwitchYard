using System.Text.Json;
using SwitchYard.Capacity;

namespace SwitchYard.Service.Services;

// The API owns the durable job record; a browser session is never its lifetime owner.
public sealed class CapacitySolveJobStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<StoredCapacitySolveJob> Load()
    {
        var db = Open();
        var jsonType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "LONGTEXT" : "TEXT";
        db.ExecuteNonQuery($@"CREATE TABLE IF NOT EXISTS capacitysolvejob (
            JobID VARCHAR(50) NOT NULL PRIMARY KEY,
            RecordJSON {jsonType} NOT NULL)");
        return (db.Query<string>("SELECT RecordJSON FROM capacitysolvejob") ?? new())
            .Select(json => JsonSerializer.Deserialize<StoredCapacitySolveJob>(json, JsonOptions)
                ?? throw new InvalidOperationException("求解任务记录无效。"))
            .ToList();
    }

    public void Save(StoredCapacitySolveJob record)
    {
        var sql = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName)
            ? "INSERT INTO capacitysolvejob (JobID, RecordJSON) VALUES (@id, @json) ON DUPLICATE KEY UPDATE RecordJSON = @json"
            : "INSERT INTO capacitysolvejob (JobID, RecordJSON) VALUES (@id, @json) ON CONFLICT(JobID) DO UPDATE SET RecordJSON = @json";
        Open().ExecuteNonQuery(sql, new { id = record.Job.JobId, json = JsonSerializer.Serialize(record, JsonOptions) });
    }

    public void Delete(string jobId) =>
        Open().ExecuteNonQuery("DELETE FROM capacitysolvejob WHERE JobID = @jobId", new { jobId });

    private static DBConnector Open() => DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
}

public sealed class StoredCapacitySolveJob
{
    public CapacitySolveJob Job { get; set; } = new();
    public CapacitySolveJobRequest Request { get; set; } = new();
    public SaturatedPlanJobContext? SaturatedPlanContext { get; set; }
}
