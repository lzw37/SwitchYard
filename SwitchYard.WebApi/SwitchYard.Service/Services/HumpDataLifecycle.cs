namespace SwitchYard.Service.Services;

/// <summary>Discard results whose geometry changed while retaining editable calculation settings.
/// Callers own the transaction.</summary>
public static class HumpDataLifecycle
{
    public static void RemoveCalculationReferences(DBConnector db, string instanceID, IEnumerable<string> calculationIDs)
    {
        foreach (var batch in calculationIDs.Distinct(StringComparer.Ordinal).Chunk(400))
        {
            var scope = new { instanceID, ids = batch };
            var checks = db.Query<string>("SELECT HeadwayCheckID FROM headwaycheckwagon WHERE InstanceID=@instanceID AND HumpCalculationID IN @ids", scope) ?? new();
            DeleteAndVerify(db, "headwaycheckwagon", "InstanceID=@instanceID AND HumpCalculationID IN @ids", scope);
            foreach (var checkBatch in checks.Distinct(StringComparer.Ordinal).Chunk(400))
                foreach (var table in new[] { "headwaycheckdata", "headwaycheckresult" })
                    DeleteAndVerify(db, table, "InstanceID=@instanceID AND HeadwayCheckID IN @ids", new { instanceID, ids = checkBatch });
        }
    }

    public static void InvalidateLayoutResults(DBConnector db, string instanceID, string? slopeLineID = null, string? humpSchemeID = null)
    {
        var filter = "InstanceID=@instanceID";
        if (slopeLineID is not null) filter += " AND SlopeLineID=@slopeLineID";
        if (humpSchemeID is not null) filter += " AND HumpSchemeID=@humpSchemeID";
        var scope = new { instanceID, slopeLineID, humpSchemeID };
        var calculations = db.Query<string>($"SELECT ID FROM humpcalculation WHERE {filter}", scope) ?? new();
        foreach (var batch in calculations.Distinct(StringComparer.Ordinal).Chunk(400))
        {
            var parameters = new { instanceID, ids = batch };
            DeleteAndVerify(db, "humpcalculationdata", "InstanceID=@instanceID AND HumpCalculationID IN @ids", parameters);
            DeleteAndVerify(db, "retarderstatus", "InstanceID=@instanceID AND HumpCalculationID IN @ids", parameters);
        }
        if (humpSchemeID is not null)
            DeleteAndVerify(db, "humpcalculationdata", "InstanceID=@instanceID AND HumpSchemeID=@humpSchemeID", scope);
        if (slopeLineID is not null)
        {
            // Clean device states even when their calculation row was already missing.
            DeleteAndVerify(db, "retarderstatus", "InstanceID=@instanceID AND RetarderID IN " +
                "(SELECT ID FROM retarder WHERE InstanceID=@instanceID AND SlopeLineID=@slopeLineID)", scope);
        }

        var checkIDs = db.Query<string>($"SELECT ID FROM headwaycheckscheme WHERE {filter}", scope) ?? new();
        // A check may refer to a calculation using a different planar line.
        foreach (var batch in calculations.Distinct(StringComparer.Ordinal).Chunk(400))
            checkIDs.AddRange(db.Query<string>("SELECT HeadwayCheckID FROM headwaycheckwagon WHERE InstanceID=@instanceID AND HumpCalculationID IN @ids",
                new { instanceID, ids = batch }) ?? new());
        foreach (var batch in checkIDs.Distinct(StringComparer.Ordinal).Chunk(400))
            foreach (var table in new[] { "headwaycheckdata", "headwaycheckresult" })
                DeleteAndVerify(db, table, "InstanceID=@instanceID AND HeadwayCheckID IN @ids", new { instanceID, ids = batch });
    }

    private static void DeleteAndVerify(DBConnector db, string table, string filter, object parameters)
    {
        db.ExecuteNonQuery($"DELETE FROM {table} WHERE {filter}", parameters);
        if ((db.Query<long>($"SELECT COUNT(1) FROM {table} WHERE {filter}", parameters)?.FirstOrDefault() ?? 0) != 0)
            throw new InvalidOperationException($"Result invalidation left rows in {table}.");
    }
}
