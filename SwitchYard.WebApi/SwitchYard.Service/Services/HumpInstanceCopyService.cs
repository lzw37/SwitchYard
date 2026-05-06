using SwitchYard.Hump;
using SwitchYard.Service.Utils;

namespace SwitchYard.Service.Services
{
    public sealed class HumpInstanceCopyResult
    {
        public bool Success { get; init; }

        public int StatusCode { get; init; }

        public string? ErrorMessage { get; init; }

        public HumpInstance? CopiedInstance { get; init; }

        public static HumpInstanceCopyResult Ok(HumpInstance instance)
        {
            return new HumpInstanceCopyResult
            {
                Success = true,
                StatusCode = 200,
                CopiedInstance = instance
            };
        }

        public static HumpInstanceCopyResult Fail(int statusCode, string message)
        {
            return new HumpInstanceCopyResult
            {
                Success = false,
                StatusCode = statusCode,
                ErrorMessage = message
            };
        }
    }

    public sealed class HumpInstanceCopyService
    {
        private readonly SnowflakeIdGenerator _snowflakeIdGenerator;
        private readonly ILogger<HumpInstanceCopyService> _logger;

        public HumpInstanceCopyService(
            SnowflakeIdGenerator snowflakeIdGenerator,
            ILogger<HumpInstanceCopyService> logger)
        {
            _snowflakeIdGenerator = snowflakeIdGenerator;
            _logger = logger;
        }

        public HumpInstanceCopyResult CopyTemplateInstanceForNewUser(string sourceInstanceID, string owner)
        {
            var normalizedSourceInstanceID = sourceInstanceID?.Trim() ?? string.Empty;
            var normalizedOwner = owner?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedSourceInstanceID))
            {
                return HumpInstanceCopyResult.Fail(400, "Source instance ID is required.");
            }

            if (string.IsNullOrWhiteSpace(normalizedOwner))
            {
                return HumpInstanceCopyResult.Fail(400, "Owner is required.");
            }

            var sourceInstanceName = GetInstanceName(normalizedSourceInstanceID);
            if (sourceInstanceName == null)
            {
                return HumpInstanceCopyResult.Fail(404, "Source instance not found.");
            }

            if (string.IsNullOrWhiteSpace(sourceInstanceName))
            {
                return HumpInstanceCopyResult.Fail(400, "Source instance name is required.");
            }

