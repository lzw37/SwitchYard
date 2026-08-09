using System.Text.Json;
using SwitchYard.Capacity;

namespace SwitchYard.Service.Services;

public sealed class CapacitySolvePresetService
{
    private static readonly HashSet<string> SupportedObjectives = new(StringComparer.OrdinalIgnoreCase)
    {
        "min-end-time",
        "min-duration",
        "min-start-and-end"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<CapacitySolvePreset> GetAll(string owner)
    {
        var db = OpenDatabase();
        EnsureSchema(db);
        var rows = db.Query<PresetRow>(
            $@"SELECT PresetID, Name, Description, AgentID, ModelID, SettingsJSON, CreatedAt, UpdatedAt
               FROM {Quote("capacitysolvepreset")}
               WHERE Owner = @owner
               ORDER BY Name, PresetID",
            new { owner }) ?? new List<PresetRow>();
        return rows.Select(ToPreset).ToList();
    }

    public CapacitySolvePreset? Get(string presetId, string owner)
    {
        var normalizedPresetId = presetId?.Trim() ?? string.Empty;
        if (normalizedPresetId.Length == 0 || string.IsNullOrWhiteSpace(owner))
        {
            return null;
        }

        var db = OpenDatabase();
        EnsureSchema(db);
        var row = (db.Query<PresetRow>(
            $@"SELECT PresetID, Name, Description, AgentID, ModelID, SettingsJSON, CreatedAt, UpdatedAt
               FROM {Quote("capacitysolvepreset")}
               WHERE Owner = @owner AND PresetID = @normalizedPresetId
               LIMIT 1",
            new { owner, normalizedPresetId }) ?? new List<PresetRow>()).FirstOrDefault();
        return row == null ? null : ToPreset(row);
    }

    public CapacitySolvePreset Save(CapacitySolvePresetSaveRequest request, string owner, string? routePresetId = null)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            throw new ArgumentException("无法确定当前用户。", nameof(owner));
        }

        var name = Trim(request.Name, 100);
        var description = Trim(request.Description, 500);
        var agentId = Trim(request.AgentId, 100);
        var modelId = Trim(request.ModelId, 100);
        if (name.Length == 0)
        {
            throw new ArgumentException("预设名称不能为空。", nameof(request));
        }
        if (agentId.Length == 0 || modelId.Length == 0)
        {
            throw new ArgumentException("CapacityAgent 和模型不能为空。", nameof(request));
        }

        var requestedPresetId = string.IsNullOrWhiteSpace(routePresetId) ? request.PresetId : routePresetId;
        var presetId = Trim(requestedPresetId ?? string.Empty, 50);
        var isCreate = presetId.Length == 0;
        if (isCreate)
        {
            presetId = Guid.NewGuid().ToString("N");
        }

        var settings = NormalizeSettings(request.Settings);
        var now = DateTime.Now;
        var db = OpenDatabase();
        EnsureSchema(db);
        var exists = Exists(db, presetId, owner);
        if (!isCreate && !exists)
        {
            throw new KeyNotFoundException("求解预设不存在。即使是管理员，也只能修改自己的预设。");
        }

        var row = new PresetRow
        {
            PresetID = presetId,
            Owner = owner,
            Name = name,
            Description = description,
            AgentID = agentId,
            ModelID = modelId,
            SettingsJSON = JsonSerializer.Serialize(settings, JsonOptions),
            CreatedAt = now,
            UpdatedAt = now
        };

