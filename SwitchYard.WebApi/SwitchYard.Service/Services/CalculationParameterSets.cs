using SwitchYard.Capacity;

namespace SwitchYard.Service.Services;

/// <summary>Scheme-scoped parameter selection. Missing references always resolve to the scheme default.</summary>
public sealed class CalculationParameterSets
{
    public const string InitialDefaultID = "default";
    public const string Scope = "InstanceID=@InstanceID AND StationSchemeID=@StationSchemeID";
    public List<CalculationParameterSet> Sets { get; }
    public string DefaultID => Sets.FirstOrDefault(set => set.IsDefault)?.ID ?? Sets.First().ID;

    public CalculationParameterSets(DBConnector db, string instanceID, string stationSchemeID)
    {
        Sets = db.Query<CalculationParameterSet>($"SELECT * FROM calculationparameterset WHERE {Scope} ORDER BY IsDefault DESC, ID",
            new { InstanceID = instanceID, StationSchemeID = stationSchemeID }) ?? [];
        // New schemes have a usable empty default even before their first parameter write.
        if (Sets.Count == 0) Sets.Add(new() { InstanceID = instanceID, StationSchemeID = stationSchemeID,
            ID = InitialDefaultID, Name = "默认计算参数", IsDefault = true });
    }

    public string Resolve(string? requestedID) => Sets.FirstOrDefault(set => set.ID == requestedID?.Trim())?.ID ?? DefaultID;

    // Keep an unassigned train unassigned; occupation generation and solving resolve the current default.
    public string? ResolveSelection(string? requestedID) => string.IsNullOrWhiteSpace(requestedID) ? null : Resolve(requestedID);

    public static void ResolveTrain(DBConnector db, TrainRow train) => train.CalculationParameterSetID =
        new CalculationParameterSets(db, train.InstanceID!, train.StationSchemeID!).ResolveSelection(train.CalculationParameterSetID);

    // Called inside a write transaction, after locking the station scheme.
    public static void EnsureDefault(DBConnector db, string instanceID, string stationSchemeID)
    {
        var scope = new { InstanceID = instanceID, StationSchemeID = stationSchemeID };
        if ((db.Query<int>($"SELECT COUNT(*) FROM calculationparameterset WHERE {Scope}", scope)?.Single() ?? 0) == 0)
            db.ExecuteNonQuery("INSERT INTO calculationparameterset(InstanceID,StationSchemeID,ID,Name,IsDefault) VALUES(@InstanceID,@StationSchemeID,'default',@name,1)",
                new { scope.InstanceID, scope.StationSchemeID, name = "默认计算参数" });
    }
}