            return CopyInstance(
                normalizedSourceInstanceID,
                $"{sourceInstanceName}{normalizedOwner}",
                normalizedOwner);
        }

        public HumpInstanceCopyResult CopyInstance(string sourceInstanceID, string? newInstanceName, string owner)
        {
            var normalizedSourceInstanceID = sourceInstanceID?.Trim() ?? string.Empty;
            var normalizedNewInstanceName = newInstanceName?.Trim() ?? string.Empty;
            var normalizedOwner = owner?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedSourceInstanceID))
            {
                return HumpInstanceCopyResult.Fail(400, "Source instance ID is required.");
            }

            if (string.IsNullOrWhiteSpace(normalizedOwner))
            {
                return HumpInstanceCopyResult.Fail(400, "Owner is required.");
            }

            var dbConnector = DBConnector.GetDBConnector();
            var newInstanceID = string.Empty;

            try
            {
                dbConnector.BeginTransaction();

                var sourceInstance = (dbConnector.Query<HumpInstance>(
                    "SELECT * FROM humpinstance WHERE ID = @id",
                    new { id = normalizedSourceInstanceID }) ?? new List<HumpInstance>()).FirstOrDefault();
                if (sourceInstance == null)
                {
                    dbConnector.Rollback();
                    return HumpInstanceCopyResult.Fail(404, "Source instance not found.");
                }

                if (string.IsNullOrWhiteSpace(normalizedNewInstanceName))
                {
                    normalizedNewInstanceName = sourceInstance.Name?.Trim() ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(normalizedNewInstanceName))
                {
                    dbConnector.Rollback();
                    return HumpInstanceCopyResult.Fail(400, "New instance name is required.");
                }

                newInstanceID = GenerateUniqueInstanceId(dbConnector);

                var copiedInstance = new HumpInstance
                {
                    ID = newInstanceID,
                    Name = normalizedNewInstanceName,
                    Owner = normalizedOwner,
                    CreatedDate = DateTime.Now,
                    IsActive = sourceInstance.IsActive
                };

                EnsureWriteSucceeded(
                    dbConnector.ExecuteNonQuery(
                        "INSERT INTO humpinstance (ID, Name, Owner, CreatedDate, IsActive) VALUES (@ID, @Name, @Owner, @CreatedDate, @IsActive)",
                        copiedInstance),
                    "Insert humpinstance");

                var slopeLineIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
                var retarderIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
                var operationConditionIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
                var humpSchemeIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
                var humpCalculationIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
                var headwayCheckSchemeIdMap = new Dictionary<string, string>(StringComparer.Ordinal);

                var sourceSlopeLines = dbConnector.Query<SlopeLine>(
                    "SELECT * FROM slopeline WHERE InstanceID = @instanceID",
                    new { instanceID = normalizedSourceInstanceID }) ?? new List<SlopeLine>();

                foreach (var sourceSlopeLine in sourceSlopeLines)
                {
                    var newSlopeLineID = _snowflakeIdGenerator.NextIdString();
                    slopeLineIdMap[sourceSlopeLine.ID] = newSlopeLineID;

                    EnsureWriteSucceeded(
                        dbConnector.ExecuteNonQuery(
                            "INSERT INTO slopeline (ID, InstanceID, Name) VALUES (@ID, @InstanceID, @Name)",
                            new
                            {
                                ID = newSlopeLineID,
                                InstanceID = newInstanceID,
                                sourceSlopeLine.Name
                            }),
                        "Insert slopeline");

                    var sourcePositions = dbConnector.Query<HPosition>(
                        "SELECT * FROM position WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID",
                        new { instanceID = normalizedSourceInstanceID, slopeLineID = sourceSlopeLine.ID }) ?? new List<HPosition>();
                    foreach (var sourcePosition in sourcePositions)
                    {
                        EnsureWriteSucceeded(
                            dbConnector.ExecuteNonQuery(
                                "INSERT INTO position (ID, InstanceID, SlopeLineID, X, Height) VALUES (@ID, @InstanceID, @SlopeLineID, @X, @Height)",
                                new
                                {
                                    ID = sourcePosition.ID,
                                    InstanceID = newInstanceID,
                                    SlopeLineID = newSlopeLineID,
                                    sourcePosition.X,
                                    sourcePosition.Height
                                }),
                            "Insert position");
                    }

                    var sourceSegments = dbConnector.Query<HPositionSegment>(
                        "SELECT * FROM positionsegment WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID",
                        new { instanceID = normalizedSourceInstanceID, slopeLineID = sourceSlopeLine.ID }) ?? new List<HPositionSegment>();
                    foreach (var sourceSegment in sourceSegments)
                    {
                        EnsureWriteSucceeded(
                            dbConnector.ExecuteNonQuery(
                                "INSERT INTO positionsegment (ID, InstanceID, SlopeLineID, StartPositionID, EndPositionID, Length, CurveDegree, CurveDirection, LocationParam) VALUES (@ID, @InstanceID, @SlopeLineID, @StartPositionID, @EndPositionID, @Length, @CurveDegree, @CurveDirection, @LocationParam)",
                                new
                                {
                                    ID = sourceSegment.ID,
                                    InstanceID = newInstanceID,
                                    SlopeLineID = newSlopeLineID,
                                    sourceSegment.StartPositionID,
                                    sourceSegment.EndPositionID,
                                    sourceSegment.Length,
                                    sourceSegment.CurveDegree,
                                    sourceSegment.CurveDirection,
                                    sourceSegment.LocationParam
                                }),
                            "Insert positionsegment");
                    }

                    var sourceSwitches = dbConnector.Query<SwitchYard.Hump.Switch>(
                        "SELECT * FROM switch WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID",
                        new { instanceID = normalizedSourceInstanceID, slopeLineID = sourceSlopeLine.ID }) ?? new List<SwitchYard.Hump.Switch>();
                    foreach (var sourceSwitch in sourceSwitches)
                    {
                        EnsureWriteSucceeded(
                            dbConnector.ExecuteNonQuery(
                                "INSERT INTO switch (ID, InstanceID, SlopeLineID, BindingPositionID, BindingPositionSegmentID, CurveDegree, Type, Direction, Side) VALUES (@ID, @InstanceID, @SlopeLineID, @BindingPositionID, @BindingPositionSegmentID, @CurveDegree, @Type, @Direction, @Side)",
                                new
                                {
                                    ID = _snowflakeIdGenerator.NextIdString(),
                                    InstanceID = newInstanceID,
                                    SlopeLineID = newSlopeLineID,
                                    sourceSwitch.BindingPositionID,
                                    sourceSwitch.BindingPositionSegmentID,
                                    sourceSwitch.CurveDegree,
                                    sourceSwitch.Type,
                                    sourceSwitch.Direction,
                                    sourceSwitch.Side
                                }),
                            "Insert switch");
                    }

                    var sourceRetarders = dbConnector.Query<Retarder>(
                        "SELECT * FROM retarder WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID",
                        new { instanceID = normalizedSourceInstanceID, slopeLineID = sourceSlopeLine.ID }) ?? new List<Retarder>();
                    foreach (var sourceRetarder in sourceRetarders)
                    {
                        var newRetarderID = _snowflakeIdGenerator.NextIdString();
                        retarderIdMap[sourceRetarder.ID] = newRetarderID;

                        EnsureWriteSucceeded(
                            dbConnector.ExecuteNonQuery(
                                "INSERT INTO retarder (ID, InstanceID, SlopeLineID, BindingPositionSegmentID, Numbers) VALUES (@ID, @InstanceID, @SlopeLineID, @BindingPositionSegmentID, @Numbers)",
                                new
                                {
                                    ID = newRetarderID,
                                    InstanceID = newInstanceID,
                                    SlopeLineID = newSlopeLineID,
                                    sourceRetarder.BindingPositionSegmentID,
                                    sourceRetarder.Numbers
                                }),
                            "Insert retarder");
                    }
                }

                var sourceWagonConcepts = dbConnector.Query<WagonConcept>(
                    "SELECT * FROM wagonconcept WHERE InstanceID = @instanceID",
                    new { instanceID = normalizedSourceInstanceID }) ?? new List<WagonConcept>();
                foreach (var sourceWagonConcept in sourceWagonConcepts)
                {
                    EnsureWriteSucceeded(
                        dbConnector.ExecuteNonQuery(
                            "INSERT INTO wagonconcept (InstanceID, TypeName, Length, NetMass, LoadingMass, WindwardArea, AxleNumber, Label, g) VALUES (@InstanceID, @TypeName, @Length, @NetMass, @LoadingMass, @WindwardArea, @AxleNumber, @Label, @g)",
                            new
                            {
                                InstanceID = newInstanceID,
                                sourceWagonConcept.TypeName,
                                sourceWagonConcept.Length,
                                sourceWagonConcept.NetMass,
                                sourceWagonConcept.LoadingMass,
                                sourceWagonConcept.WindwardArea,
                                sourceWagonConcept.AxleNumber,
                                sourceWagonConcept.Label,
                                sourceWagonConcept.g
                            }),
                        "Insert wagonconcept");
                }

                var sourceOperationConditions = dbConnector.Query<OperationCondition>(
                    "SELECT * FROM operationcondition WHERE InstanceID = @instanceID",
                    new { instanceID = normalizedSourceInstanceID }) ?? new List<OperationCondition>();
                foreach (var sourceCondition in sourceOperationConditions)
                {
                    var newConditionID = _snowflakeIdGenerator.NextIdString();
                    operationConditionIdMap[sourceCondition.ID] = newConditionID;

                    EnsureWriteSucceeded(
                        dbConnector.ExecuteNonQuery(
                            "INSERT INTO operationcondition (InstanceID, ID, WagonVelocityOnTop, WagonVelocityOnSlope, WagonVelocityOnYard, WindVelocity, IsHeadWind, AirDensity, Temperature, Name) VALUES (@InstanceID, @ID, @WagonVelocityOnTop, @WagonVelocityOnSlope, @WagonVelocityOnYard, @WindVelocity, @IsHeadWind, @AirDensity, @Temperature, @Name)",
                            new
                            {
                                InstanceID = newInstanceID,
                                ID = newConditionID,
                                sourceCondition.WagonVelocityOnTop,
                                sourceCondition.WagonVelocityOnSlope,
                                sourceCondition.WagonVelocityOnYard,
                                sourceCondition.WindVelocity,
                                sourceCondition.IsHeadWind,
                                sourceCondition.AirDensity,
                                sourceCondition.Temperature,
                                sourceCondition.Name
                            }),
                        "Insert operationcondition");
                }

                var sourceHumpSchemes = dbConnector.Query<HumpScheme>(
                    "SELECT * FROM humpscheme WHERE InstanceID = @instanceID",
                    new { instanceID = normalizedSourceInstanceID }) ?? new List<HumpScheme>();
                foreach (var sourceHumpScheme in sourceHumpSchemes)
                {
                    var newHumpSchemeID = _snowflakeIdGenerator.NextIdString();
                    humpSchemeIdMap[sourceHumpScheme.ID] = newHumpSchemeID;

                    EnsureWriteSucceeded(
                        dbConnector.ExecuteNonQuery(
                            "INSERT INTO humpscheme (InstanceID, ID, Name) VALUES (@InstanceID, @ID, @Name)",
                            new
                            {
                                InstanceID = newInstanceID,
                                ID = newHumpSchemeID,
                                sourceHumpScheme.Name
                            }),
                        "Insert humpscheme");

                    var sourceVPositions = dbConnector.Query<VPosition>(
                        "SELECT * FROM vposition WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID",
                        new { instanceID = normalizedSourceInstanceID, humpSchemeID = sourceHumpScheme.ID }) ?? new List<VPosition>();
                    foreach (var sourceVPosition in sourceVPositions)
                    {
                        EnsureWriteSucceeded(
                            dbConnector.ExecuteNonQuery(
                                "INSERT INTO vposition (ID, InstanceID, HumpSchemeID, X, Height) VALUES (@ID, @InstanceID, @HumpSchemeID, @X, @Height)",
                                new
                                {
                                    ID = sourceVPosition.ID,
                                    InstanceID = newInstanceID,
                                    HumpSchemeID = newHumpSchemeID,
                                    sourceVPosition.X,
                                    sourceVPosition.Height
                                }),
                            "Insert vposition");
                    }

                    var sourceVSegments = dbConnector.Query<VPositionSegment>(
                        "SELECT * FROM vpositionsegment WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID",
                        new { instanceID = normalizedSourceInstanceID, humpSchemeID = sourceHumpScheme.ID }) ?? new List<VPositionSegment>();
                    foreach (var sourceVSegment in sourceVSegments)
                    {
                        EnsureWriteSucceeded(
                            dbConnector.ExecuteNonQuery(
                                "INSERT INTO vpositionsegment (ID, InstanceID, HumpSchemeID, StartPositionID, EndPositionID, Length, Gradient, Height) VALUES (@ID, @InstanceID, @HumpSchemeID, @StartPositionID, @EndPositionID, @Length, @Gradient, @Height)",
                                new
                                {
                                    ID = sourceVSegment.ID,
                                    InstanceID = newInstanceID,
                                    HumpSchemeID = newHumpSchemeID,
                                    sourceVSegment.StartPositionID,
                                    sourceVSegment.EndPositionID,
                                    sourceVSegment.Length,
                                    sourceVSegment.Gradient,
                                    sourceVSegment.Height
                                }),
                            "Insert vpositionsegment");
                    }
                }

                var sourceHumpCalculations = dbConnector.Query<HumpCalculation>(
                    "SELECT * FROM humpcalculation WHERE InstanceID = @instanceID",
                    new { instanceID = normalizedSourceInstanceID }) ?? new List<HumpCalculation>();
                foreach (var sourceCalculation in sourceHumpCalculations)
                {
                    var newCalculationID = _snowflakeIdGenerator.NextIdString();
                    humpCalculationIdMap[sourceCalculation.ID] = newCalculationID;

                    EnsureWriteSucceeded(
                        dbConnector.ExecuteNonQuery(
                            "INSERT INTO humpcalculation (InstanceID, HumpSchemeID, ID, WagonType, OperationConditionID, SlopeLineID) VALUES (@InstanceID, @HumpSchemeID, @ID, @WagonType, @OperationConditionID, @SlopeLineID)",
                            new
                            {
                                InstanceID = newInstanceID,
                                HumpSchemeID = RequireMappedId(humpSchemeIdMap, sourceCalculation.HumpSchemeID, "Hump scheme"),
                                ID = newCalculationID,
                                sourceCalculation.WagonType,
                                OperationConditionID = RequireMappedId(operationConditionIdMap, sourceCalculation.OperationConditionID, "Operation condition"),
                                SlopeLineID = RequireMappedId(slopeLineIdMap, sourceCalculation.SlopeLineID, "Slope line")
                            }),
                        "Insert humpcalculation");

                    var mappedRetarderStatusList = (LoadRetarderStatusList(dbConnector, normalizedSourceInstanceID, sourceCalculation.ID) ?? new List<RetarderStatus>())
                        .Select(status => new RetarderStatus
                        {
                            RetarderID = RequireMappedId(retarderIdMap, status.RetarderID, "Retarder"),
                            IsActivated = status.IsActivated,
                            Output = status.Output,
                            TotalEnergyHeight = status.TotalEnergyHeight
                        })
                        .ToList();

                    SaveRetarderStatusList(dbConnector, newInstanceID, newCalculationID, mappedRetarderStatusList);
                }

                var sourceCalculationData = dbConnector.Query<HumpCalculationData>(
                    "SELECT * FROM humpcalculationdata WHERE InstanceID = @instanceID",
                    new { instanceID = normalizedSourceInstanceID }) ?? new List<HumpCalculationData>();
                foreach (var sourceData in sourceCalculationData)
                {
                    EnsureWriteSucceeded(
                        dbConnector.ExecuteNonQuery(
                            "INSERT INTO humpcalculationdata (InstanceID, HumpSchemeID, HumpCalculationID, X, GravityEnergyHeight, ResistanceEnergyHeight, KineticEnergyHeight, BreakingEnergyHeight, InitTotalEnergyHeight) VALUES (@InstanceID, @HumpSchemeID, @HumpCalculationID, @X, @GravityEnergyHeight, @ResistanceEnergyHeight, @KineticEnergyHeight, @BreakingEnergyHeight, @InitTotalEnergyHeight)",
                            new
                            {
                                InstanceID = newInstanceID,
                                HumpSchemeID = RequireMappedId(humpSchemeIdMap, sourceData.HumpSchemeID, "Hump scheme"),
                                HumpCalculationID = RequireMappedId(humpCalculationIdMap, sourceData.HumpCalculationID, "Hump calculation"),
                                sourceData.X,
                                sourceData.GravityEnergyHeight,
                                sourceData.ResistanceEnergyHeight,
                                sourceData.KineticEnergyHeight,
                                sourceData.BreakingEnergyHeight,
                                sourceData.InitTotalEnergyHeight
                            }),
                        "Insert humpcalculationdata");
                }

                var sourceHeadwaySchemes = dbConnector.Query<HeadwayCheckScheme>(
                    "SELECT * FROM headwaycheckscheme WHERE InstanceID = @instanceID",
                    new { instanceID = normalizedSourceInstanceID }) ?? new List<HeadwayCheckScheme>();
                foreach (var sourceHeadwayScheme in sourceHeadwaySchemes)
                {
                    var newHeadwaySchemeID = _snowflakeIdGenerator.NextIdString();
                    headwayCheckSchemeIdMap[sourceHeadwayScheme.ID] = newHeadwaySchemeID;

                    EnsureWriteSucceeded(
                        dbConnector.ExecuteNonQuery(
                            "INSERT INTO headwaycheckscheme (InstanceID, ID, Name, HumpSchemeID, WagonVelocityOnTop, SlopeLineID) VALUES (@InstanceID, @ID, @Name, @HumpSchemeID, @WagonVelocityOnTop, @SlopeLineID)",
                            new
                            {
                                InstanceID = newInstanceID,
                                ID = newHeadwaySchemeID,
                                sourceHeadwayScheme.Name,
                                HumpSchemeID = RequireMappedId(humpSchemeIdMap, sourceHeadwayScheme.HumpSchemeID, "Hump scheme"),
                                sourceHeadwayScheme.WagonVelocityOnTop,
                                SlopeLineID = RequireMappedId(slopeLineIdMap, sourceHeadwayScheme.SlopeLineID, "Slope line")
                            }),
                        "Insert headwaycheckscheme");

                    var sourceHeadwayWagons = dbConnector.Query<HeadwayCheckWagon>(
                        "SELECT * FROM headwaycheckwagon WHERE InstanceID = @instanceID AND HeadwayCheckID = @headwayCheckID ORDER BY Sequence",
                        new { instanceID = normalizedSourceInstanceID, headwayCheckID = sourceHeadwayScheme.ID }) ?? new List<HeadwayCheckWagon>();
                    foreach (var sourceHeadwayWagon in sourceHeadwayWagons)
                    {
                        EnsureWriteSucceeded(
                            dbConnector.ExecuteNonQuery(
                                "INSERT INTO headwaycheckwagon (InstanceID, HeadwayCheckID, Sequence, HumpCalculationID) VALUES (@InstanceID, @HeadwayCheckID, @Sequence, @HumpCalculationID)",
                                new
                                {
                                    InstanceID = newInstanceID,
                                    HeadwayCheckID = newHeadwaySchemeID,
                                    sourceHeadwayWagon.Sequence,
                                    HumpCalculationID = RequireMappedId(humpCalculationIdMap, sourceHeadwayWagon.HumpCalculationID, "Hump calculation")
                                }),
                            "Insert headwaycheckwagon");
                    }
                }

                dbConnector.Commit();

                _logger.LogInformation(
                    "Copied HumpInstance from {SourceInstanceID} to {TargetInstanceID} for owner {Owner}.",
                    normalizedSourceInstanceID,
                    newInstanceID,
                    normalizedOwner);

                return HumpInstanceCopyResult.Ok(copiedInstance);
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                _logger.LogError(
                    ex,
                    "Error copying HumpInstance from {SourceInstanceID} to {TargetInstanceID} for owner {Owner}.",
                    normalizedSourceInstanceID,
                    newInstanceID,
                    normalizedOwner);
                return HumpInstanceCopyResult.Fail(500, "Internal server error while copying HumpInstance.");
            }
        }

        private void EnsureWriteSucceeded(int affectedRows, string operationName)
        {
            if (affectedRows <= 0)
            {
                throw new InvalidOperationException($"{operationName} failed.");
            }
        }

        private string RequireMappedId(Dictionary<string, string> idMap, string? sourceId, string mappingName)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new InvalidOperationException($"{mappingName} source ID is empty.");
            }

            if (!idMap.TryGetValue(sourceId, out var mappedId) || string.IsNullOrWhiteSpace(mappedId))
            {
                throw new InvalidOperationException($"{mappingName} mapping is missing for source ID {sourceId}.");
            }

            return mappedId;
        }

        private string GenerateUniqueInstanceId(DBConnector dbConnector)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var candidate = _snowflakeIdGenerator.NextIdString();
                var exists = (dbConnector.Query<HumpInstance>(
                    "SELECT * FROM humpinstance WHERE ID = @id",
                    new { id = candidate }) ?? new List<HumpInstance>()).Any();

                if (!exists)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Failed to generate a unique instance ID.");
        }

        private static string? GetInstanceName(string instanceID)
        {
            var dbConnector = DBConnector.GetDBConnector();
            var sourceInstance = (dbConnector.Query<HumpInstance>(
                "SELECT * FROM humpinstance WHERE ID = @id",
                new { id = instanceID }) ?? new List<HumpInstance>()).FirstOrDefault();

            return sourceInstance?.Name?.Trim();
        }

        private List<RetarderStatus> LoadRetarderStatusList(DBConnector dbConnector, string instanceID, string humpCalculationID)
        {
            var retarderStatusList = dbConnector.Query<RetarderStatus>(
                "SELECT RetarderID, COALESCE(IsActivated, 0) AS IsActivated, COALESCE(Output, 0) AS Output, COALESCE(TotalEnergyHeight, 0) AS TotalEnergyHeight FROM retarderstatus WHERE InstanceID = @instanceID AND HumpCalculationID = @humpCalculationID",
                new { instanceID, humpCalculationID });

            return retarderStatusList ?? new List<RetarderStatus>();
        }

        private void SaveRetarderStatusList(DBConnector dbConnector, string instanceID, string humpCalculationID, List<RetarderStatus>? retarderStatusList)
        {
            dbConnector.ExecuteNonQuery(
                "DELETE FROM retarderstatus WHERE InstanceID = @instanceID AND HumpCalculationID = @humpCalculationID",
                new { instanceID, humpCalculationID });

            if (retarderStatusList == null || retarderStatusList.Count == 0)
            {
                return;
            }

            foreach (var retarderStatus in retarderStatusList)
            {
                dbConnector.ExecuteNonQuery(
                    "INSERT INTO retarderstatus (InstanceID, RetarderID, IsActivated, Output, TotalEnergyHeight, HumpCalculationID) VALUES (@InstanceID, @RetarderID, @IsActivated, @Output, @TotalEnergyHeight, @HumpCalculationID)",
                    new
                    {
                        InstanceID = instanceID,
                        retarderStatus.RetarderID,
                        IsActivated = retarderStatus.IsActivated ? 1 : 0,
                        retarderStatus.Output,
                        retarderStatus.TotalEnergyHeight,
                        HumpCalculationID = humpCalculationID
                    });
            }
        }
    }
}
