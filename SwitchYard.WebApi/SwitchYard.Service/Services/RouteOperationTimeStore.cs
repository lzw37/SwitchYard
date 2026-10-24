namespace SwitchYard.Service.Services;

/// <summary>One scalar duration per parameter-set route/train type. Callers own write transactions.</summary>
public static class RouteOperationTimeStore
{
    public const string Table = "stationrouteoperationtime";
    private const string Scope = "InstanceID=@instanceID AND StationSchemeID=@stationSchemeID AND CalculationParameterSetID=@parameterSetID AND RouteID=@routeID AND TrainTypeID=@trainTypeID";

    public static void EnsureSchema(DBConnector db) => db.ExecuteNonQuery(DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName)
        ? "CREATE TABLE IF NOT EXISTS stationrouteoperationtime (InstanceID VARCHAR(50) COLLATE utf8mb4_bin NOT NULL, StationSchemeID VARCHAR(50) COLLATE utf8mb4_bin NOT NULL, CalculationParameterSetID VARCHAR(50) COLLATE utf8mb4_bin NOT NULL, RouteID VARCHAR(50) COLLATE utf8mb4_bin NOT NULL, TrainTypeID VARCHAR(50) COLLATE utf8mb4_bin NOT NULL, OperationTimeS DOUBLE NOT NULL, PRIMARY KEY(InstanceID,StationSchemeID,CalculationParameterSetID,RouteID,TrainTypeID)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci"
        : "CREATE TABLE IF NOT EXISTS stationrouteoperationtime (InstanceID TEXT NOT NULL, StationSchemeID TEXT NOT NULL, CalculationParameterSetID TEXT NOT NULL, RouteID TEXT NOT NULL, TrainTypeID TEXT NOT NULL, OperationTimeS REAL NOT NULL CHECK(OperationTimeS >= 0), PRIMARY KEY(InstanceID,StationSchemeID,CalculationParameterSetID,RouteID,TrainTypeID))");

    public static void Validate(double? operationTimeS)
    {
        if (operationTimeS is double value && (!double.IsFinite(value) || value < 0))
            throw new ArgumentException("OperationTimeS must be a finite, nonnegative number of seconds.");
    }

    public static double? Load(DBConnector db, string instanceID, string stationSchemeID, string parameterSetID, string routeID, string trainTypeID) =>
        db.Query<double?>("SELECT OperationTimeS FROM " + Table + " WHERE " + Scope,
            new { instanceID, stationSchemeID, parameterSetID, routeID, trainTypeID })?.SingleOrDefault();

    public static void Save(DBConnector db, string instanceID, string stationSchemeID, string parameterSetID, string routeID, string trainTypeID, double? operationTimeS)
    {
        Validate(operationTimeS);
        var scope = new { instanceID, stationSchemeID, parameterSetID, routeID, trainTypeID, operationTimeS };
        db.ExecuteNonQuery("DELETE FROM " + Table + " WHERE " + Scope, scope);
        if (operationTimeS.HasValue)
            db.ExecuteNonQuery("INSERT INTO " + Table + "(InstanceID,StationSchemeID,CalculationParameterSetID,RouteID,TrainTypeID,OperationTimeS) VALUES(@instanceID,@stationSchemeID,@parameterSetID,@routeID,@trainTypeID,@operationTimeS)", scope);
    }
}