        if (exists)
        {
            db.ExecuteNonQuery(
                $@"UPDATE {Quote("capacitysolvepreset")}
                   SET Name = @Name,
                       Description = @Description,
                       AgentID = @AgentID,
                       ModelID = @ModelID,
                       SettingsJSON = @SettingsJSON,
                       UpdatedAt = @UpdatedAt
                   WHERE Owner = @Owner AND PresetID = @PresetID",
                row);
        }
        else
        {
            db.ExecuteNonQuery(
                $@"INSERT INTO {Quote("capacitysolvepreset")} (
                       PresetID, Owner, Name, Description, AgentID, ModelID, SettingsJSON, CreatedAt, UpdatedAt)
                   VALUES (
                       @PresetID, @Owner, @Name, @Description, @AgentID, @ModelID, @SettingsJSON, @CreatedAt, @UpdatedAt)",
                row);
        }

        return ToPreset(row);
    }

    public bool Delete(string presetId, string owner)
    {
        var normalizedPresetId = presetId?.Trim() ?? string.Empty;
        if (normalizedPresetId.Length == 0 || string.IsNullOrWhiteSpace(owner))
        {
            return false;
        }

        var db = OpenDatabase();
        EnsureSchema(db);
        return db.ExecuteNonQuery(
            $@"DELETE FROM {Quote("capacitysolvepreset")}
               WHERE Owner = @owner AND PresetID = @normalizedPresetId",
            new { owner, normalizedPresetId }) > 0;
    }

    public static StationCapacitySolveSettings NormalizeSettings(StationCapacitySolveSettings? settings)
    {
        settings ??= new StationCapacitySolveSettings();
        var objective = settings.Objective?.Trim() ?? string.Empty;
        if (!SupportedObjectives.Contains(objective))
        {
            objective = "min-end-time";
        }

        return new StationCapacitySolveSettings
        {
            LeftShiftToleranceSeconds = Math.Clamp(settings.LeftShiftToleranceSeconds, 0, 172_800),
            RightShiftToleranceSeconds = Math.Clamp(settings.RightShiftToleranceSeconds, 0, 172_800),
            TimeLimitSeconds = Math.Clamp(settings.TimeLimitSeconds, 1, 86_400),
            ThreadCount = Math.Clamp(settings.ThreadCount, 1, 128),
            Objective = objective
        };
    }

    private static CapacitySolvePreset ToPreset(PresetRow row)
    {
        StationCapacitySolveSettings? settings = null;
        try
        {
            settings = JsonSerializer.Deserialize<StationCapacitySolveSettings>(row.SettingsJSON ?? string.Empty, JsonOptions);
        }
        catch (JsonException)
        {
            // Keep old or manually edited rows usable by falling back to defaults.
        }

        return new CapacitySolvePreset
        {
            PresetId = row.PresetID ?? string.Empty,
            Name = row.Name ?? string.Empty,
            Description = row.Description ?? string.Empty,
            AgentId = row.AgentID ?? string.Empty,
            ModelId = row.ModelID ?? CapacityAgentProtocol.StationCapacityModelId,
            Settings = NormalizeSettings(settings),
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt
        };
    }

    private static bool Exists(DBConnector db, string presetId, string owner) =>
        (db.Query<PresetRow>(
            $@"SELECT PresetID
               FROM {Quote("capacitysolvepreset")}
               WHERE Owner = @owner AND PresetID = @presetId
               LIMIT 1",
            new { owner, presetId }) ?? new List<PresetRow>()).Any();

    private static DBConnector OpenDatabase() =>
        DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);

    private static void EnsureSchema(DBConnector db)
    {
        var table = Quote("capacitysolvepreset");
        if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
        {
            db.ExecuteNonQuery(
                $@"CREATE TABLE IF NOT EXISTS {table} (
                    PresetID VARCHAR(50) NOT NULL,
                    Owner VARCHAR(100) NOT NULL,
                    Name VARCHAR(100) NOT NULL,
                    Description VARCHAR(500) NULL,
                    AgentID VARCHAR(100) NOT NULL,
                    ModelID VARCHAR(100) NOT NULL,
                    SettingsJSON TEXT NOT NULL,
                    CreatedAt DATETIME NULL,
                    UpdatedAt DATETIME NULL,
                    PRIMARY KEY (Owner, PresetID)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
        }
        else
        {
            db.ExecuteNonQuery(
                $@"CREATE TABLE IF NOT EXISTS {table} (
                    PresetID TEXT NOT NULL,
                    Owner TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Description TEXT NULL,
                    AgentID TEXT NOT NULL,
                    ModelID TEXT NOT NULL,
                    SettingsJSON TEXT NOT NULL,
                    CreatedAt DATETIME NULL,
                    UpdatedAt DATETIME NULL,
                    PRIMARY KEY (Owner, PresetID)
                )");
        }
    }

    private static string Quote(string identifier) =>
        DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName)
            ? $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`"
            : $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string Trim(string? value, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private sealed class PresetRow
    {
        public string? PresetID { get; set; }
        public string? Owner { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? AgentID { get; set; }
        public string? ModelID { get; set; }
        public string? SettingsJSON { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
