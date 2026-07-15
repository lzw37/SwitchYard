using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwitchYard.Capacity;
using SwitchYard.Service.Utils;

namespace SwitchYard.Service.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    [Authorize]
    public class OperationPlanController : ControllerBase
    {
        private readonly ILogger<OperationPlanController> _logger;
        private readonly SnowflakeIdGenerator _snowflakeIdGenerator;
        private const string DefaultOperationPlanID = "default";
        private const string DefaultOperationPlanName = "默认作业计划";

        public OperationPlanController(
            ILogger<OperationPlanController> logger,
            SnowflakeIdGenerator snowflakeIdGenerator)
        {
            _logger = logger;
            _snowflakeIdGenerator = snowflakeIdGenerator;
        }

        [HttpGet(Name = "GetOperationPlans")]
        public IActionResult GetOperationPlans(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null)
        {
            try
            {
                var scope = NormalizeScope(instanceID, stationSchemeID);
                if (scope.ErrorResult != null)
                {
                    return scope.ErrorResult;
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, scope.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationPlanObjectSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, scope.InstanceID!, scope.StationSchemeID!);
                return Ok(LoadOperationPlans(dbConnector, scope.InstanceID!, scope.StationSchemeID!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load operation plans.");
                return StatusCode(500, "Failed to load operation plans.");
            }
        }

        [HttpPost(Name = "CreateOperationPlan")]
        public IActionResult CreateOperationPlan([FromBody] OperationPlanRequest? request)
        {
            try
            {
                var dbConnector = GetCapacityDbConnector();
                var normalized = NormalizeOperationPlanRequest(request, allowMissingID: true);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                var operationPlan = normalized.OperationPlan!;
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, operationPlan.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationPlanObjectSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, operationPlan.InstanceID!, operationPlan.StationSchemeID!);
                if (string.IsNullOrWhiteSpace(operationPlan.OperationPlanID))
                {
                    operationPlan.OperationPlanID = GenerateOperationPlanID(dbConnector, operationPlan.InstanceID!, operationPlan.StationSchemeID!);
                }

                if (OperationPlanExists(dbConnector, operationPlan.InstanceID!, operationPlan.StationSchemeID!, operationPlan.OperationPlanID!))
                {
                    return BadRequest("Operation plan ID already exists in the selected station scheme.");
                }

                operationPlan.CreatedDate = DateTime.Now;
                operationPlan.UpdatedDate = operationPlan.CreatedDate;
                InsertOperationPlan(dbConnector, operationPlan);
                return Ok(operationPlan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create operation plan.");
                return StatusCode(500, "Failed to create operation plan.");
            }
        }

        [HttpPut(Name = "EditOperationPlan")]
        public IActionResult EditOperationPlan([FromBody] OperationPlanRequest? request)
        {
            DBConnector? dbConnector = null;
            try
            {
                dbConnector = GetCapacityDbConnector();
                var normalized = NormalizeOperationPlanRequest(request, allowMissingID: false);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                var operationPlan = normalized.OperationPlan!;
                var originalOperationPlanID = request?.OriginalOperationPlanID?.Trim();
                if (string.IsNullOrWhiteSpace(originalOperationPlanID))
                {
                    originalOperationPlanID = operationPlan.OperationPlanID;
                }

                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, operationPlan.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureTrainOperationPlanSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, operationPlan.InstanceID!, operationPlan.StationSchemeID!);
                if (!OperationPlanExists(dbConnector, operationPlan.InstanceID!, operationPlan.StationSchemeID!, originalOperationPlanID!))
                {
                    return NotFound("Operation plan not found.");
                }

                if (!string.Equals(originalOperationPlanID, operationPlan.OperationPlanID, StringComparison.OrdinalIgnoreCase) &&
                    OperationPlanExists(dbConnector, operationPlan.InstanceID!, operationPlan.StationSchemeID!, operationPlan.OperationPlanID!))
                {
                    return BadRequest("Operation plan ID already exists in the selected station scheme.");
                }

                operationPlan.UpdatedDate = DateTime.Now;
                dbConnector.BeginTransaction();
                UpdateOperationPlan(dbConnector, operationPlan, originalOperationPlanID!);
                if (!string.Equals(originalOperationPlanID, operationPlan.OperationPlanID, StringComparison.OrdinalIgnoreCase))
                {
                    UpdateOperationPlanIDInScopedTables(
                        dbConnector,
                        operationPlan.InstanceID!,
                        operationPlan.StationSchemeID!,
                        originalOperationPlanID!,
                        operationPlan.OperationPlanID!);
                }

                dbConnector.Commit();
                return Ok(operationPlan);
            }
            catch (Exception ex)
            {
                dbConnector?.Rollback();
                _logger.LogError(ex, "Failed to update operation plan.");
                return StatusCode(500, "Failed to update operation plan.");
            }
        }

        [HttpPost(Name = "CopyOperationPlan")]
        public IActionResult CopyOperationPlan([FromBody] OperationPlanCopyRequest? request)
        {
            DBConnector? dbConnector = null;
            try
            {
                dbConnector = GetCapacityDbConnector();
                var sourceScope = NormalizeOperationPlanScope(
                    request?.InstanceID,
                    request?.StationSchemeID,
                    request?.SourceOperationPlanID,
                    requireOperationPlanID: true);
                if (sourceScope.ErrorResult != null)
                {
                    return sourceScope.ErrorResult;
                }

                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, sourceScope.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureTrainOperationPlanSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, sourceScope.InstanceID!, sourceScope.StationSchemeID!);
                var sourcePlan = LoadOperationPlan(
                    dbConnector,
                    sourceScope.InstanceID!,
                    sourceScope.StationSchemeID!,
                    sourceScope.OperationPlanID!);
                if (sourcePlan == null)
                {
                    return NotFound("Operation plan not found.");
                }

                var targetOperationPlanID = TrimToMaxLength(request?.OperationPlanID ?? string.Empty, 50);
                if (string.IsNullOrWhiteSpace(targetOperationPlanID))
                {
                    targetOperationPlanID = GenerateOperationPlanID(dbConnector, sourceScope.InstanceID!, sourceScope.StationSchemeID!);
                }

                if (OperationPlanExists(dbConnector, sourceScope.InstanceID!, sourceScope.StationSchemeID!, targetOperationPlanID))
                {
                    return BadRequest("Operation plan ID already exists in the selected station scheme.");
                }

                var sourceName = string.IsNullOrWhiteSpace(sourcePlan.Name)
                    ? sourcePlan.OperationPlanID ?? "作业计划"
                    : sourcePlan.Name;
                var now = DateTime.Now;
                var copiedPlan = new OperationPlanRow
                {
                    InstanceID = sourceScope.InstanceID,
                    StationSchemeID = sourceScope.StationSchemeID,
                    OperationPlanID = targetOperationPlanID,
                    Name = TrimToMaxLength(
                        string.IsNullOrWhiteSpace(request?.Name)
                            ? $"{sourceName} 副本"
                            : request.Name,
                        100),
                    Description = TrimToMaxLength(
                        string.IsNullOrWhiteSpace(request?.Description)
                            ? sourcePlan.Description ?? string.Empty
                            : request.Description,
                        500),
                    SortOrder = request?.SortOrder ?? GetNextOperationPlanSortOrder(dbConnector, sourceScope.InstanceID!, sourceScope.StationSchemeID!),
                    CreatedDate = now,
                    UpdatedDate = now
                };

                dbConnector.BeginTransaction();
                InsertOperationPlan(dbConnector, copiedPlan);
                CopyOperationPlanScopedData(
                    dbConnector,
                    sourceScope.InstanceID!,
                    sourceScope.StationSchemeID!,
                    sourceScope.OperationPlanID!,
                    copiedPlan.OperationPlanID!);
                dbConnector.Commit();
                return Ok(copiedPlan);
            }
            catch (Exception ex)
            {
                dbConnector?.Rollback();
                _logger.LogError(ex, "Failed to copy operation plan.");
                return StatusCode(500, "Failed to copy operation plan.");
            }
        }

        [HttpDelete(Name = "DeleteOperationPlan")]
        public IActionResult DeleteOperationPlan(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null,
            [FromQuery] string? operationPlanID = null)
        {
            DBConnector? dbConnector = null;
            try
            {
                var scope = NormalizeOperationPlanScope(instanceID, stationSchemeID, operationPlanID, requireOperationPlanID: true);
                if (scope.ErrorResult != null)
                {
                    return scope.ErrorResult;
                }

                if (string.Equals(scope.OperationPlanID, DefaultOperationPlanID, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("The default operation plan cannot be deleted.");
                }

                dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, scope.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureTrainOperationPlanSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, scope.InstanceID!, scope.StationSchemeID!);
                if (!OperationPlanExists(dbConnector, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!))
                {
                    return NotFound("Operation plan not found.");
                }

                dbConnector.BeginTransaction();
                DeleteOperationPlanScopedData(dbConnector, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!);
                dbConnector.ExecuteNonQuery(
                    $@"DELETE FROM {QuoteIdentifier("operationplan")}
                       WHERE InstanceID = @instanceID
                         AND StationSchemeID = @stationSchemeID
                         AND OperationPlanID = @operationPlanID",
                    new
                    {
                        instanceID = scope.InstanceID,
                        stationSchemeID = scope.StationSchemeID,
                        operationPlanID = scope.OperationPlanID
                    });
                dbConnector.Commit();
                return Ok("Operation plan deleted successfully.");
            }
            catch (Exception ex)
            {
                dbConnector?.Rollback();
                _logger.LogError(ex, "Failed to delete operation plan.");
                return StatusCode(500, "Failed to delete operation plan.");
            }
        }

        [HttpGet(Name = "GetTrainTemplates")]
        public IActionResult GetTrainTemplates(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null,
            [FromQuery] string? operationPlanID = null)
        {
            try
            {
                var scope = NormalizeOperationPlanScope(instanceID, stationSchemeID, operationPlanID);
                if (scope.ErrorResult != null)
                {
                    return scope.ErrorResult;
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, scope.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationPlanTemplateSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, scope.InstanceID!, scope.StationSchemeID!);
                return Ok(LoadTrainTemplates(dbConnector, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load train templates.");
                return StatusCode(500, "Failed to load train templates.");
            }
        }

        [HttpPost(Name = "CreateTrainTemplate")]
        public IActionResult CreateTrainTemplate([FromBody] TrainTemplateRequest? request)
        {
            try
            {
                var dbConnector = GetCapacityDbConnector();
                var normalized = NormalizeTrainTemplateRequest(request, allowMissingID: true);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                var template = normalized.Template!;
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, template.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationPlanTemplateSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, template.InstanceID!, template.StationSchemeID!);
                if (string.IsNullOrWhiteSpace(template.TrainTemplateID))
                {
                    template.TrainTemplateID = GenerateTrainTemplateID(dbConnector, template.InstanceID!, template.StationSchemeID!, template.OperationPlanID!);
                }

                if (TrainTemplateExists(dbConnector, template.InstanceID!, template.StationSchemeID!, template.OperationPlanID!, template.TrainTemplateID!))
                {
                    return BadRequest("Train template ID already exists in the selected station scheme.");
                }

                var tableName = QuoteIdentifier("traintemplate");
                var result = dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {tableName} (
                           InstanceID, StationSchemeID, OperationPlanID, TrainTemplateID, Name, {QuoteIdentifier("Type")}, {QuoteIdentifier("Number")}, IsFixedOperation)
                       VALUES (
                           @InstanceID, @StationSchemeID, @OperationPlanID, @TrainTemplateID, @Name, @Type, @Number, @IsFixedOperation)",
                    template);
                if (result <= 0)
                {
                    return StatusCode(500, "Failed to create train template.");
                }

                return Ok(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create train template.");
                return StatusCode(500, "Failed to create train template.");
            }
        }

        [HttpPut(Name = "EditTrainTemplate")]
        public IActionResult EditTrainTemplate([FromBody] TrainTemplateRequest? request)
        {
            DBConnector? dbConnector = null;
            try
            {
                dbConnector = GetCapacityDbConnector();
                var normalized = NormalizeTrainTemplateRequest(request, allowMissingID: false);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                var template = normalized.Template!;
                var originalTrainTemplateID = request?.OriginalTrainTemplateID?.Trim();
                if (string.IsNullOrWhiteSpace(originalTrainTemplateID))
                {
                    originalTrainTemplateID = template.TrainTemplateID;
                }

                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, template.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationPlanTemplateSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, template.InstanceID!, template.StationSchemeID!);
                if (!TrainTemplateExists(dbConnector, template.InstanceID!, template.StationSchemeID!, template.OperationPlanID!, originalTrainTemplateID!))
                {
                    return NotFound("Train template not found.");
                }

                if (!string.Equals(originalTrainTemplateID, template.TrainTemplateID, StringComparison.OrdinalIgnoreCase) &&
                    TrainTemplateExists(dbConnector, template.InstanceID!, template.StationSchemeID!, template.OperationPlanID!, template.TrainTemplateID!))
                {
                    return BadRequest("Train template ID already exists in the selected station scheme.");
                }

                dbConnector.BeginTransaction();
                var trainTableName = QuoteIdentifier("traintemplate");
                var movementTableName = QuoteIdentifier("movementtemplate");
                dbConnector.ExecuteNonQuery(
                    $@"UPDATE {trainTableName}
                       SET TrainTemplateID = @TrainTemplateID,
                           Name = @Name,
                           {QuoteIdentifier("Type")} = @Type,
                           {QuoteIdentifier("Number")} = @Number,
                           IsFixedOperation = @IsFixedOperation
                        WHERE InstanceID = @InstanceID
                          AND StationSchemeID = @StationSchemeID
                          AND OperationPlanID = @OperationPlanID
                          AND TrainTemplateID = @OriginalTrainTemplateID",
                    new
                    {
                        template.InstanceID,
                        template.StationSchemeID,
                        template.OperationPlanID,
                        template.TrainTemplateID,
                        template.Name,
                        template.Type,
                        template.Number,
                        template.IsFixedOperation,
                        OriginalTrainTemplateID = originalTrainTemplateID
                    });

                if (!string.Equals(originalTrainTemplateID, template.TrainTemplateID, StringComparison.OrdinalIgnoreCase))
                {
                    dbConnector.ExecuteNonQuery(
                        $@"UPDATE {movementTableName}
                            SET TrainTemplateID = @TrainTemplateID
                            WHERE InstanceID = @InstanceID
                              AND StationSchemeID = @StationSchemeID
                              AND OperationPlanID = @OperationPlanID
                              AND TrainTemplateID = @OriginalTrainTemplateID",
                        new
                        {
                            template.InstanceID,
                            template.StationSchemeID,
                            template.OperationPlanID,
                            template.TrainTemplateID,
                            OriginalTrainTemplateID = originalTrainTemplateID
                        });
                }

                dbConnector.Commit();
                return Ok(template);
            }
            catch (Exception ex)
            {
                dbConnector?.Rollback();
                _logger.LogError(ex, "Failed to update train template.");
                return StatusCode(500, "Failed to update train template.");
            }
        }

        [HttpDelete(Name = "DeleteTrainTemplate")]
        public IActionResult DeleteTrainTemplate(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null,
            [FromQuery] string? operationPlanID = null,
            [FromQuery] string? trainTemplateID = null)
        {
            DBConnector? dbConnector = null;
            try
            {
                var scope = NormalizeOperationPlanScope(instanceID, stationSchemeID, operationPlanID);
                if (scope.ErrorResult != null)
                {
                    return scope.ErrorResult;
                }

                var normalizedTrainTemplateID = trainTemplateID?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedTrainTemplateID))
                {
                    return BadRequest("trainTemplateID is required.");
                }

                dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, scope.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationPlanTemplateSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, scope.InstanceID!, scope.StationSchemeID!);
                if (!TrainTemplateExists(dbConnector, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!, normalizedTrainTemplateID))
                {
                    return NotFound("Train template not found.");
                }

                dbConnector.BeginTransaction();
                DeleteMovementTemplatesForTrain(dbConnector, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!, normalizedTrainTemplateID);
                dbConnector.ExecuteNonQuery(
                    $@"DELETE FROM {QuoteIdentifier("traintemplate")}
                       WHERE InstanceID = @instanceID
                         AND StationSchemeID = @stationSchemeID
                         AND OperationPlanID = @operationPlanID
                         AND TrainTemplateID = @normalizedTrainTemplateID",
                    new
                    {
                        instanceID = scope.InstanceID,
                        stationSchemeID = scope.StationSchemeID,
                        operationPlanID = scope.OperationPlanID,
                        normalizedTrainTemplateID
                    });
                dbConnector.Commit();

                return Ok("Train template deleted successfully.");
            }
            catch (Exception ex)
            {
                dbConnector?.Rollback();
                _logger.LogError(ex, "Failed to delete train template.");
                return StatusCode(500, "Failed to delete train template.");
            }
        }

        [HttpGet(Name = "GetMovementTemplates")]
        public IActionResult GetMovementTemplates(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null,
            [FromQuery] string? operationPlanID = null,
            [FromQuery] string? trainTemplateID = null)
        {
            try
            {
                var scope = NormalizeOperationPlanScope(instanceID, stationSchemeID, operationPlanID);
                if (scope.ErrorResult != null)
                {
                    return scope.ErrorResult;
                }

                var normalizedTrainTemplateID = trainTemplateID?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedTrainTemplateID))
                {
                    return BadRequest("trainTemplateID is required.");
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, scope.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationPlanTemplateSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, scope.InstanceID!, scope.StationSchemeID!);
                if (!TrainTemplateExists(dbConnector, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!, normalizedTrainTemplateID))
                {
                    return Ok(new List<MovementTemplateRow>());
                }

                return Ok(LoadMovementTemplates(
                    dbConnector,
                    scope.InstanceID!,
                    scope.StationSchemeID!,
                    scope.OperationPlanID!,
                    normalizedTrainTemplateID));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load movement templates.");
                return StatusCode(500, "Failed to load movement templates.");
            }
        }

        [HttpPost(Name = "CreateMovementTemplate")]
        public IActionResult CreateMovementTemplate([FromBody] MovementTemplateRequest? request)
        {
            try
            {
                var dbConnector = GetCapacityDbConnector();
                var normalized = NormalizeMovementTemplateRequest(request, allowMissingID: true);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                var movement = normalized.Movement!;
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, movement.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationPlanTemplateSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, movement.InstanceID!, movement.StationSchemeID!);
                if (!TrainTemplateExists(dbConnector, movement.InstanceID!, movement.StationSchemeID!, movement.OperationPlanID!, movement.TrainTemplateID!))
                {
                    return NotFound("Train template not found.");
                }

                if (string.IsNullOrWhiteSpace(movement.MovementID))
                {
                    movement.MovementID = GenerateMovementID(
                        dbConnector,
                        movement.InstanceID!,
                        movement.StationSchemeID!,
                        movement.OperationPlanID!,
                        movement.TrainTemplateID!);
                }

                if (MovementTemplateExists(
                    dbConnector,
                    movement.InstanceID!,
                    movement.StationSchemeID!,
                    movement.OperationPlanID!,
                    movement.TrainTemplateID!,
                    movement.MovementID!))
                {
                    return BadRequest("Movement template ID already exists under the selected train template.");
                }

                movement.SortOrder ??= GetNextMovementTemplateSortOrder(
                    dbConnector,
                    movement.InstanceID!,
                    movement.StationSchemeID!,
                    movement.OperationPlanID!,
                    movement.TrainTemplateID!);
                var tableName = QuoteIdentifier("movementtemplate");
                var result = dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {tableName} (
                           InstanceID, StationSchemeID, OperationPlanID, TrainTemplateID, MovementID, Name, RouteIDList, MinDuration, SortOrder)
                       VALUES (
                           @InstanceID, @StationSchemeID, @OperationPlanID, @TrainTemplateID, @MovementID, @Name, @RouteIDList, @MinDuration, @SortOrder)",
                    movement);
                if (result <= 0)
                {
                    return StatusCode(500, "Failed to create movement template.");
                }

                return Ok(movement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create movement template.");
                return StatusCode(500, "Failed to create movement template.");
            }
        }

        [HttpPut(Name = "EditMovementTemplate")]
        public IActionResult EditMovementTemplate([FromBody] MovementTemplateRequest? request)
        {
            try
            {
                var dbConnector = GetCapacityDbConnector();
                var normalized = NormalizeMovementTemplateRequest(request, allowMissingID: false);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                var movement = normalized.Movement!;
                var originalMovementID = request?.OriginalMovementID?.Trim();
                if (string.IsNullOrWhiteSpace(originalMovementID))
                {
                    originalMovementID = movement.MovementID;
                }

                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, movement.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationPlanTemplateSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, movement.InstanceID!, movement.StationSchemeID!);
                if (!TrainTemplateExists(dbConnector, movement.InstanceID!, movement.StationSchemeID!, movement.OperationPlanID!, movement.TrainTemplateID!))
                {
                    return NotFound("Train template not found.");
                }

                if (!MovementTemplateExists(
                    dbConnector,
                    movement.InstanceID!,
                    movement.StationSchemeID!,
                    movement.OperationPlanID!,
                    movement.TrainTemplateID!,
                    originalMovementID!))
                {
                    return NotFound("Movement template not found.");
                }

                if (!string.Equals(originalMovementID, movement.MovementID, StringComparison.OrdinalIgnoreCase) &&
                    MovementTemplateExists(
                        dbConnector,
                        movement.InstanceID!,
                        movement.StationSchemeID!,
                        movement.OperationPlanID!,
                        movement.TrainTemplateID!,
                        movement.MovementID!))
                {
                    return BadRequest("Movement template ID already exists under the selected train template.");
                }

                var tableName = QuoteIdentifier("movementtemplate");
                dbConnector.ExecuteNonQuery(
                    $@"UPDATE {tableName}
                       SET MovementID = @MovementID,
                           Name = @Name,
                           RouteIDList = @RouteIDList,
                           MinDuration = @MinDuration,
                           SortOrder = @SortOrder
                        WHERE InstanceID = @InstanceID
                          AND StationSchemeID = @StationSchemeID
                          AND OperationPlanID = @OperationPlanID
                          AND TrainTemplateID = @TrainTemplateID
                          AND MovementID = @OriginalMovementID",
                    new
                    {
                        movement.InstanceID,
                        movement.StationSchemeID,
                        movement.OperationPlanID,
                        movement.TrainTemplateID,
                        movement.MovementID,
                        movement.Name,
                        movement.RouteIDList,
                        movement.MinDuration,
                        movement.SortOrder,
                        OriginalMovementID = originalMovementID
                    });

                return Ok(movement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update movement template.");
                return StatusCode(500, "Failed to update movement template.");
            }
        }

        [HttpPut(Name = "UpdateMovementTemplateOrder")]
        public IActionResult UpdateMovementTemplateOrder([FromBody] MovementTemplateOrderRequest? request)
        {
            try
            {
                var scope = NormalizeOperationPlanScope(request?.InstanceID, request?.StationSchemeID, request?.OperationPlanID);
                if (scope.ErrorResult != null)
                {
                    return scope.ErrorResult;
                }

                var trainTemplateID = request?.TrainTemplateID?.Trim();
                if (string.IsNullOrWhiteSpace(trainTemplateID))
                {
                    return BadRequest("trainTemplateID is required.");
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, scope.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationPlanTemplateSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, scope.InstanceID!, scope.StationSchemeID!);
                if (!TrainTemplateExists(dbConnector, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!, trainTemplateID))
                {
                    return NotFound("Train template not found.");
                }

                var items = NormalizeMovementOrderItems(request?.Items);
                UpdateMovementTemplateSortOrders(
                    dbConnector,
                    scope.InstanceID!,
                    scope.StationSchemeID!,
                    scope.OperationPlanID!,
                    trainTemplateID,
                    items);
                return Ok(LoadMovementTemplates(
                    dbConnector,
                    scope.InstanceID!,
                    scope.StationSchemeID!,
                    scope.OperationPlanID!,
                    trainTemplateID));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update movement template order.");
                return StatusCode(500, "Failed to update movement template order.");
            }
        }

        [HttpDelete(Name = "DeleteMovementTemplate")]
        public IActionResult DeleteMovementTemplate(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null,
            [FromQuery] string? operationPlanID = null,
            [FromQuery] string? trainTemplateID = null,
            [FromQuery] string? movementID = null)
        {
            try
            {
                var scope = NormalizeOperationPlanScope(instanceID, stationSchemeID, operationPlanID);
                if (scope.ErrorResult != null)
                {
                    return scope.ErrorResult;
                }

                var normalizedTrainTemplateID = trainTemplateID?.Trim();
                var normalizedMovementID = movementID?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedTrainTemplateID) ||
                    string.IsNullOrWhiteSpace(normalizedMovementID))
                {
                    return BadRequest("trainTemplateID and movementID are required.");
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, scope.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationPlanTemplateSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, scope.InstanceID!, scope.StationSchemeID!);
                if (!MovementTemplateExists(
                    dbConnector,
                    scope.InstanceID!,
                    scope.StationSchemeID!,
                    scope.OperationPlanID!,
                    normalizedTrainTemplateID,
                    normalizedMovementID))
                {
                    return NotFound("Movement template not found.");
                }

                dbConnector.ExecuteNonQuery(
                    $@"DELETE FROM {QuoteIdentifier("movementtemplate")}
                       WHERE InstanceID = @instanceID
                         AND StationSchemeID = @stationSchemeID
                         AND OperationPlanID = @operationPlanID
                         AND TrainTemplateID = @normalizedTrainTemplateID
                         AND MovementID = @normalizedMovementID",
                    new
                    {
                        instanceID = scope.InstanceID,
                        stationSchemeID = scope.StationSchemeID,
                        operationPlanID = scope.OperationPlanID,
                        normalizedTrainTemplateID,
                        normalizedMovementID
                    });

                return Ok("Movement template deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete movement template.");
                return StatusCode(500, "Failed to delete movement template.");
            }
        }

        [HttpGet(Name = "GetTrainOperationPlan")]
        public IActionResult GetTrainOperationPlan(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null,
            [FromQuery] string? operationPlanID = null)
        {
            try
            {
                var scope = NormalizeOperationPlanScope(instanceID, stationSchemeID, operationPlanID);
                if (scope.ErrorResult != null)
                {
                    return scope.ErrorResult;
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, scope.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureTrainOperationPlanSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, scope.InstanceID!, scope.StationSchemeID!);
                return Ok(LoadTrainOperationPlan(dbConnector, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load train operation plan.");
                return StatusCode(500, "Failed to load train operation plan.");
            }
        }

        [HttpPost(Name = "GenerateTrainOperationPlan")]
        public IActionResult GenerateTrainOperationPlan([FromBody] GenerateTrainOperationPlanRequest? request)
        {
            DBConnector? dbConnector = null;
            try
            {
                var normalized = NormalizeTrainOperationPlanRequest(request);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalized.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureTrainOperationPlanSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, normalized.InstanceID!, normalized.StationSchemeID!);
                var generatedPlan = BuildGeneratedTrainOperationPlan(
                    dbConnector,
                    normalized.InstanceID!,
                    normalized.StationSchemeID!,
                    normalized.OperationPlanID!,
                    normalized.StartMinutes,
                    normalized.EndMinutes);

                dbConnector.BeginTransaction();
                DeleteTrainOperationPlan(dbConnector, normalized.InstanceID!, normalized.StationSchemeID!, normalized.OperationPlanID!);
                InsertTrainOperationPlan(dbConnector, generatedPlan);
                dbConnector.Commit();

                return Ok(generatedPlan);
            }
            catch (Exception ex)
            {
                dbConnector?.Rollback();
                _logger.LogError(ex, "Failed to generate train operation plan.");
                return StatusCode(500, "Failed to generate train operation plan.");
            }
        }

        [HttpPost(Name = "CreateTrain")]
        public IActionResult CreateTrain([FromBody] TrainRow? request)
        {
            try
            {
                var dbConnector = GetCapacityDbConnector();
                var normalized = NormalizeTrainRowRequest(request, allowMissingID: true);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                var train = normalized.Train!;
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, train.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureTrainOperationPlanSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, train.InstanceID!, train.StationSchemeID!);
                if (string.IsNullOrWhiteSpace(train.ID))
                {
                    train.ID = GenerateOperationTrainID(dbConnector, train.InstanceID!, train.StationSchemeID!, train.OperationPlanID!);
                }
                if (string.IsNullOrWhiteSpace(train.TrainNumber))
                {
                    train.TrainNumber = GenerateOperationTrainNumber(dbConnector, train.InstanceID!, train.StationSchemeID!, train.OperationPlanID!);
                }

                if (OperationTrainExists(dbConnector, train.InstanceID!, train.StationSchemeID!, train.OperationPlanID!, train.ID!))
                {
                    return BadRequest("Train ID already exists in the selected station scheme.");
                }

                InsertTrain(dbConnector, train);
                return Ok(train);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create train.");
                return StatusCode(500, "Failed to create train.");
            }
        }

        [HttpPut(Name = "EditTrain")]
        public IActionResult EditTrain([FromBody] TrainRow? request)
        {
            try
            {
                var dbConnector = GetCapacityDbConnector();
                var normalized = NormalizeTrainRowRequest(request, allowMissingID: false);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                var train = normalized.Train!;
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, train.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureTrainOperationPlanSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, train.InstanceID!, train.StationSchemeID!);
                if (!OperationTrainExists(dbConnector, train.InstanceID!, train.StationSchemeID!, train.OperationPlanID!, train.ID!))
                {
                    return NotFound("Train not found.");
                }

                UpdateTrain(dbConnector, train);
                return Ok(train);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update train.");
                return StatusCode(500, "Failed to update train.");
            }
        }

        [HttpDelete(Name = "DeleteTrain")]
        public IActionResult DeleteTrain(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null,
            [FromQuery] string? operationPlanID = null,
            [FromQuery] string? id = null)
        {
            DBConnector? dbConnector = null;
            try
            {
                var scope = NormalizeOperationPlanScope(instanceID, stationSchemeID, operationPlanID);
                if (scope.ErrorResult != null)
                {
                    return scope.ErrorResult;
                }

                var normalizedID = id?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedID))
                {
                    return BadRequest("id is required.");
                }

                dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, scope.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureTrainOperationPlanSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, scope.InstanceID!, scope.StationSchemeID!);
                if (!OperationTrainExists(dbConnector, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!, normalizedID))
                {
                    return NotFound("Train not found.");
                }

                dbConnector.BeginTransaction();
                dbConnector.ExecuteNonQuery(
                    $@"DELETE FROM {QuoteIdentifier("movement")}
                       WHERE InstanceID = @instanceID
                         AND StationSchemeID = @stationSchemeID
                         AND OperationPlanID = @operationPlanID
                         AND TrainID = @normalizedID",
                    new
                    {
                        instanceID = scope.InstanceID,
                        stationSchemeID = scope.StationSchemeID,
                        operationPlanID = scope.OperationPlanID,
                        normalizedID
                    });
                dbConnector.ExecuteNonQuery(
                    $@"DELETE FROM {QuoteIdentifier("train")}
                       WHERE InstanceID = @instanceID
                         AND StationSchemeID = @stationSchemeID
                         AND OperationPlanID = @operationPlanID
                         AND {QuoteIdentifier("ID")} = @normalizedID",
                    new
                    {
                        instanceID = scope.InstanceID,
                        stationSchemeID = scope.StationSchemeID,
                        operationPlanID = scope.OperationPlanID,
                        normalizedID
                    });
                dbConnector.Commit();
                return Ok("Train deleted successfully.");
            }
            catch (Exception ex)
            {
                dbConnector?.Rollback();
                _logger.LogError(ex, "Failed to delete train.");
                return StatusCode(500, "Failed to delete train.");
            }
        }

        [HttpPost(Name = "CreateMovement")]
        public IActionResult CreateMovement([FromBody] MovementRow? request)
        {
            try
            {
                var dbConnector = GetCapacityDbConnector();
                var normalized = NormalizeMovementRowRequest(request, allowMissingMovementID: true);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                var movement = normalized.Movement!;
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, movement.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureTrainOperationPlanSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, movement.InstanceID!, movement.StationSchemeID!);
                if (!OperationTrainExists(dbConnector, movement.InstanceID!, movement.StationSchemeID!, movement.OperationPlanID!, movement.TrainID!))
                {
                    return NotFound("Train not found.");
                }

                if (string.IsNullOrWhiteSpace(movement.MovementID))
                {
                    movement.MovementID = GenerateOperationMovementID(
                        dbConnector,
                        movement.InstanceID!,
                        movement.StationSchemeID!,
                        movement.OperationPlanID!,
                        movement.TrainID!);
                }

                if (OperationMovementExists(dbConnector, movement.InstanceID!, movement.StationSchemeID!, movement.OperationPlanID!, movement.TrainID!, movement.MovementID!))
                {
                    return BadRequest("Movement ID already exists under the selected train.");
                }

                movement.SortOrder ??= GetNextMovementSortOrder(
                    dbConnector,
                    movement.InstanceID!,
                    movement.StationSchemeID!,
                    movement.OperationPlanID!,
                    movement.TrainID!);
                InsertMovement(dbConnector, movement);
                return Ok(movement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create movement.");
                return StatusCode(500, "Failed to create movement.");
            }
        }

        [HttpPut(Name = "EditMovement")]
        public IActionResult EditMovement([FromBody] MovementRow? request)
        {
            try
            {
                var dbConnector = GetCapacityDbConnector();
                var normalized = NormalizeMovementRowRequest(request, allowMissingMovementID: false);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                var movement = normalized.Movement!;
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, movement.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureTrainOperationPlanSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, movement.InstanceID!, movement.StationSchemeID!);
                if (!OperationMovementExists(dbConnector, movement.InstanceID!, movement.StationSchemeID!, movement.OperationPlanID!, movement.TrainID!, movement.MovementID!))
                {
                    return NotFound("Movement not found.");
                }

                UpdateMovement(dbConnector, movement);
                return Ok(movement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update movement.");
                return StatusCode(500, "Failed to update movement.");
            }
        }

        [HttpPut(Name = "UpdateMovementOrder")]
        public IActionResult UpdateMovementOrder([FromBody] MovementOrderRequest? request)
        {
            try
            {
                var scope = NormalizeOperationPlanScope(request?.InstanceID, request?.StationSchemeID, request?.OperationPlanID);
                if (scope.ErrorResult != null)
                {
                    return scope.ErrorResult;
                }

                var trainID = request?.TrainID?.Trim();
                if (string.IsNullOrWhiteSpace(trainID))
                {
                    return BadRequest("trainID is required.");
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, scope.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureTrainOperationPlanSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, scope.InstanceID!, scope.StationSchemeID!);
                if (!OperationTrainExists(dbConnector, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!, trainID))
                {
                    return NotFound("Train not found.");
                }

                var items = NormalizeMovementOrderItems(request?.Items);
                UpdateMovementSortOrders(
                    dbConnector,
                    scope.InstanceID!,
                    scope.StationSchemeID!,
                    scope.OperationPlanID!,
                    trainID,
                    items);
                return Ok(LoadTrainOperationPlan(dbConnector, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update movement order.");
                return StatusCode(500, "Failed to update movement order.");
            }
        }

        [HttpDelete(Name = "DeleteMovement")]
        public IActionResult DeleteMovement(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null,
            [FromQuery] string? operationPlanID = null,
            [FromQuery] string? trainID = null,
            [FromQuery] string? movementID = null)
        {
            try
            {
                var scope = NormalizeOperationPlanScope(instanceID, stationSchemeID, operationPlanID);
                if (scope.ErrorResult != null)
                {
                    return scope.ErrorResult;
                }

                var normalizedTrainID = trainID?.Trim();
                var normalizedMovementID = movementID?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedTrainID) ||
                    string.IsNullOrWhiteSpace(normalizedMovementID))
                {
                    return BadRequest("trainID and movementID are required.");
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, scope.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureTrainOperationPlanSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, scope.InstanceID!, scope.StationSchemeID!);
                if (!OperationMovementExists(dbConnector, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!, normalizedTrainID, normalizedMovementID))
                {
                    return NotFound("Movement not found.");
                }

                dbConnector.ExecuteNonQuery(
                    $@"DELETE FROM {QuoteIdentifier("movement")}
                       WHERE InstanceID = @instanceID
                         AND StationSchemeID = @stationSchemeID
                         AND OperationPlanID = @operationPlanID
                         AND TrainID = @normalizedTrainID
                         AND MovementID = @normalizedMovementID",
                    new
                    {
                        instanceID = scope.InstanceID,
                        stationSchemeID = scope.StationSchemeID,
                        operationPlanID = scope.OperationPlanID,
                        normalizedTrainID,
                        normalizedMovementID
                    });
                return Ok("Movement deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete movement.");
                return StatusCode(500, "Failed to delete movement.");
            }
        }

        [HttpGet(Name = "GetBottleneckSummaryCategories")]
        public IActionResult GetBottleneckSummaryCategories(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null,
            [FromQuery] string? operationPlanID = null)
        {
            try
            {
                var scope = NormalizeOperationPlanScope(instanceID, stationSchemeID, operationPlanID);
                if (scope.ErrorResult != null)
                {
                    return scope.ErrorResult;
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, scope.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationBottleneckSummaryCategorySchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, scope.InstanceID!, scope.StationSchemeID!);
                return Ok(LoadOperationBottleneckSummaryCategories(dbConnector, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load operation bottleneck summary categories.");
                return StatusCode(500, "Failed to load operation bottleneck summary categories.");
            }
        }

        [HttpPut(Name = "SaveBottleneckSummaryCategories")]
        public IActionResult SaveBottleneckSummaryCategories([FromBody] OperationBottleneckSummaryCategorySaveRequest? request)
        {
            DBConnector? dbConnector = null;
            try
            {
                var normalized = NormalizeOperationBottleneckSummaryCategorySaveRequest(request);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalized.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationBottleneckSummaryCategorySchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, normalized.InstanceID!, normalized.StationSchemeID!);
                dbConnector.BeginTransaction();
                DeleteOperationBottleneckSummaryCategories(dbConnector, normalized.InstanceID!, normalized.StationSchemeID!, normalized.OperationPlanID!);
                InsertOperationBottleneckSummaryCategories(dbConnector, normalized.Categories);
                dbConnector.Commit();

                return Ok(LoadOperationBottleneckSummaryCategories(dbConnector, normalized.InstanceID!, normalized.StationSchemeID!, normalized.OperationPlanID!));
            }
            catch (Exception ex)
            {
                dbConnector?.Rollback();
                _logger.LogError(ex, "Failed to save operation bottleneck summary categories.");
                return StatusCode(500, "Failed to save operation bottleneck summary categories.");
            }
        }

        [HttpGet(Name = "GetOperationAnalysisResult")]
        public IActionResult GetOperationAnalysisResult(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null,
            [FromQuery] string? operationPlanID = null)
        {
            try
            {
                var scope = NormalizeOperationPlanScope(instanceID, stationSchemeID, operationPlanID);
                if (scope.ErrorResult != null)
                {
                    return scope.ErrorResult;
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, scope.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationAnalysisResultSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, scope.InstanceID!, scope.StationSchemeID!);
                return Ok(LoadOperationAnalysisResult(dbConnector, scope.InstanceID!, scope.StationSchemeID!, scope.OperationPlanID!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load operation analysis result.");
                return StatusCode(500, "Failed to load operation analysis result.");
            }
        }

        [HttpPut(Name = "SaveOperationAnalysisResult")]
        public IActionResult SaveOperationAnalysisResult([FromBody] OperationAnalysisResultSaveRequest? request)
        {
            DBConnector? dbConnector = null;
            try
            {
                var normalized = NormalizeOperationAnalysisResultSaveRequest(request);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalized.Snapshot!.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationAnalysisResultSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, normalized.Snapshot.InstanceID!, normalized.Snapshot.StationSchemeID!);
                dbConnector.BeginTransaction();
                DeleteOperationAnalysisResult(dbConnector, normalized.Snapshot.InstanceID!, normalized.Snapshot.StationSchemeID!, normalized.Snapshot.OperationPlanID!);
                InsertOperationAnalysisResult(dbConnector, normalized.Snapshot);
                dbConnector.Commit();

                return Ok(LoadOperationAnalysisResult(
                    dbConnector,
                    normalized.Snapshot.InstanceID!,
                    normalized.Snapshot.StationSchemeID!,
                    normalized.Snapshot.OperationPlanID!));
            }
            catch (Exception ex)
            {
                dbConnector?.Rollback();
                _logger.LogError(ex, "Failed to save operation analysis result.");
                return StatusCode(500, "Failed to save operation analysis result.");
            }
        }

        [HttpGet(Name = "GetOperationOccupationTimeSubTables")]
        public IActionResult GetOperationOccupationTimeSubTables(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null,
            [FromQuery] string? operationPlanID = null)
        {
            try
            {
                var scope = NormalizeOperationPlanScope(instanceID, stationSchemeID, operationPlanID);
                if (scope.ErrorResult != null)
                {
                    return scope.ErrorResult;
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, scope.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationAnalysisResultSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, scope.InstanceID!, scope.StationSchemeID!);
                return Ok(LoadOperationOccupationTimeSubTableSettings(
                    dbConnector,
                    scope.InstanceID!,
                    scope.StationSchemeID!,
                    scope.OperationPlanID!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load operation occupation time sub table settings.");
                return StatusCode(500, "Failed to load operation occupation time sub table settings.");
            }
        }

        [HttpPut(Name = "SaveOperationOccupationTimeSubTables")]
        public IActionResult SaveOperationOccupationTimeSubTables([FromBody] OperationOccupationTimeSubTableSaveRequest? request)
        {
            DBConnector? dbConnector = null;
            try
            {
                var normalized = NormalizeOperationOccupationTimeSubTableSaveRequest(request);
                if (normalized.ErrorResult != null)
                {
                    return normalized.ErrorResult;
                }

                dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalized.InstanceID!);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationAnalysisResultSchema(dbConnector);
                EnsureDefaultOperationPlan(dbConnector, normalized.InstanceID!, normalized.StationSchemeID!);
                dbConnector.BeginTransaction();
                DeleteOperationOccupationTimeSubTableSettings(
                    dbConnector,
                    normalized.InstanceID!,
                    normalized.StationSchemeID!,
                    normalized.OperationPlanID!);
                InsertOperationOccupationTimeSubTableSettings(dbConnector, normalized.SubTables!);
                dbConnector.Commit();

                return Ok(LoadOperationOccupationTimeSubTableSettings(
                    dbConnector,
                    normalized.InstanceID!,
                    normalized.StationSchemeID!,
                    normalized.OperationPlanID!));
            }
            catch (Exception ex)
            {
                dbConnector?.Rollback();
                _logger.LogError(ex, "Failed to save operation occupation time sub table settings.");
                return StatusCode(500, "Failed to save operation occupation time sub table settings.");
            }
        }

        private DBConnector GetCapacityDbConnector()
        {
            return DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
        }

        private (string? InstanceID, string? StationSchemeID, IActionResult? ErrorResult) NormalizeScope(
            string? instanceID,
            string? stationSchemeID)
        {
            var normalizedInstanceID = instanceID?.Trim();
            var normalizedStationSchemeID = stationSchemeID?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedInstanceID) ||
                string.IsNullOrWhiteSpace(normalizedStationSchemeID))
            {
                return (null, null, BadRequest("instanceID and stationSchemeID are required."));
            }

            return (normalizedInstanceID, normalizedStationSchemeID, null);
        }

        private (string? InstanceID, string? StationSchemeID, string? OperationPlanID, IActionResult? ErrorResult)
            NormalizeOperationPlanScope(
                string? instanceID,
                string? stationSchemeID,
                string? operationPlanID,
                bool requireOperationPlanID = false)
        {
            var scope = NormalizeScope(instanceID, stationSchemeID);
            if (scope.ErrorResult != null)
            {
                return (null, null, null, scope.ErrorResult);
            }

            var normalizedOperationPlanID = operationPlanID?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedOperationPlanID))
            {
                if (requireOperationPlanID)
                {
                    return (null, null, null, BadRequest("operationPlanID is required."));
                }

                normalizedOperationPlanID = DefaultOperationPlanID;
            }

            return (scope.InstanceID, scope.StationSchemeID, TrimToMaxLength(normalizedOperationPlanID, 50), null);
        }

        private (OperationPlanRow? OperationPlan, IActionResult? ErrorResult) NormalizeOperationPlanRequest(
            OperationPlanRequest? request,
            bool allowMissingID)
        {
            var scope = NormalizeScope(request?.InstanceID, request?.StationSchemeID);
            if (scope.ErrorResult != null)
            {
                return (null, scope.ErrorResult);
            }

            var operationPlanID = request?.OperationPlanID?.Trim();
            if (!allowMissingID && string.IsNullOrWhiteSpace(operationPlanID))
            {
                return (null, BadRequest("operationPlanID is required."));
            }

            if (!string.IsNullOrWhiteSpace(operationPlanID))
            {
                operationPlanID = TrimToMaxLength(operationPlanID, 50);
            }

            var name = request?.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return (null, BadRequest("Name is required."));
            }

            return (new OperationPlanRow
            {
                InstanceID = scope.InstanceID,
                StationSchemeID = scope.StationSchemeID,
                OperationPlanID = operationPlanID,
                Name = TrimToMaxLength(name, 100),
                Description = TrimToMaxLength(request?.Description ?? string.Empty, 500),
                SortOrder = request?.SortOrder
            }, null);
        }

        private (TrainTemplateRow? Template, IActionResult? ErrorResult) NormalizeTrainTemplateRequest(
            TrainTemplateRequest? request,
            bool allowMissingID)
        {
            var scope = NormalizeOperationPlanScope(request?.InstanceID, request?.StationSchemeID, request?.OperationPlanID);
            if (scope.ErrorResult != null)
            {
                return (null, scope.ErrorResult);
            }

            var trainTemplateID = request?.TrainTemplateID?.Trim();
            if (!allowMissingID && string.IsNullOrWhiteSpace(trainTemplateID))
            {
                return (null, BadRequest("trainTemplateID is required."));
            }

            var name = request?.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return (null, BadRequest("Name is required."));
            }

            return (new TrainTemplateRow
            {
                InstanceID = scope.InstanceID,
                StationSchemeID = scope.StationSchemeID,
                OperationPlanID = scope.OperationPlanID,
                TrainTemplateID = trainTemplateID,
                Name = name,
                Type = request?.Type?.Trim() ?? string.Empty,
                Number = request?.Number,
                IsFixedOperation = NormalizeBinaryFlag(request?.IsFixedOperation)
            }, null);
        }

        private (MovementTemplateRow? Movement, IActionResult? ErrorResult) NormalizeMovementTemplateRequest(
            MovementTemplateRequest? request,
            bool allowMissingID)
        {
            var scope = NormalizeOperationPlanScope(request?.InstanceID, request?.StationSchemeID, request?.OperationPlanID);
            if (scope.ErrorResult != null)
            {
                return (null, scope.ErrorResult);
            }

            var trainTemplateID = request?.TrainTemplateID?.Trim();
            if (string.IsNullOrWhiteSpace(trainTemplateID))
            {
                return (null, BadRequest("trainTemplateID is required."));
            }

            var movementID = request?.MovementID?.Trim();
            if (!allowMissingID && string.IsNullOrWhiteSpace(movementID))
            {
                return (null, BadRequest("movementID is required."));
            }

            var name = request?.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return (null, BadRequest("Name is required."));
            }

            return (new MovementTemplateRow
            {
                InstanceID = scope.InstanceID,
                StationSchemeID = scope.StationSchemeID,
                OperationPlanID = scope.OperationPlanID,
                TrainTemplateID = trainTemplateID,
                MovementID = movementID,
                Name = name,
                RouteIDList = request?.RouteIDList?.Trim() ?? string.Empty,
                MinDuration = request?.MinDuration,
                SortOrder = request?.SortOrder
            }, null);
        }

        private (string? InstanceID, string? StationSchemeID, string? OperationPlanID, int StartMinutes, int EndMinutes, IActionResult? ErrorResult)
            NormalizeTrainOperationPlanRequest(GenerateTrainOperationPlanRequest? request)
        {
            var scope = NormalizeOperationPlanScope(request?.InstanceID, request?.StationSchemeID, request?.OperationPlanID);
            if (scope.ErrorResult != null)
            {
                return (null, null, null, 0, 0, scope.ErrorResult);
            }

            var startTime = string.IsNullOrWhiteSpace(request?.StartTime) ? "00:00" : request.StartTime.Trim();
            var endTime = string.IsNullOrWhiteSpace(request?.EndTime) ? "24:00" : request.EndTime.Trim();
            if (!TryParsePlanTime(startTime, out var startMinutes) ||
                !TryParsePlanTime(endTime, out var endMinutes))
            {
                return (null, null, null, 0, 0, BadRequest("startTime and endTime must be valid time values."));
            }

            while (endMinutes <= startMinutes)
            {
                endMinutes += 24 * 60;
            }

            return (scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, startMinutes, endMinutes, null);
        }

        private (TrainRow? Train, IActionResult? ErrorResult) NormalizeTrainRowRequest(
            TrainRow? request,
            bool allowMissingID)
        {
            var scope = NormalizeOperationPlanScope(request?.InstanceID, request?.StationSchemeID, request?.OperationPlanID);
            if (scope.ErrorResult != null)
            {
                return (null, scope.ErrorResult);
            }

            var id = request?.ID?.Trim();
            if (!allowMissingID && string.IsNullOrWhiteSpace(id))
            {
                return (null, BadRequest("id is required."));
            }

            return (new TrainRow
            {
                InstanceID = scope.InstanceID,
                StationSchemeID = scope.StationSchemeID,
                OperationPlanID = scope.OperationPlanID,
                ID = id,
                TrainTemplateID = request?.TrainTemplateID?.Trim() ?? string.Empty,
                TrainNumber = TrimToMaxLength(request?.TrainNumber ?? string.Empty, 50),
                Name = request?.Name?.Trim() ?? string.Empty,
                TrainType = TrimToMaxLength(request?.TrainType ?? string.Empty, 20),
                IsFixedOperation = NormalizeBinaryFlag(request?.IsFixedOperation)
            }, null);
        }

        private (MovementRow? Movement, IActionResult? ErrorResult) NormalizeMovementRowRequest(
            MovementRow? request,
            bool allowMissingMovementID)
        {
            var scope = NormalizeOperationPlanScope(request?.InstanceID, request?.StationSchemeID, request?.OperationPlanID);
            if (scope.ErrorResult != null)
            {
                return (null, scope.ErrorResult);
            }

            var trainID = request?.TrainID?.Trim();
            if (string.IsNullOrWhiteSpace(trainID))
            {
                return (null, BadRequest("trainID is required."));
            }

            var movementID = request?.MovementID?.Trim();
            if (!allowMissingMovementID && string.IsNullOrWhiteSpace(movementID))
            {
                return (null, BadRequest("movementID is required."));
            }

            return (new MovementRow
            {
                InstanceID = scope.InstanceID,
                StationSchemeID = scope.StationSchemeID,
                OperationPlanID = scope.OperationPlanID,
                TrainID = trainID,
                TrainTemplateID = request?.TrainTemplateID?.Trim() ?? string.Empty,
                MovementID = movementID,
                Name = request?.Name?.Trim() ?? string.Empty,
                RouteIDList = request?.RouteIDList?.Trim() ?? string.Empty,
                MinDuration = request?.MinDuration,
                EarliestStartTime = request?.EarliestStartTime?.Trim() ?? string.Empty,
                LatestEndTime = request?.LatestEndTime?.Trim() ?? string.Empty,
                Route = request?.Route?.Trim() ?? string.Empty,
                Tag = request?.Tag?.Trim() ?? string.Empty,
                SortOrder = request?.SortOrder
            }, null);
        }

        private (string? InstanceID, string? StationSchemeID, string? OperationPlanID, List<OperationBottleneckSummaryCategoryRow> Categories, IActionResult? ErrorResult)
            NormalizeOperationBottleneckSummaryCategorySaveRequest(OperationBottleneckSummaryCategorySaveRequest? request)
        {
            var scope = NormalizeOperationPlanScope(request?.InstanceID, request?.StationSchemeID, request?.OperationPlanID);
            if (scope.ErrorResult != null)
            {
                return (null, null, null, new List<OperationBottleneckSummaryCategoryRow>(), scope.ErrorResult);
            }

            var usedCategoryIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var categories = new List<OperationBottleneckSummaryCategoryRow>();
            var requestCategories = request?.Categories ?? new List<OperationBottleneckSummaryCategoryRow>();
            for (var index = 0; index < requestCategories.Count; index++)
            {
                var requestedCategory = requestCategories[index];
                var sortOrder = requestedCategory.SortOrder ?? index;
                var categoryID = TrimToMaxLength(requestedCategory.CategoryID ?? string.Empty, 50);
                if (string.IsNullOrWhiteSpace(categoryID))
                {
                    categoryID = Guid.NewGuid().ToString("N");
                }

                var baseCategoryID = categoryID;
                for (var duplicateIndex = 1; !usedCategoryIDs.Add(categoryID); duplicateIndex++)
                {
                    var suffix = $"_{duplicateIndex}";
                    categoryID = $"{TrimToMaxLength(baseCategoryID, Math.Max(1, 50 - suffix.Length))}{suffix}";
                }

                var routeIDList = string.Join(",", ParseRouteIDList(requestedCategory.RouteIDList));
                categories.Add(new OperationBottleneckSummaryCategoryRow
                {
                    InstanceID = scope.InstanceID,
                    StationSchemeID = scope.StationSchemeID,
                    OperationPlanID = scope.OperationPlanID,
                    CategoryID = categoryID,
                    Name = TrimToMaxLength(requestedCategory.Name ?? $"Category {sortOrder + 1}", 100),
                    RouteIDList = routeIDList,
                    SortOrder = sortOrder
                });
            }

            return (scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, categories, null);
        }

        private (OperationAnalysisResultResponse? Snapshot, IActionResult? ErrorResult)
            NormalizeOperationAnalysisResultSaveRequest(OperationAnalysisResultSaveRequest? request)
        {
            var scope = NormalizeOperationPlanScope(request?.InstanceID, request?.StationSchemeID, request?.OperationPlanID);
            if (scope.ErrorResult != null)
            {
                return (null, scope.ErrorResult);
            }

            var totalTimeSeconds = request?.TotalTimeSeconds;
            if (totalTimeSeconds <= 0)
            {
                totalTimeSeconds = null;
            }

            return (new OperationAnalysisResultResponse
            {
                InstanceID = scope.InstanceID,
                StationSchemeID = scope.StationSchemeID,
                OperationPlanID = scope.OperationPlanID,
                TotalTimeSeconds = totalTimeSeconds,
                Cells = (request?.Cells ?? new List<OperationAnalysisCellSnapshotRow>())
                    .Select(cell => new OperationAnalysisCellSnapshotRow
                    {
                        ID = TrimToMaxLength(cell.ID ?? string.Empty, 50),
                        Name = TrimToMaxLength(cell.Name ?? string.Empty, 100)
                    })
                    .Where(cell => !string.IsNullOrWhiteSpace(cell.ID))
                    .ToList(),
                OccupationTimeTableRows = (request?.OccupationTimeTableRows ?? new List<OperationOccupationTimeTableSnapshotRow>())
                    .Select(row => new OperationOccupationTimeTableSnapshotRow
                    {
                        RowType = TrimToMaxLength(row.RowType ?? string.Empty, 20),
                        Sequence = TrimToMaxLength(row.Sequence ?? string.Empty, 50),
                        RouteID = TrimToMaxLength(row.RouteID ?? string.Empty, 50),
                        RouteName = TrimToMaxLength(row.RouteName ?? string.Empty, 200),
                        OperationCount = TrimToMaxLength(row.OperationCount ?? string.Empty, 50),
                        CellDurations = NormalizeOperationAnalysisCellDurations(row.CellDurations),
                        InterruptCellDurations = NormalizeOperationAnalysisCellDurations(row.InterruptCellDurations)
                    })
                    .Where(row => !string.IsNullOrWhiteSpace(row.RowType))
                    .ToList(),
                BottleneckAnalysisRows = (request?.BottleneckAnalysisRows ?? new List<OperationBottleneckAnalysisSnapshotRow>())
                    .Select(row => new OperationBottleneckAnalysisSnapshotRow
                    {
                        RouteID = TrimToMaxLength(row.RouteID ?? string.Empty, 50),
                        RouteName = TrimToMaxLength(row.RouteName ?? string.Empty, 200),
                        OperationCount = Math.Max(0, row.OperationCount),
                        BottleneckCellID = TrimToMaxLength(row.BottleneckCellID ?? string.Empty, 50),
                        BottleneckCellName = TrimToMaxLength(row.BottleneckCellName ?? string.Empty, 100),
                        BottleneckUtilization = NormalizeFiniteDouble(row.BottleneckUtilization),
                        ThroughputCapacity = NormalizeFiniteDouble(row.ThroughputCapacity)
                    })
                    .Where(row => !string.IsNullOrWhiteSpace(row.RouteID))
                    .ToList(),
                ThroughputSummaryRows = (request?.ThroughputSummaryRows ?? new List<OperationBottleneckSummarySnapshotRow>())
                    .Select(row => new OperationBottleneckSummarySnapshotRow
                    {
                        CategoryID = TrimToMaxLength(row.CategoryID ?? string.Empty, 50),
                        GroupKey = TrimToMaxLength(row.GroupKey ?? string.Empty, 50),
                        GroupText = TrimToMaxLength(row.GroupText ?? string.Empty, 100),
                        RouteIDs = row.RouteIDs
                            .Select(routeID => TrimToMaxLength(routeID ?? string.Empty, 50))
                            .Where(routeID => !string.IsNullOrWhiteSpace(routeID))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                        RouteCount = Math.Max(0, row.RouteCount),
                        OperationCount = Math.Max(0, row.OperationCount),
                        CapacityTotal = NormalizeFiniteDouble(row.CapacityTotal),
                        CapacityAverage = NormalizeFiniteDouble(row.CapacityAverage)
                    })
                    .Where(row => !string.IsNullOrWhiteSpace(row.CategoryID) || !string.IsNullOrWhiteSpace(row.GroupKey))
                    .ToList(),
                UpdatedDate = DateTime.Now
            }, null);
        }

        private (string? InstanceID, string? StationSchemeID, string? OperationPlanID, List<OperationOccupationTimeSubTableSetting>? SubTables, IActionResult? ErrorResult)
            NormalizeOperationOccupationTimeSubTableSaveRequest(OperationOccupationTimeSubTableSaveRequest? request)
        {
            var scope = NormalizeOperationPlanScope(request?.InstanceID, request?.StationSchemeID, request?.OperationPlanID);
            if (scope.ErrorResult != null)
            {
                return (null, null, null, null, scope.ErrorResult);
            }

            var subTables = new List<OperationOccupationTimeSubTableSetting>();
            var usedSubTableIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sortOrder = 0;
            foreach (var requestedSubTable in request?.SubTables ?? new List<OperationOccupationTimeSubTableSetting>())
            {
                var subTableID = TrimToMaxLength(requestedSubTable.SubTableID ?? string.Empty, 50);
                if (string.IsNullOrWhiteSpace(subTableID) || !usedSubTableIDs.Add(subTableID))
                {
                    subTableID = $"subtable-{sortOrder + 1}";
                    while (!usedSubTableIDs.Add(subTableID))
                    {
                        subTableID = $"subtable-{sortOrder + 1}-{usedSubTableIDs.Count + 1}";
                    }
                }

                var requestedCellIDs = requestedSubTable.CellIDs ?? new List<string>();
                var cellIDs = requestedCellIDs.Count > 0
                    ? NormalizeOperationOccupationTimeSubTableCellIDs(requestedCellIDs)
                    : NormalizeOperationOccupationTimeSubTableCellIDs(ParseRouteIDList(requestedSubTable.CellIDList));

                subTables.Add(new OperationOccupationTimeSubTableSetting
                {
                    InstanceID = scope.InstanceID,
                    StationSchemeID = scope.StationSchemeID,
                    OperationPlanID = scope.OperationPlanID,
                    SubTableID = subTableID,
                    SubTableName = TrimToMaxLength(
                        string.IsNullOrWhiteSpace(requestedSubTable.SubTableName)
                            ? $"SubTable {sortOrder + 1}"
                            : requestedSubTable.SubTableName,
                        100),
                    CellIDs = cellIDs,
                    CellIDList = string.Join(",", cellIDs),
                    SortOrder = requestedSubTable.SortOrder ?? sortOrder
                });
                sortOrder++;
            }

            return (scope.InstanceID, scope.StationSchemeID, scope.OperationPlanID, subTables, null);
        }

        private static List<string> NormalizeOperationOccupationTimeSubTableCellIDs(IEnumerable<string>? cellIDs)
        {
            return (cellIDs ?? Enumerable.Empty<string>())
                .Select(cellID => TrimToMaxLength(cellID ?? string.Empty, 50))
                .Where(cellID => !string.IsNullOrWhiteSpace(cellID))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private TrainOperationPlanResponse BuildGeneratedTrainOperationPlan(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID,
            int startMinutes,
            int endMinutes)
        {
            var templates = LoadTrainTemplates(dbConnector, instanceID, stationSchemeID, operationPlanID);
            var movementTemplatesByTrainTemplate = LoadMovementTemplatesByTrainTemplate(
                dbConnector,
                instanceID,
                stationSchemeID,
                operationPlanID);
            var generatedTrainCountByTemplate = templates.ToDictionary(
                template => template.TrainTemplateID ?? string.Empty,
                template => Math.Max(0, template.Number ?? 0),
                StringComparer.OrdinalIgnoreCase);
            var totalMovementCount = templates.Sum(template =>
            {
                var trainTemplateID = template.TrainTemplateID ?? string.Empty;
                movementTemplatesByTrainTemplate.TryGetValue(trainTemplateID, out var movementTemplates);
                return generatedTrainCountByTemplate[trainTemplateID] * (movementTemplates?.Count ?? 0);
            });
            var maxMinDuration = movementTemplatesByTrainTemplate.Values
                .SelectMany(movementTemplates => movementTemplates)
                .Select(movement => Math.Max(0, movement.MinDuration ?? 0))
                .DefaultIfEmpty(0)
                .Max();
            var distributionEndMinutes = Math.Max(startMinutes, endMinutes - maxMinDuration);
            var availableMinutes = Math.Max(0, distributionEndMinutes - startMinutes);
            var movementSlotSize = totalMovementCount > 1
                ? availableMinutes / (double)(totalMovementCount - 1)
                : 0d;
            var generatedMovementIndex = 0;
            var generatedTrainSequence = 1;
            var usedTrainIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new TrainOperationPlanResponse();

            foreach (var template in templates)
            {
                var trainTemplateID = template.TrainTemplateID ?? string.Empty;
                var trainCount = generatedTrainCountByTemplate[trainTemplateID];
                movementTemplatesByTrainTemplate.TryGetValue(trainTemplateID, out var movementTemplates);
                movementTemplates ??= new List<MovementTemplateRow>();

                for (var trainSequence = 1; trainSequence <= trainCount; trainSequence++)
                {
                    var trainID = BuildGeneratedTrainID(trainTemplateID, trainSequence, usedTrainIds);
                    usedTrainIds.Add(trainID);
                    result.Trains.Add(new TrainRow
                    {
                        InstanceID = instanceID,
                        StationSchemeID = stationSchemeID,
                        OperationPlanID = operationPlanID,
                        ID = trainID,
                        TrainTemplateID = trainTemplateID,
                        TrainNumber = (generatedTrainSequence++).ToString(),
                        Name = TrimToMaxLength(template.Name ?? string.Empty, 50),
                        TrainType = TrimToMaxLength(template.Type ?? string.Empty, 20),
                        IsFixedOperation = NormalizeBinaryFlag(template.IsFixedOperation)
                    });

                    var trainCursorMinutes = startMinutes;
                    for (var movementSortOrder = 0; movementSortOrder < movementTemplates.Count; movementSortOrder++)
                    {
                        var movementTemplate = movementTemplates[movementSortOrder];
                        var minDuration = Math.Max(0, movementTemplate.MinDuration ?? 0);
                        var plannedStartMinutes = startMinutes + (int)Math.Round(generatedMovementIndex * movementSlotSize);
                        var earliestStartMinutes = Math.Max(plannedStartMinutes, trainCursorMinutes);
                        var latestEndMinutes = earliestStartMinutes + minDuration;
                        var routeAlternatives = ParseRouteIDList(movementTemplate.RouteIDList);
                        var route = routeAlternatives.Count > 0
                            ? routeAlternatives[(trainSequence - 1) % routeAlternatives.Count]
                            : string.Empty;

                        result.Movements.Add(new MovementRow
                        {
                            InstanceID = instanceID,
                            StationSchemeID = stationSchemeID,
                            OperationPlanID = operationPlanID,
                            TrainID = trainID,
                            TrainTemplateID = TrimToMaxLength(movementTemplate.TrainTemplateID ?? string.Empty, 50),
                            MovementID = TrimToMaxLength(movementTemplate.MovementID ?? string.Empty, 50),
                            Name = TrimToMaxLength(movementTemplate.Name ?? string.Empty, 50),
                            RouteIDList = movementTemplate.RouteIDList ?? string.Empty,
                            MinDuration = movementTemplate.MinDuration,
                            EarliestStartTime = FormatPlanTime(earliestStartMinutes),
                            LatestEndTime = FormatPlanTime(latestEndMinutes),
                            Route = TrimToMaxLength(route, 50),
                            Tag = TrimToMaxLength(movementTemplate.Name ?? string.Empty, 50),
                            SortOrder = movementSortOrder
                        });

                        trainCursorMinutes = latestEndMinutes;
                        generatedMovementIndex++;
                    }
                }
            }

            return result;
        }

        private static bool TryParsePlanTime(string? value, out int minutes)
        {
            minutes = 0;
            var text = value?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var dayOffset = 0;
            if (text.StartsWith("D+", StringComparison.OrdinalIgnoreCase))
            {
                var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2 ||
                    !int.TryParse(parts[0].Substring(2), out dayOffset) ||
                    dayOffset < 0)
                {
                    return false;
                }

                text = parts[1].Trim();
            }

            var timeParts = text.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (timeParts.Length < 2 ||
                !int.TryParse(timeParts[0], out var hours) ||
                !int.TryParse(timeParts[1], out var minutePart) ||
                hours < 0 ||
                minutePart < 0 ||
                minutePart >= 60)
            {
                return false;
            }

            minutes = dayOffset * 24 * 60 + hours * 60 + minutePart;
            return true;
        }

        private static string FormatPlanTime(int totalMinutes)
        {
            if (totalMinutes < 0)
            {
                totalMinutes = 0;
            }

            if (totalMinutes == 24 * 60)
            {
                return "24:00";
            }

            var days = totalMinutes / (24 * 60);
            var minutesInDay = totalMinutes % (24 * 60);
            var hours = minutesInDay / 60;
            var minutes = minutesInDay % 60;
            var timeText = $"{hours:D2}:{minutes:D2}";
            return days > 0 ? $"D+{days} {timeText}" : timeText;
        }

        private static string BuildGeneratedTrainID(
            string trainTemplateID,
            int trainSequence,
            HashSet<string> usedTrainIds)
        {
            var baseID = string.IsNullOrWhiteSpace(trainTemplateID)
                ? "TRAIN"
                : trainTemplateID.Trim();
            for (var attempt = 0; attempt < 1000; attempt++)
            {
                var suffix = attempt == 0
                    ? $"-{trainSequence:000}"
                    : $"-{trainSequence:000}-{attempt}";
                var candidate = $"{TrimToMaxLength(baseID, Math.Max(1, 50 - suffix.Length))}{suffix}";
                if (!usedTrainIds.Contains(candidate))
                {
                    return candidate;
                }
            }

            return Guid.NewGuid().ToString("N");
        }

        private static string TrimToMaxLength(string value, int maxLength)
        {
            var normalized = value.Trim();
            if (normalized.Length <= maxLength)
            {
                return normalized;
            }

            return normalized[..maxLength];
        }

        private static int NormalizeBinaryFlag(int? value)
        {
            return value == 1 ? 1 : 0;
        }

        private static int GetTrainNumberSortValue(string? trainNumber)
        {
            return int.TryParse(trainNumber?.Trim(), out var parsed)
                ? parsed
                : int.MaxValue;
        }

        private static List<string> ParseRouteIDList(string? routeIDList)
        {
            if (string.IsNullOrWhiteSpace(routeIDList))
            {
                return new List<string>();
            }

            return routeIDList
                .Split(new[] { ',', ';', '，', '；', '\r', '\n', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static double? NormalizeFiniteDouble(double? value)
        {
            return value.HasValue && double.IsFinite(value.Value) ? value : null;
        }

        private static Dictionary<string, double> NormalizeOperationAnalysisCellDurations(
            Dictionary<string, double>? cellDurations)
        {
            var normalizedDurations = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (cellDurations == null)
            {
                return normalizedDurations;
            }

            foreach (var item in cellDurations)
            {
                if (string.IsNullOrWhiteSpace(item.Key) || !double.IsFinite(item.Value))
                {
                    continue;
                }

                var cellID = TrimToMaxLength(item.Key, 50);
                normalizedDurations[cellID] = Math.Max(0, item.Value);
            }

            return normalizedDurations;
        }

        private static string BuildOperationOccupationRowKey(
            OperationOccupationTimeTableSnapshotRow row,
            int index,
            ISet<string> usedRowKeys)
        {
            var rowType = row.RowType?.Trim().ToLowerInvariant() ?? string.Empty;
            var baseKey = rowType switch
            {
                "total" => "total",
                "utilization" => "utilization",
                "route" when !string.IsNullOrWhiteSpace(row.RouteID) => $"route:{row.RouteID!.Trim()}",
                _ => $"row:{index}"
            };

            baseKey = TrimToMaxLength(baseKey, 100);
            var rowKey = baseKey;
            for (var duplicateIndex = 1; !usedRowKeys.Add(rowKey); duplicateIndex++)
            {
                var suffix = $"_{duplicateIndex}";
                rowKey = $"{TrimToMaxLength(baseKey, Math.Max(1, 100 - suffix.Length))}{suffix}";
            }

            return rowKey;
        }

        private IActionResult? ValidateCapacityInstanceOwnershipOrFail(DBConnector dbConnector, string instanceID)
        {
            var instance = (dbConnector.Query<CapacityInstance>(
                "SELECT * FROM capacityinstance WHERE ID = @instanceID",
                new { instanceID }) ?? new List<CapacityInstance>()).FirstOrDefault();
            if (instance == null)
            {
                return NotFound("Instance not found.");
            }

            if (IsCurrentUserAdmin())
            {
                return null;
            }

            var username = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized("Invalid user context.");
            }

            if (!string.Equals(instance.Owner, username, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "User {Username} is not the owner of capacity instance {InstanceID}.",
                    username,
                    instanceID);
                return Unauthorized("Instance not owned by user.");
            }

            return null;
        }

        private bool IsCurrentUserAdmin()
        {
            var username = User?.Identity?.Name;
            if (string.Equals(username, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var role = User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        private List<OperationPlanRow> LoadOperationPlans(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID)
        {
            return dbConnector.Query<OperationPlanRow>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, Name, Description, SortOrder, CreatedDate, UpdatedDate
                   FROM {QuoteIdentifier("operationplan")}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID
                   ORDER BY SortOrder IS NULL, SortOrder, OperationPlanID",
                new { instanceID, stationSchemeID }) ?? new List<OperationPlanRow>();
        }

        private OperationPlanRow? LoadOperationPlan(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID)
        {
            return (dbConnector.Query<OperationPlanRow>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, Name, Description, SortOrder, CreatedDate, UpdatedDate
                   FROM {QuoteIdentifier("operationplan")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                   LIMIT 1",
                new { instanceID, stationSchemeID, operationPlanID }) ?? new List<OperationPlanRow>())
                .FirstOrDefault();
        }

        private int GetNextOperationPlanSortOrder(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID)
        {
            return LoadOperationPlans(dbConnector, instanceID, stationSchemeID)
                .Select(plan => plan.SortOrder ?? -1)
                .DefaultIfEmpty(-1)
                .Max() + 1;
        }

        private List<TrainTemplateRow> LoadTrainTemplates(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID)
        {
            return dbConnector.Query<TrainTemplateRow>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, TrainTemplateID, Name,
                           {QuoteIdentifier("Type")} AS {QuoteIdentifier("Type")},
                           {QuoteIdentifier("Number")} AS {QuoteIdentifier("Number")},
                           IsFixedOperation
                   FROM {QuoteIdentifier("traintemplate")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                   ORDER BY {QuoteIdentifier("Number")} IS NULL,
                            {QuoteIdentifier("Number")},
                            TrainTemplateID",
                new { instanceID, stationSchemeID, operationPlanID }) ?? new List<TrainTemplateRow>();
        }

        private List<MovementTemplateRow> LoadMovementTemplates(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID,
            string trainTemplateID)
        {
            return dbConnector.Query<MovementTemplateRow>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, TrainTemplateID, MovementID, Name, RouteIDList, MinDuration, SortOrder
                   FROM {QuoteIdentifier("movementtemplate")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                     AND TrainTemplateID = @trainTemplateID
                   ORDER BY SortOrder IS NULL, SortOrder, MinDuration IS NULL, MinDuration, MovementID",
                new { instanceID, stationSchemeID, operationPlanID, trainTemplateID }) ?? new List<MovementTemplateRow>();
        }

        private Dictionary<string, List<MovementTemplateRow>> LoadMovementTemplatesByTrainTemplate(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID)
        {
            var rows = dbConnector.Query<MovementTemplateRow>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, TrainTemplateID, MovementID, Name, RouteIDList, MinDuration, SortOrder
                   FROM {QuoteIdentifier("movementtemplate")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                   ORDER BY TrainTemplateID, SortOrder IS NULL, SortOrder, MinDuration IS NULL, MinDuration, MovementID",
                new { instanceID, stationSchemeID, operationPlanID }) ?? new List<MovementTemplateRow>();
            return rows
                .Where(row => !string.IsNullOrWhiteSpace(row.TrainTemplateID))
                .GroupBy(row => row.TrainTemplateID!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToList(),
                    StringComparer.OrdinalIgnoreCase);
        }

        private TrainOperationPlanResponse LoadTrainOperationPlan(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID)
        {
            return new TrainOperationPlanResponse
            {
                Trains = LoadTrains(dbConnector, instanceID, stationSchemeID, operationPlanID),
                Movements = LoadMovements(dbConnector, instanceID, stationSchemeID, operationPlanID)
            };
        }

        private List<TrainRow> LoadTrains(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID)
        {
            var trains = dbConnector.Query<TrainRow>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, {QuoteIdentifier("ID")}, TrainTemplateID, TrainNumber, Name, TrainType, IsFixedOperation
                   FROM {QuoteIdentifier("train")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID",
                new { instanceID, stationSchemeID, operationPlanID }) ?? new List<TrainRow>();
            return trains
                .OrderBy(train => GetTrainNumberSortValue(train.TrainNumber))
                .ThenBy(train => train.TrainNumber, StringComparer.OrdinalIgnoreCase)
                .ThenBy(train => train.TrainTemplateID, StringComparer.OrdinalIgnoreCase)
                .ThenBy(train => train.ID, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<MovementRow> LoadMovements(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID)
        {
            return dbConnector.Query<MovementRow>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, TrainID, TrainTemplateID, MovementID, Name, RouteIDList,
                           MinDuration, EarliestStartTime, LatestEndTime,
                           {QuoteIdentifier("Route")}, Tag, SortOrder
                   FROM {QuoteIdentifier("movement")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                   ORDER BY TrainID, SortOrder IS NULL, SortOrder, EarliestStartTime, MovementID",
                new { instanceID, stationSchemeID, operationPlanID }) ?? new List<MovementRow>();
        }

        private List<OperationBottleneckSummaryCategoryRow> LoadOperationBottleneckSummaryCategories(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID)
        {
            var categoryColumns = GetColumnNames(dbConnector, "operationbottlenecksummarycategory");
            var routeIDListSelect = categoryColumns.Any(column => string.Equals(column, "RouteIDList", StringComparison.OrdinalIgnoreCase))
                ? QuoteIdentifier("RouteIDList")
                : "NULL AS RouteIDList";
            var categories = dbConnector.Query<OperationBottleneckSummaryCategoryRow>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, CategoryID, Name, {routeIDListSelect}, SortOrder
                   FROM {QuoteIdentifier("operationbottlenecksummarycategory")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                   ORDER BY SortOrder IS NULL, SortOrder, CategoryID",
                new { instanceID, stationSchemeID, operationPlanID }) ?? new List<OperationBottleneckSummaryCategoryRow>();

            var categoryRoutes = dbConnector.Query<OperationBottleneckSummaryCategoryRouteRow>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, CategoryID, RouteID, SortOrder
                   FROM {QuoteIdentifier("operationbottlenecksummarycategoryroute")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                   ORDER BY CategoryID, SortOrder IS NULL, SortOrder, RouteID",
                new { instanceID, stationSchemeID, operationPlanID }) ?? new List<OperationBottleneckSummaryCategoryRouteRow>();

            var routeIDListMap = categoryRoutes
                .Where(route => !string.IsNullOrWhiteSpace(route.CategoryID) && !string.IsNullOrWhiteSpace(route.RouteID))
                .GroupBy(route => route.CategoryID!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => string.Join(",", group.Select(route => route.RouteID!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var category in categories)
            {
                if (!string.IsNullOrWhiteSpace(category.CategoryID) &&
                    routeIDListMap.TryGetValue(category.CategoryID!, out var routeIDList))
                {
                    category.RouteIDList = routeIDList;
                    continue;
                }

                category.RouteIDList = string.Join(",", ParseRouteIDList(category.RouteIDList));
            }

            return categories;
        }

        private OperationAnalysisResultResponse? LoadOperationAnalysisResult(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID)
        {
            var meta = (dbConnector.Query<OperationAnalysisMetaRow>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, TotalTimeSeconds, UpdatedDate
                   FROM {QuoteIdentifier("operationanalysismeta")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                   ORDER BY UpdatedDate DESC
                   LIMIT 1",
                new { instanceID, stationSchemeID, operationPlanID }) ?? new List<OperationAnalysisMetaRow>())
                .FirstOrDefault();

            var cells = dbConnector.Query<OperationAnalysisCellRow>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, CellID, CellName, SortOrder
                   FROM {QuoteIdentifier("operationanalysiscell")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                   ORDER BY SortOrder IS NULL, SortOrder, CellID",
                new { instanceID, stationSchemeID, operationPlanID }) ?? new List<OperationAnalysisCellRow>();

            var occupationRows = dbConnector.Query<OperationOccupationTimeTableResultRow>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, RowKey, RowType, SequenceText, RouteID, RouteName, OperationCountText, SortOrder
                   FROM {QuoteIdentifier("operationoccupationtimerow")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                   ORDER BY SortOrder IS NULL, SortOrder, RowKey",
                new { instanceID, stationSchemeID, operationPlanID }) ?? new List<OperationOccupationTimeTableResultRow>();

            var occupationCellValues = dbConnector.Query<OperationOccupationTimeCellValueRow>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, RowKey, CellID, CellValue, InterruptCellValue
                   FROM {QuoteIdentifier("operationoccupationtimecell")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID",
                new { instanceID, stationSchemeID, operationPlanID }) ?? new List<OperationOccupationTimeCellValueRow>();

            var bottleneckRows = dbConnector.Query<OperationBottleneckAnalysisResultRow>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, RouteID, RouteName, OperationCount, BottleneckCellID, BottleneckCellName,
                          BottleneckUtilization, ThroughputCapacity, SortOrder
                   FROM {QuoteIdentifier("operationbottleneckanalysisresult")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                   ORDER BY SortOrder IS NULL, SortOrder, RouteID",
                new { instanceID, stationSchemeID, operationPlanID }) ?? new List<OperationBottleneckAnalysisResultRow>();

            var summaryRows = dbConnector.Query<OperationThroughputSummaryResultRow>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, CategoryID, GroupKey, GroupText, RouteCount, OperationCount,
                          CapacityTotal, CapacityAverage, SortOrder
                   FROM {QuoteIdentifier("operationthroughputsummaryresult")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                   ORDER BY SortOrder IS NULL, SortOrder, CategoryID",
                new { instanceID, stationSchemeID, operationPlanID }) ?? new List<OperationThroughputSummaryResultRow>();

            var summaryRoutes = dbConnector.Query<OperationThroughputSummaryRouteRow>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, CategoryID, RouteID, SortOrder
                   FROM {QuoteIdentifier("operationthroughputsummaryroute")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                   ORDER BY CategoryID, SortOrder IS NULL, SortOrder, RouteID",
                new { instanceID, stationSchemeID, operationPlanID }) ?? new List<OperationThroughputSummaryRouteRow>();

            if (meta == null &&
                cells.Count == 0 &&
                occupationRows.Count == 0 &&
                bottleneckRows.Count == 0 &&
                summaryRows.Count == 0)
            {
                return null;
            }

            var occupationCellValueMap = occupationCellValues
                .Where(value => !string.IsNullOrWhiteSpace(value.RowKey) &&
                                !string.IsNullOrWhiteSpace(value.CellID) &&
                                value.CellValue.HasValue)
                .GroupBy(value => value.RowKey!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToDictionary(
                        value => value.CellID!,
                        value => value.CellValue!.Value,
                        StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

            var occupationInterruptCellValueMap = occupationCellValues
                .Where(value => !string.IsNullOrWhiteSpace(value.RowKey) &&
                                !string.IsNullOrWhiteSpace(value.CellID) &&
                                value.InterruptCellValue.HasValue)
                .GroupBy(value => value.RowKey!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToDictionary(
                        value => value.CellID!,
                        value => value.InterruptCellValue!.Value,
                        StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

            var summaryRouteMap = summaryRoutes
                .Where(route => !string.IsNullOrWhiteSpace(route.CategoryID) && !string.IsNullOrWhiteSpace(route.RouteID))
                .GroupBy(route => route.CategoryID!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(route => route.SortOrder ?? int.MaxValue)
                        .Select(route => route.RouteID!)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);

            return new OperationAnalysisResultResponse
            {
                InstanceID = meta?.InstanceID ?? instanceID,
                StationSchemeID = meta?.StationSchemeID ?? stationSchemeID,
                OperationPlanID = meta?.OperationPlanID ?? operationPlanID,
                TotalTimeSeconds = meta?.TotalTimeSeconds,
                Cells = cells.Select(cell => new OperationAnalysisCellSnapshotRow
                {
                    ID = cell.CellID,
                    Name = cell.CellName
                }).ToList(),
                OccupationTimeTableRows = occupationRows.Select(row => new OperationOccupationTimeTableSnapshotRow
                {
                    RowType = row.RowType,
                    Sequence = row.SequenceText,
                    RouteID = row.RouteID,
                    RouteName = row.RouteName,
                    OperationCount = row.OperationCountText,
                    CellDurations = !string.IsNullOrWhiteSpace(row.RowKey) &&
                                    occupationCellValueMap.TryGetValue(row.RowKey, out var values)
                        ? values
                        : new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
                    InterruptCellDurations = !string.IsNullOrWhiteSpace(row.RowKey) &&
                                             occupationInterruptCellValueMap.TryGetValue(row.RowKey, out var interruptValues)
                        ? interruptValues
                        : new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                }).ToList(),
                BottleneckAnalysisRows = bottleneckRows.Select(row => new OperationBottleneckAnalysisSnapshotRow
                {
                    RouteID = row.RouteID,
                    RouteName = row.RouteName,
                    OperationCount = Math.Max(0, row.OperationCount ?? 0),
                    BottleneckCellID = row.BottleneckCellID,
                    BottleneckCellName = row.BottleneckCellName,
                    BottleneckUtilization = NormalizeFiniteDouble(row.BottleneckUtilization),
                    ThroughputCapacity = NormalizeFiniteDouble(row.ThroughputCapacity)
                }).ToList(),
                ThroughputSummaryRows = summaryRows.Select(row => new OperationBottleneckSummarySnapshotRow
                {
                    CategoryID = row.CategoryID,
                    GroupKey = row.GroupKey,
                    GroupText = row.GroupText,
                    RouteIDs = !string.IsNullOrWhiteSpace(row.CategoryID) &&
                               summaryRouteMap.TryGetValue(row.CategoryID, out var routeIDs)
                        ? routeIDs
                        : new List<string>(),
                    RouteCount = Math.Max(0, row.RouteCount ?? 0),
                    OperationCount = Math.Max(0, row.OperationCount ?? 0),
                    CapacityTotal = NormalizeFiniteDouble(row.CapacityTotal),
                    CapacityAverage = NormalizeFiniteDouble(row.CapacityAverage)
                }).ToList(),
                UpdatedDate = meta?.UpdatedDate
            };
        }

        private List<OperationOccupationTimeSubTableSetting> LoadOperationOccupationTimeSubTableSettings(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID)
        {
            var rows = dbConnector.Query<OperationOccupationTimeSubTableSetting>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, SubTableID, SubTableName, CellIDList, SortOrder
                   FROM {QuoteIdentifier("operationoccupationtimesubtable")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                   ORDER BY CASE WHEN SortOrder IS NULL THEN 1 ELSE 0 END, SortOrder, SubTableID",
                new { instanceID, stationSchemeID, operationPlanID }) ?? new List<OperationOccupationTimeSubTableSetting>();

            return rows
                .Select((row, index) => new OperationOccupationTimeSubTableSetting
                {
                    InstanceID = row.InstanceID ?? instanceID,
                    StationSchemeID = row.StationSchemeID ?? stationSchemeID,
                    OperationPlanID = row.OperationPlanID ?? operationPlanID,
                    SubTableID = row.SubTableID,
                    SubTableName = row.SubTableName,
                    CellIDs = NormalizeOperationOccupationTimeSubTableCellIDs(ParseRouteIDList(row.CellIDList)),
                    CellIDList = row.CellIDList,
                    SortOrder = row.SortOrder ?? index
                })
                .Where(row => !string.IsNullOrWhiteSpace(row.SubTableID))
                .ToList();
        }

        private void DeleteOperationOccupationTimeSubTableSettings(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID)
        {
            dbConnector.ExecuteNonQuery(
                $@"DELETE FROM {QuoteIdentifier("operationoccupationtimesubtable")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID",
                new { instanceID, stationSchemeID, operationPlanID });
        }

        private void InsertOperationOccupationTimeSubTableSettings(
            DBConnector dbConnector,
            IEnumerable<OperationOccupationTimeSubTableSetting> subTables)
        {
            foreach (var subTable in subTables)
            {
                dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {QuoteIdentifier("operationoccupationtimesubtable")} (
                           InstanceID, StationSchemeID, OperationPlanID, SubTableID, SubTableName, CellIDList, SortOrder)
                       VALUES (
                           @InstanceID, @StationSchemeID, @OperationPlanID, @SubTableID, @SubTableName, @CellIDList, @SortOrder)",
                    new OperationOccupationTimeSubTableSetting
                    {
                        InstanceID = subTable.InstanceID,
                        StationSchemeID = subTable.StationSchemeID,
                        OperationPlanID = subTable.OperationPlanID,
                        SubTableID = subTable.SubTableID,
                        SubTableName = subTable.SubTableName,
                        CellIDList = string.Join(",", NormalizeOperationOccupationTimeSubTableCellIDs(subTable.CellIDs)),
                        SortOrder = subTable.SortOrder
                    });
            }
        }

        private void InsertOperationPlan(DBConnector dbConnector, OperationPlanRow operationPlan)
        {
            dbConnector.ExecuteNonQuery(
                $@"INSERT INTO {QuoteIdentifier("operationplan")} (
                       InstanceID, StationSchemeID, OperationPlanID, Name, Description, SortOrder, CreatedDate, UpdatedDate)
                   VALUES (
                       @InstanceID, @StationSchemeID, @OperationPlanID, @Name, @Description, @SortOrder, @CreatedDate, @UpdatedDate)",
                operationPlan);
        }

        private void UpdateOperationPlan(
            DBConnector dbConnector,
            OperationPlanRow operationPlan,
            string originalOperationPlanID)
        {
            dbConnector.ExecuteNonQuery(
                $@"UPDATE {QuoteIdentifier("operationplan")}
                   SET OperationPlanID = @OperationPlanID,
                       Name = @Name,
                       Description = @Description,
                       SortOrder = @SortOrder,
                       UpdatedDate = @UpdatedDate
                   WHERE InstanceID = @InstanceID
                     AND StationSchemeID = @StationSchemeID
                     AND OperationPlanID = @OriginalOperationPlanID",
                new
                {
                    operationPlan.InstanceID,
                    operationPlan.StationSchemeID,
                    operationPlan.OperationPlanID,
                    operationPlan.Name,
                    operationPlan.Description,
                    operationPlan.SortOrder,
                    operationPlan.UpdatedDate,
                    OriginalOperationPlanID = originalOperationPlanID
                });
        }

        private static void EnsureDefaultOperationPlan(DBConnector dbConnector, string instanceID, string stationSchemeID)
        {
            EnsureOperationPlanObjectSchema(dbConnector);
            var exists = (dbConnector.Query<OperationPlanRow>(
                $@"SELECT OperationPlanID
                   FROM {QuoteIdentifier("operationplan")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                   LIMIT 1",
                new { instanceID, stationSchemeID, operationPlanID = DefaultOperationPlanID }) ?? new List<OperationPlanRow>()).Any();
            if (exists)
            {
                return;
            }

            var now = DateTime.Now;
            dbConnector.ExecuteNonQuery(
                $@"INSERT INTO {QuoteIdentifier("operationplan")} (
                       InstanceID, StationSchemeID, OperationPlanID, Name, Description, SortOrder, CreatedDate, UpdatedDate)
                   VALUES (
                       @instanceID, @stationSchemeID, @operationPlanID, @name, @description, @sortOrder, @createdDate, @updatedDate)",
                new
                {
                    instanceID,
                    stationSchemeID,
                    operationPlanID = DefaultOperationPlanID,
                    name = DefaultOperationPlanName,
                    description = string.Empty,
                    sortOrder = 0,
                    createdDate = now,
                    updatedDate = now
                });
        }

        private static IReadOnlyList<string> OperationPlanScopedTableNames { get; } = new[]
        {
            "operationthroughputsummaryroute",
            "operationthroughputsummaryresult",
            "operationbottleneckanalysisresult",
            "operationoccupationtimesubtable",
            "operationoccupationtimecell",
            "operationoccupationtimerow",
            "operationanalysiscell",
            "operationanalysismeta",
            "operationbottlenecksummarycategoryroute",
            "operationbottlenecksummarycategory",
            "movement",
            "train",
            "movementtemplate",
            "traintemplate"
        };

        private static void DeleteOperationPlanScopedData(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID)
        {
            foreach (var tableName in OperationPlanScopedTableNames)
            {
                dbConnector.ExecuteNonQuery(
                    $@"DELETE FROM {QuoteIdentifier(tableName)}
                       WHERE InstanceID = @instanceID
                         AND StationSchemeID = @stationSchemeID
                         AND OperationPlanID = @operationPlanID",
                    new { instanceID, stationSchemeID, operationPlanID });
            }
        }

        private static void CopyOperationPlanScopedData(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string sourceOperationPlanID,
            string targetOperationPlanID)
        {
            foreach (var tableName in OperationPlanScopedTableNames.Reverse())
            {
                var columns = GetColumnNames(dbConnector, tableName)
                    .Where(column => !string.IsNullOrWhiteSpace(column))
                    .ToList();
                if (!columns.Any(column => string.Equals(column, "OperationPlanID", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var insertColumns = string.Join(", ", columns.Select(QuoteIdentifier));
                var selectColumns = string.Join(", ", columns.Select(column =>
                    string.Equals(column, "OperationPlanID", StringComparison.OrdinalIgnoreCase)
                        ? "@targetOperationPlanID"
                        : QuoteIdentifier(column)));
                dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {QuoteIdentifier(tableName)} ({insertColumns})
                       SELECT {selectColumns}
                       FROM {QuoteIdentifier(tableName)}
                       WHERE InstanceID = @instanceID
                         AND StationSchemeID = @stationSchemeID
                         AND OperationPlanID = @sourceOperationPlanID",
                    new
                    {
                        instanceID,
                        stationSchemeID,
                        sourceOperationPlanID,
                        targetOperationPlanID
                    });
            }
        }

        private static void UpdateOperationPlanIDInScopedTables(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string originalOperationPlanID,
            string operationPlanID)
        {
            foreach (var tableName in OperationPlanScopedTableNames)
            {
                dbConnector.ExecuteNonQuery(
                    $@"UPDATE {QuoteIdentifier(tableName)}
                       SET OperationPlanID = @operationPlanID
                       WHERE InstanceID = @instanceID
                         AND StationSchemeID = @stationSchemeID
                         AND OperationPlanID = @originalOperationPlanID",
                    new { instanceID, stationSchemeID, originalOperationPlanID, operationPlanID });
            }
        }

        private void DeleteOperationBottleneckSummaryCategories(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID)
        {
            dbConnector.ExecuteNonQuery(
                $@"DELETE FROM {QuoteIdentifier("operationbottlenecksummarycategoryroute")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID",
                new { instanceID, stationSchemeID, operationPlanID });
            dbConnector.ExecuteNonQuery(
                $@"DELETE FROM {QuoteIdentifier("operationbottlenecksummarycategory")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID",
                new { instanceID, stationSchemeID, operationPlanID });
        }

        private void InsertOperationBottleneckSummaryCategories(
            DBConnector dbConnector,
            IEnumerable<OperationBottleneckSummaryCategoryRow> categories)
        {
            foreach (var category in categories)
            {
                dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {QuoteIdentifier("operationbottlenecksummarycategory")} (
                           InstanceID, StationSchemeID, OperationPlanID, CategoryID, Name, SortOrder)
                       VALUES (
                           @InstanceID, @StationSchemeID, @OperationPlanID, @CategoryID, @Name, @SortOrder)",
                    category);

                var routeIDs = ParseRouteIDList(category.RouteIDList);
                for (var index = 0; index < routeIDs.Count; index++)
                {
                    dbConnector.ExecuteNonQuery(
                        $@"INSERT INTO {QuoteIdentifier("operationbottlenecksummarycategoryroute")} (
                               InstanceID, StationSchemeID, OperationPlanID, CategoryID, RouteID, SortOrder)
                           VALUES (
                               @InstanceID, @StationSchemeID, @OperationPlanID, @CategoryID, @RouteID, @SortOrder)",
                        new OperationBottleneckSummaryCategoryRouteRow
                        {
                            InstanceID = category.InstanceID,
                            StationSchemeID = category.StationSchemeID,
                            OperationPlanID = category.OperationPlanID,
                            CategoryID = category.CategoryID,
                            RouteID = routeIDs[index],
                            SortOrder = index
                        });
                }
            }
        }

        private void DeleteOperationAnalysisResult(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID)
        {
            foreach (var tableName in new[]
            {
                "operationthroughputsummaryroute",
                "operationthroughputsummaryresult",
                "operationbottleneckanalysisresult",
                "operationoccupationtimecell",
                "operationoccupationtimerow",
                "operationanalysiscell",
                "operationanalysismeta"
            })
            {
                dbConnector.ExecuteNonQuery(
                    $@"DELETE FROM {QuoteIdentifier(tableName)}
                       WHERE InstanceID = @instanceID
                         AND StationSchemeID = @stationSchemeID
                         AND OperationPlanID = @operationPlanID",
                    new { instanceID, stationSchemeID, operationPlanID });
            }
        }

        private void InsertOperationAnalysisResult(
            DBConnector dbConnector,
            OperationAnalysisResultResponse snapshot)
        {
            var updatedDate = snapshot.UpdatedDate ?? DateTime.Now;

            dbConnector.ExecuteNonQuery(
                $@"INSERT INTO {QuoteIdentifier("operationanalysismeta")} (
                       InstanceID, StationSchemeID, OperationPlanID, TotalTimeSeconds, UpdatedDate)
                   VALUES (
                       @InstanceID, @StationSchemeID, @OperationPlanID, @TotalTimeSeconds, @UpdatedDate)",
                new OperationAnalysisMetaRow
                {
                    InstanceID = snapshot.InstanceID,
                    StationSchemeID = snapshot.StationSchemeID,
                    OperationPlanID = snapshot.OperationPlanID,
                    TotalTimeSeconds = snapshot.TotalTimeSeconds,
                    UpdatedDate = updatedDate
                });

            for (var index = 0; index < snapshot.Cells.Count; index++)
            {
                var cell = snapshot.Cells[index];
                dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {QuoteIdentifier("operationanalysiscell")} (
                           InstanceID, StationSchemeID, OperationPlanID, CellID, CellName, SortOrder)
                       VALUES (
                           @InstanceID, @StationSchemeID, @OperationPlanID, @CellID, @CellName, @SortOrder)",
                    new OperationAnalysisCellRow
                    {
                        InstanceID = snapshot.InstanceID,
                        StationSchemeID = snapshot.StationSchemeID,
                        OperationPlanID = snapshot.OperationPlanID,
                        CellID = cell.ID,
                        CellName = cell.Name,
                        SortOrder = index
                    });
            }

            var usedOccupationRowKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < snapshot.OccupationTimeTableRows.Count; index++)
            {
                var row = snapshot.OccupationTimeTableRows[index];
                var rowKey = BuildOperationOccupationRowKey(row, index, usedOccupationRowKeys);
                dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {QuoteIdentifier("operationoccupationtimerow")} (
                           InstanceID, StationSchemeID, OperationPlanID, RowKey, RowType, SequenceText, RouteID, RouteName, OperationCountText, SortOrder)
                       VALUES (
                           @InstanceID, @StationSchemeID, @OperationPlanID, @RowKey, @RowType, @SequenceText, @RouteID, @RouteName, @OperationCountText, @SortOrder)",
                    new OperationOccupationTimeTableResultRow
                    {
                        InstanceID = snapshot.InstanceID,
                        StationSchemeID = snapshot.StationSchemeID,
                        OperationPlanID = snapshot.OperationPlanID,
                        RowKey = rowKey,
                        RowType = row.RowType,
                        SequenceText = row.Sequence,
                        RouteID = row.RouteID,
                        RouteName = row.RouteName,
                        OperationCountText = row.OperationCount,
                        SortOrder = index
                    });

                var cellIDs = row.CellDurations.Keys
                    .Concat(row.InterruptCellDurations.Keys)
                    .Where(cellID => !string.IsNullOrWhiteSpace(cellID))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                foreach (var cellID in cellIDs)
                {
                    var hasCellValue = row.CellDurations.TryGetValue(cellID, out var cellValue);
                    var hasInterruptCellValue = row.InterruptCellDurations.TryGetValue(cellID, out var interruptCellValue);
                    dbConnector.ExecuteNonQuery(
                        $@"INSERT INTO {QuoteIdentifier("operationoccupationtimecell")} (
                               InstanceID, StationSchemeID, OperationPlanID, RowKey, CellID, CellValue, InterruptCellValue)
                            VALUES (
                               @InstanceID, @StationSchemeID, @OperationPlanID, @RowKey, @CellID, @CellValue, @InterruptCellValue)",
                        new OperationOccupationTimeCellValueRow
                        {
                            InstanceID = snapshot.InstanceID,
                            StationSchemeID = snapshot.StationSchemeID,
                            OperationPlanID = snapshot.OperationPlanID,
                            RowKey = rowKey,
                            CellID = cellID,
                            CellValue = hasCellValue ? cellValue : interruptCellValue,
                            InterruptCellValue = hasInterruptCellValue ? interruptCellValue : null
                        });
                }
            }

            for (var index = 0; index < snapshot.BottleneckAnalysisRows.Count; index++)
            {
                var row = snapshot.BottleneckAnalysisRows[index];
                dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {QuoteIdentifier("operationbottleneckanalysisresult")} (
                           InstanceID, StationSchemeID, OperationPlanID, RouteID, RouteName, OperationCount, BottleneckCellID, BottleneckCellName,
                           BottleneckUtilization, ThroughputCapacity, SortOrder)
                       VALUES (
                           @InstanceID, @StationSchemeID, @OperationPlanID, @RouteID, @RouteName, @OperationCount, @BottleneckCellID, @BottleneckCellName,
                           @BottleneckUtilization, @ThroughputCapacity, @SortOrder)",
                    new OperationBottleneckAnalysisResultRow
                    {
                        InstanceID = snapshot.InstanceID,
                        StationSchemeID = snapshot.StationSchemeID,
                        OperationPlanID = snapshot.OperationPlanID,
                        RouteID = row.RouteID,
                        RouteName = row.RouteName,
                        OperationCount = row.OperationCount,
                        BottleneckCellID = row.BottleneckCellID,
                        BottleneckCellName = row.BottleneckCellName,
                        BottleneckUtilization = row.BottleneckUtilization,
                        ThroughputCapacity = row.ThroughputCapacity,
                        SortOrder = index
                    });
            }

            for (var index = 0; index < snapshot.ThroughputSummaryRows.Count; index++)
            {
                var row = snapshot.ThroughputSummaryRows[index];
                dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {QuoteIdentifier("operationthroughputsummaryresult")} (
                           InstanceID, StationSchemeID, OperationPlanID, CategoryID, GroupKey, GroupText, RouteCount, OperationCount,
                           CapacityTotal, CapacityAverage, SortOrder)
                       VALUES (
                           @InstanceID, @StationSchemeID, @OperationPlanID, @CategoryID, @GroupKey, @GroupText, @RouteCount, @OperationCount,
                           @CapacityTotal, @CapacityAverage, @SortOrder)",
                    new OperationThroughputSummaryResultRow
                    {
                        InstanceID = snapshot.InstanceID,
                        StationSchemeID = snapshot.StationSchemeID,
                        OperationPlanID = snapshot.OperationPlanID,
                        CategoryID = row.CategoryID,
                        GroupKey = row.GroupKey,
                        GroupText = row.GroupText,
                        RouteCount = row.RouteCount,
                        OperationCount = row.OperationCount,
                        CapacityTotal = row.CapacityTotal,
                        CapacityAverage = row.CapacityAverage,
                        SortOrder = index
                    });

                for (var routeIndex = 0; routeIndex < row.RouteIDs.Count; routeIndex++)
                {
                    dbConnector.ExecuteNonQuery(
                        $@"INSERT INTO {QuoteIdentifier("operationthroughputsummaryroute")} (
                               InstanceID, StationSchemeID, OperationPlanID, CategoryID, RouteID, SortOrder)
                           VALUES (
                               @InstanceID, @StationSchemeID, @OperationPlanID, @CategoryID, @RouteID, @SortOrder)",
                        new OperationThroughputSummaryRouteRow
                        {
                            InstanceID = snapshot.InstanceID,
                            StationSchemeID = snapshot.StationSchemeID,
                            OperationPlanID = snapshot.OperationPlanID,
                            CategoryID = row.CategoryID,
                            RouteID = row.RouteIDs[routeIndex],
                            SortOrder = routeIndex
                        });
                }
            }
        }

        private void DeleteTrainOperationPlan(DBConnector dbConnector, string instanceID, string stationSchemeID, string operationPlanID)
        {
            dbConnector.ExecuteNonQuery(
                $@"DELETE FROM {QuoteIdentifier("movement")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID",
                new { instanceID, stationSchemeID, operationPlanID });
            dbConnector.ExecuteNonQuery(
                $@"DELETE FROM {QuoteIdentifier("train")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID",
                new { instanceID, stationSchemeID, operationPlanID });
        }

        private void InsertTrainOperationPlan(DBConnector dbConnector, TrainOperationPlanResponse plan)
        {
            foreach (var train in plan.Trains)
            {
                dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {QuoteIdentifier("train")} (
                           InstanceID, StationSchemeID, OperationPlanID, {QuoteIdentifier("ID")}, TrainTemplateID, TrainNumber, Name, TrainType, IsFixedOperation)
                       VALUES (
                           @InstanceID, @StationSchemeID, @OperationPlanID, @ID, @TrainTemplateID, @TrainNumber, @Name, @TrainType, @IsFixedOperation)",
                    train);
            }

            foreach (var movement in plan.Movements)
            {
                dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {QuoteIdentifier("movement")} (
                           InstanceID, StationSchemeID, OperationPlanID, TrainID, TrainTemplateID, MovementID, Name, RouteIDList,
                           MinDuration, EarliestStartTime, LatestEndTime,
                           {QuoteIdentifier("Route")}, Tag, SortOrder)
                       VALUES (
                           @InstanceID, @StationSchemeID, @OperationPlanID, @TrainID, @TrainTemplateID, @MovementID, @Name, @RouteIDList,
                           @MinDuration, @EarliestStartTime, @LatestEndTime,
                           @Route, @Tag, @SortOrder)",
                    movement);
            }
        }

        private void InsertTrain(DBConnector dbConnector, TrainRow train)
        {
            dbConnector.ExecuteNonQuery(
                $@"INSERT INTO {QuoteIdentifier("train")} (
                       InstanceID, StationSchemeID, OperationPlanID, {QuoteIdentifier("ID")}, TrainTemplateID, TrainNumber, Name, TrainType, IsFixedOperation)
                   VALUES (
                       @InstanceID, @StationSchemeID, @OperationPlanID, @ID, @TrainTemplateID, @TrainNumber, @Name, @TrainType, @IsFixedOperation)",
                train);
        }

        private void UpdateTrain(DBConnector dbConnector, TrainRow train)
        {
            dbConnector.ExecuteNonQuery(
                $@"UPDATE {QuoteIdentifier("train")}
                   SET TrainTemplateID = @TrainTemplateID,
                       TrainNumber = @TrainNumber,
                       Name = @Name,
                       TrainType = @TrainType,
                       IsFixedOperation = @IsFixedOperation
                    WHERE InstanceID = @InstanceID
                      AND StationSchemeID = @StationSchemeID
                      AND OperationPlanID = @OperationPlanID
                      AND {QuoteIdentifier("ID")} = @ID",
                train);
        }

        private void InsertMovement(DBConnector dbConnector, MovementRow movement)
        {
            dbConnector.ExecuteNonQuery(
                $@"INSERT INTO {QuoteIdentifier("movement")} (
                       InstanceID, StationSchemeID, OperationPlanID, TrainID, TrainTemplateID, MovementID, Name, RouteIDList,
                       MinDuration, EarliestStartTime, LatestEndTime,
                       {QuoteIdentifier("Route")}, Tag, SortOrder)
                   VALUES (
                       @InstanceID, @StationSchemeID, @OperationPlanID, @TrainID, @TrainTemplateID, @MovementID, @Name, @RouteIDList,
                       @MinDuration, @EarliestStartTime, @LatestEndTime,
                       @Route, @Tag, @SortOrder)",
                movement);
        }

        private void UpdateMovement(DBConnector dbConnector, MovementRow movement)
        {
            dbConnector.ExecuteNonQuery(
                $@"UPDATE {QuoteIdentifier("movement")}
                   SET TrainTemplateID = @TrainTemplateID,
                       Name = @Name,
                       RouteIDList = @RouteIDList,
                       MinDuration = @MinDuration,
                       EarliestStartTime = @EarliestStartTime,
                       LatestEndTime = @LatestEndTime,
                       {QuoteIdentifier("Route")} = @Route,
                       Tag = @Tag,
                       SortOrder = @SortOrder
                    WHERE InstanceID = @InstanceID
                      AND StationSchemeID = @StationSchemeID
                      AND OperationPlanID = @OperationPlanID
                      AND TrainID = @TrainID
                      AND MovementID = @MovementID",
                movement);
        }

        private static List<MovementOrderItem> NormalizeMovementOrderItems(IEnumerable<MovementOrderItem>? items)
        {
            var result = new List<MovementOrderItem>();
            var seenMovementIDs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var index = 0;
            foreach (var item in items ?? Enumerable.Empty<MovementOrderItem>())
            {
                var movementID = item.MovementID?.Trim();
                if (string.IsNullOrWhiteSpace(movementID) || !seenMovementIDs.Add(movementID))
                {
                    continue;
                }

                result.Add(new MovementOrderItem
                {
                    MovementID = movementID,
                    SortOrder = item.SortOrder ?? index
                });
                index++;
            }

            return result;
        }

        private int GetNextMovementTemplateSortOrder(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID,
            string trainTemplateID)
        {
            var existing = LoadMovementTemplates(dbConnector, instanceID, stationSchemeID, operationPlanID, trainTemplateID);
            return existing.Count == 0
                ? 0
                : existing.Select(item => item.SortOrder ?? -1).DefaultIfEmpty(-1).Max() + 1;
        }

        private int GetNextMovementSortOrder(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID,
            string trainID)
        {
            var existing = LoadMovements(dbConnector, instanceID, stationSchemeID, operationPlanID)
                .Where(item => string.Equals(item.TrainID, trainID, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return existing.Count == 0
                ? 0
                : existing.Select(item => item.SortOrder ?? -1).DefaultIfEmpty(-1).Max() + 1;
        }

        private static void UpdateMovementTemplateSortOrders(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID,
            string trainTemplateID,
            IReadOnlyList<MovementOrderItem> items)
        {
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                dbConnector.ExecuteNonQuery(
                    $@"UPDATE {QuoteIdentifier("movementtemplate")}
                       SET SortOrder = @sortOrder
                       WHERE InstanceID = @instanceID
                         AND StationSchemeID = @stationSchemeID
                         AND OperationPlanID = @operationPlanID
                         AND TrainTemplateID = @trainTemplateID
                         AND MovementID = @movementID",
                    new
                    {
                        instanceID,
                        stationSchemeID,
                        operationPlanID,
                        trainTemplateID,
                        movementID = item.MovementID,
                        sortOrder = item.SortOrder ?? index
                    });
            }
        }

        private static void UpdateMovementSortOrders(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID,
            string trainID,
            IReadOnlyList<MovementOrderItem> items)
        {
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                dbConnector.ExecuteNonQuery(
                    $@"UPDATE {QuoteIdentifier("movement")}
                       SET SortOrder = @sortOrder
                       WHERE InstanceID = @instanceID
                         AND StationSchemeID = @stationSchemeID
                         AND OperationPlanID = @operationPlanID
                         AND TrainID = @trainID
                         AND MovementID = @movementID",
                    new
                    {
                        instanceID,
                        stationSchemeID,
                        operationPlanID,
                        trainID,
                        movementID = item.MovementID,
                        sortOrder = item.SortOrder ?? index
                    });
            }
        }

        private bool TrainTemplateExists(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID,
            string trainTemplateID)
        {
            return (dbConnector.Query<TrainTemplateRow>(
                $@"SELECT TrainTemplateID
                   FROM {QuoteIdentifier("traintemplate")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                     AND TrainTemplateID = @trainTemplateID
                   LIMIT 1",
                new { instanceID, stationSchemeID, operationPlanID, trainTemplateID }) ?? new List<TrainTemplateRow>()).Any();
        }

        private bool MovementTemplateExists(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID,
            string trainTemplateID,
            string movementID)
        {
            return (dbConnector.Query<MovementTemplateRow>(
                $@"SELECT MovementID
                   FROM {QuoteIdentifier("movementtemplate")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                     AND TrainTemplateID = @trainTemplateID
                     AND MovementID = @movementID
                   LIMIT 1",
                new { instanceID, stationSchemeID, operationPlanID, trainTemplateID, movementID }) ?? new List<MovementTemplateRow>()).Any();
        }

        private bool OperationPlanExists(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID)
        {
            return (dbConnector.Query<OperationPlanRow>(
                $@"SELECT OperationPlanID
                   FROM {QuoteIdentifier("operationplan")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                   LIMIT 1",
                new { instanceID, stationSchemeID, operationPlanID }) ?? new List<OperationPlanRow>()).Any();
        }

        private bool OperationTrainExists(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID,
            string id)
        {
            return (dbConnector.Query<TrainRow>(
                $@"SELECT {QuoteIdentifier("ID")}
                   FROM {QuoteIdentifier("train")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                     AND {QuoteIdentifier("ID")} = @id
                   LIMIT 1",
                new { instanceID, stationSchemeID, operationPlanID, id }) ?? new List<TrainRow>()).Any();
        }

        private bool OperationMovementExists(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID,
            string trainID,
            string movementID)
        {
            return (dbConnector.Query<MovementRow>(
                $@"SELECT MovementID
                   FROM {QuoteIdentifier("movement")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                     AND TrainID = @trainID
                     AND MovementID = @movementID
                   LIMIT 1",
                new { instanceID, stationSchemeID, operationPlanID, trainID, movementID }) ?? new List<MovementRow>()).Any();
        }

        private void DeleteMovementTemplatesForTrain(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID,
            string trainTemplateID)
        {
            dbConnector.ExecuteNonQuery(
                $@"DELETE FROM {QuoteIdentifier("movementtemplate")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND OperationPlanID = @operationPlanID
                     AND TrainTemplateID = @trainTemplateID",
                new { instanceID, stationSchemeID, operationPlanID, trainTemplateID });
        }

        private string GenerateOperationPlanID(DBConnector dbConnector, string instanceID, string stationSchemeID)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var candidate = _snowflakeIdGenerator.NextIdString();
                if (!OperationPlanExists(dbConnector, instanceID, stationSchemeID, candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Failed to generate a unique operation plan ID.");
        }

        private string GenerateTrainTemplateID(DBConnector dbConnector, string instanceID, string stationSchemeID, string operationPlanID)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var candidate = _snowflakeIdGenerator.NextIdString();
                if (!TrainTemplateExists(dbConnector, instanceID, stationSchemeID, operationPlanID, candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Failed to generate a unique train template ID.");
        }

        private string GenerateMovementID(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID,
            string trainTemplateID)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var candidate = _snowflakeIdGenerator.NextIdString();
                if (!MovementTemplateExists(dbConnector, instanceID, stationSchemeID, operationPlanID, trainTemplateID, candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Failed to generate a unique movement template ID.");
        }

        private string GenerateOperationTrainID(DBConnector dbConnector, string instanceID, string stationSchemeID, string operationPlanID)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var candidate = _snowflakeIdGenerator.NextIdString();
                if (!OperationTrainExists(dbConnector, instanceID, stationSchemeID, operationPlanID, candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Failed to generate a unique train ID.");
        }

        private string GenerateOperationTrainNumber(DBConnector dbConnector, string instanceID, string stationSchemeID, string operationPlanID)
        {
            var maxTrainNumber = LoadTrains(dbConnector, instanceID, stationSchemeID, operationPlanID)
                .Select(train => GetTrainNumberSortValue(train.TrainNumber))
                .Where(value => value != int.MaxValue)
                .DefaultIfEmpty(0)
                .Max();
            return (maxTrainNumber + 1).ToString();
        }

        private string GenerateOperationMovementID(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string operationPlanID,
            string trainID)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var candidate = _snowflakeIdGenerator.NextIdString();
                if (!OperationMovementExists(dbConnector, instanceID, stationSchemeID, operationPlanID, trainID, candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Failed to generate a unique movement ID.");
        }

        private static string QuoteIdentifier(string identifier)
        {
            var escapedIdentifier = identifier.Replace("\"", "\"\"", StringComparison.Ordinal);
            if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
            {
                return $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";
            }

            return $"\"{escapedIdentifier}\"";
        }

        private static void EnsureOperationPlanTemplateSchema(DBConnector dbConnector)
        {
            EnsureOperationPlanObjectSchema(dbConnector);
            EnsureTrainTemplateSchema(dbConnector);
            EnsureMovementTemplateSchema(dbConnector);
        }

        private static void EnsureTrainOperationPlanSchema(DBConnector dbConnector)
        {
            EnsureOperationPlanTemplateSchema(dbConnector);
            EnsureTrainSchema(dbConnector);
            EnsureMovementSchema(dbConnector);
            EnsureOperationBottleneckSummaryCategorySchema(dbConnector);
            EnsureOperationAnalysisResultSchema(dbConnector);
        }

        private static void EnsureOperationPlanObjectSchema(DBConnector dbConnector)
        {
            var tableName = QuoteIdentifier("operationplan");
            if (!TableExists(dbConnector, "operationplan"))
            {
                if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("OperationPlanID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Name")} VARCHAR(100) NULL,
                            {QuoteIdentifier("Description")} VARCHAR(500) NULL,
                            {QuoteIdentifier("SortOrder")} INT NULL,
                            {QuoteIdentifier("CreatedDate")} DATETIME NULL,
                            {QuoteIdentifier("UpdatedDate")} DATETIME NULL
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                            {QuoteIdentifier("OperationPlanID")} TEXT NULL,
                            {QuoteIdentifier("Name")} TEXT NULL,
                            {QuoteIdentifier("Description")} TEXT NULL,
                            {QuoteIdentifier("SortOrder")} INTEGER NULL,
                            {QuoteIdentifier("CreatedDate")} DATETIME NULL,
                            {QuoteIdentifier("UpdatedDate")} DATETIME NULL
                        )");
                }

                return;
            }

            var shortTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(50) NULL" : "TEXT NULL";
            var nameTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(100) NULL" : "TEXT NULL";
            var descriptionTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(500) NULL" : "TEXT NULL";
            var intType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "INT NULL" : "INTEGER NULL";
            EnsureColumns(dbConnector, "operationplan", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["InstanceID"] = shortTextType,
                ["StationSchemeID"] = shortTextType,
                ["OperationPlanID"] = shortTextType,
                ["Name"] = nameTextType,
                ["Description"] = descriptionTextType,
                ["SortOrder"] = intType,
                ["CreatedDate"] = "DATETIME NULL",
                ["UpdatedDate"] = "DATETIME NULL"
            });
        }

        private static void EnsureTrainTemplateSchema(DBConnector dbConnector)
        {
            var tableName = QuoteIdentifier("traintemplate");
            if (!TableExists(dbConnector, "traintemplate"))
            {
                if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("OperationPlanID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("TrainTemplateID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Name")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Type")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Number")} INT NULL,
                            {QuoteIdentifier("IsFixedOperation")} TINYINT NULL
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                            {QuoteIdentifier("OperationPlanID")} TEXT NULL,
                            {QuoteIdentifier("TrainTemplateID")} TEXT NULL,
                            {QuoteIdentifier("Name")} TEXT NULL,
                            {QuoteIdentifier("Type")} TEXT NULL,
                            {QuoteIdentifier("Number")} INTEGER NULL,
                            {QuoteIdentifier("IsFixedOperation")} INTEGER NULL
                        )");
                }

                return;
            }

            var textType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(50) NULL" : "TEXT NULL";
            var intType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "INT NULL" : "INTEGER NULL";
            var boolType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "TINYINT NULL" : "INTEGER NULL";
            EnsureColumns(dbConnector, "traintemplate", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["InstanceID"] = textType,
                ["StationSchemeID"] = textType,
                ["OperationPlanID"] = textType,
                ["TrainTemplateID"] = textType,
                ["Name"] = textType,
                ["Type"] = textType,
                ["Number"] = intType,
                ["IsFixedOperation"] = boolType
            });
            BackfillOperationPlanID(dbConnector, "traintemplate");
        }

        private static void EnsureMovementTemplateSchema(DBConnector dbConnector)
        {
            var tableName = QuoteIdentifier("movementtemplate");
            if (!TableExists(dbConnector, "movementtemplate"))
            {
                if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("OperationPlanID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("TrainTemplateID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("MovementID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Name")} VARCHAR(50) NULL,
                            {QuoteIdentifier("RouteIDList")} LONGTEXT NULL,
                            {QuoteIdentifier("MinDuration")} INT NULL,
                            {QuoteIdentifier("SortOrder")} INT NULL
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                            {QuoteIdentifier("OperationPlanID")} TEXT NULL,
                            {QuoteIdentifier("TrainTemplateID")} TEXT NULL,
                            {QuoteIdentifier("MovementID")} TEXT NULL,
                            {QuoteIdentifier("Name")} TEXT NULL,
                            {QuoteIdentifier("RouteIDList")} TEXT NULL,
                            {QuoteIdentifier("MinDuration")} INTEGER NULL,
                            {QuoteIdentifier("SortOrder")} INTEGER NULL
                        )");
                }

                return;
            }

            var shortTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(50) NULL" : "TEXT NULL";
            var longTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "LONGTEXT NULL" : "TEXT NULL";
            var intType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "INT NULL" : "INTEGER NULL";
            EnsureColumns(dbConnector, "movementtemplate", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["InstanceID"] = shortTextType,
                ["StationSchemeID"] = shortTextType,
                ["OperationPlanID"] = shortTextType,
                ["TrainTemplateID"] = shortTextType,
                ["MovementID"] = shortTextType,
                ["Name"] = shortTextType,
                ["RouteIDList"] = longTextType,
                ["MinDuration"] = intType,
                ["SortOrder"] = intType
            });
            BackfillOperationPlanID(dbConnector, "movementtemplate");
            BackfillMovementTemplateSortOrder(dbConnector);
        }

        private static void EnsureTrainSchema(DBConnector dbConnector)
        {
            var tableName = QuoteIdentifier("train");
            if (!TableExists(dbConnector, "train"))
            {
                if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("OperationPlanID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("ID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("TrainTemplateID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("TrainNumber")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Name")} VARCHAR(50) NULL,
                            {QuoteIdentifier("TrainType")} VARCHAR(20) NULL,
                            {QuoteIdentifier("IsFixedOperation")} TINYINT NULL
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                            {QuoteIdentifier("OperationPlanID")} TEXT NULL,
                            {QuoteIdentifier("ID")} TEXT NULL,
                            {QuoteIdentifier("TrainTemplateID")} TEXT NULL,
                            {QuoteIdentifier("TrainNumber")} TEXT NULL,
                            {QuoteIdentifier("Name")} TEXT NULL,
                            {QuoteIdentifier("TrainType")} TEXT NULL,
                            {QuoteIdentifier("IsFixedOperation")} INTEGER NULL
                        )");
                }

                return;
            }

            var shortTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(50) NULL" : "TEXT NULL";
            var trainTypeTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(20) NULL" : "TEXT NULL";
            var boolType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "TINYINT NULL" : "INTEGER NULL";
            EnsureColumns(dbConnector, "train", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["InstanceID"] = shortTextType,
                ["StationSchemeID"] = shortTextType,
                ["OperationPlanID"] = shortTextType,
                ["ID"] = shortTextType,
                ["TrainTemplateID"] = shortTextType,
                ["TrainNumber"] = shortTextType,
                ["Name"] = shortTextType,
                ["TrainType"] = trainTypeTextType,
                ["IsFixedOperation"] = boolType
            });
            BackfillOperationPlanID(dbConnector, "train");
        }

        private static void EnsureMovementSchema(DBConnector dbConnector)
        {
            var tableName = QuoteIdentifier("movement");
            if (!TableExists(dbConnector, "movement"))
            {
                if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("OperationPlanID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("TrainID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("TrainTemplateID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("MovementID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Name")} VARCHAR(50) NULL,
                            {QuoteIdentifier("RouteIDList")} LONGTEXT NULL,
                            {QuoteIdentifier("MinDuration")} INT NULL,
                            {QuoteIdentifier("EarliestStartTime")} VARCHAR(50) NULL,
                            {QuoteIdentifier("LatestEndTime")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Route")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Tag")} VARCHAR(50) NULL,
                            {QuoteIdentifier("SortOrder")} INT NULL
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                            {QuoteIdentifier("OperationPlanID")} TEXT NULL,
                            {QuoteIdentifier("TrainID")} TEXT NULL,
                            {QuoteIdentifier("TrainTemplateID")} TEXT NULL,
                            {QuoteIdentifier("MovementID")} TEXT NULL,
                            {QuoteIdentifier("Name")} TEXT NULL,
                            {QuoteIdentifier("RouteIDList")} TEXT NULL,
                            {QuoteIdentifier("MinDuration")} INTEGER NULL,
                            {QuoteIdentifier("EarliestStartTime")} TEXT NULL,
                            {QuoteIdentifier("LatestEndTime")} TEXT NULL,
                            {QuoteIdentifier("Route")} TEXT NULL,
                            {QuoteIdentifier("Tag")} TEXT NULL,
                            {QuoteIdentifier("SortOrder")} INTEGER NULL
                        )");
                }

                return;
            }

            var shortTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(50) NULL" : "TEXT NULL";
            var longTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "LONGTEXT NULL" : "TEXT NULL";
            var intType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "INT NULL" : "INTEGER NULL";
            EnsureColumns(dbConnector, "movement", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["InstanceID"] = shortTextType,
                ["StationSchemeID"] = shortTextType,
                ["OperationPlanID"] = shortTextType,
                ["TrainID"] = shortTextType,
                ["TrainTemplateID"] = shortTextType,
                ["MovementID"] = shortTextType,
                ["Name"] = shortTextType,
                ["RouteIDList"] = longTextType,
                ["MinDuration"] = intType,
                ["EarliestStartTime"] = shortTextType,
                ["LatestEndTime"] = shortTextType,
                ["Route"] = shortTextType,
                ["Tag"] = shortTextType,
                ["SortOrder"] = intType
            });
            BackfillOperationPlanID(dbConnector, "movement");
            BackfillMovementSortOrder(dbConnector);
        }

        private static void EnsureOperationBottleneckSummaryCategorySchema(DBConnector dbConnector)
        {
            var categoryTableName = QuoteIdentifier("operationbottlenecksummarycategory");
            if (!TableExists(dbConnector, "operationbottlenecksummarycategory"))
            {
                if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {categoryTableName} (
                            {QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("OperationPlanID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("CategoryID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Name")} VARCHAR(100) NULL,
                            {QuoteIdentifier("SortOrder")} INT NULL
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {categoryTableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                            {QuoteIdentifier("OperationPlanID")} TEXT NULL,
                            {QuoteIdentifier("CategoryID")} TEXT NULL,
                            {QuoteIdentifier("Name")} TEXT NULL,
                            {QuoteIdentifier("SortOrder")} INTEGER NULL
                        )");
                }
            }

            var shortTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(50) NULL" : "TEXT NULL";
            var nameTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(100) NULL" : "TEXT NULL";
            var intType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "INT NULL" : "INTEGER NULL";
            EnsureColumns(dbConnector, "operationbottlenecksummarycategory", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["InstanceID"] = shortTextType,
                ["StationSchemeID"] = shortTextType,
                ["OperationPlanID"] = shortTextType,
                ["CategoryID"] = shortTextType,
                ["Name"] = nameTextType,
                ["SortOrder"] = intType
            });
            BackfillOperationPlanID(dbConnector, "operationbottlenecksummarycategory");

            var categoryRouteTableName = QuoteIdentifier("operationbottlenecksummarycategoryroute");
            if (!TableExists(dbConnector, "operationbottlenecksummarycategoryroute"))
            {
                if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {categoryRouteTableName} (
                            {QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("OperationPlanID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("CategoryID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("RouteID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("SortOrder")} INT NULL
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {categoryRouteTableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                            {QuoteIdentifier("OperationPlanID")} TEXT NULL,
                            {QuoteIdentifier("CategoryID")} TEXT NULL,
                            {QuoteIdentifier("RouteID")} TEXT NULL,
                            {QuoteIdentifier("SortOrder")} INTEGER NULL
                        )");
                }
            }

            EnsureColumns(dbConnector, "operationbottlenecksummarycategoryroute", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["InstanceID"] = shortTextType,
                ["StationSchemeID"] = shortTextType,
                ["OperationPlanID"] = shortTextType,
                ["CategoryID"] = shortTextType,
                ["RouteID"] = shortTextType,
                ["SortOrder"] = intType
            });
            BackfillOperationPlanID(dbConnector, "operationbottlenecksummarycategoryroute");
        }

        private static void EnsureOperationAnalysisResultSchema(DBConnector dbConnector)
        {
            var shortTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(50) NULL" : "TEXT NULL";
            var rowKeyTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(100) NULL" : "TEXT NULL";
            var routeNameTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(200) NULL" : "TEXT NULL";
            var rowTypeTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(20) NULL" : "TEXT NULL";
            var longTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "LONGTEXT NULL" : "TEXT NULL";
            var intType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "INT NULL" : "INTEGER NULL";
            var doubleType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "DOUBLE NULL" : "REAL NULL";

            void EnsureResultTable(
                string tableName,
                string mysqlColumns,
                string sqliteColumns,
                IReadOnlyDictionary<string, string> requiredColumns)
            {
                var quotedTableName = QuoteIdentifier(tableName);
                if (!TableExists(dbConnector, tableName))
                {
                    if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
                    {
                        dbConnector.ExecuteNonQuery(
                            $@"CREATE TABLE IF NOT EXISTS {quotedTableName} (
                                {mysqlColumns}
                            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                    }
                    else
                    {
                        dbConnector.ExecuteNonQuery(
                            $@"CREATE TABLE IF NOT EXISTS {quotedTableName} (
                                {sqliteColumns}
                            )");
                    }

                    return;
                }

                EnsureColumns(dbConnector, tableName, requiredColumns);
                BackfillOperationPlanID(dbConnector, tableName);
            }

            EnsureResultTable(
                "operationanalysismeta",
                $@"{QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("OperationPlanID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("TotalTimeSeconds")} INT NULL,
                   {QuoteIdentifier("UpdatedDate")} DATETIME NULL",
                $@"{QuoteIdentifier("InstanceID")} TEXT NULL,
                   {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                   {QuoteIdentifier("OperationPlanID")} TEXT NULL,
                   {QuoteIdentifier("TotalTimeSeconds")} INTEGER NULL,
                   {QuoteIdentifier("UpdatedDate")} DATETIME NULL",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["InstanceID"] = shortTextType,
                    ["StationSchemeID"] = shortTextType,
                    ["OperationPlanID"] = shortTextType,
                    ["TotalTimeSeconds"] = intType,
                    ["UpdatedDate"] = "DATETIME NULL"
                });

            EnsureResultTable(
                "operationanalysiscell",
                $@"{QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("OperationPlanID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("CellID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("CellName")} VARCHAR(100) NULL,
                   {QuoteIdentifier("SortOrder")} INT NULL",
                $@"{QuoteIdentifier("InstanceID")} TEXT NULL,
                   {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                   {QuoteIdentifier("OperationPlanID")} TEXT NULL,
                   {QuoteIdentifier("CellID")} TEXT NULL,
                   {QuoteIdentifier("CellName")} TEXT NULL,
                   {QuoteIdentifier("SortOrder")} INTEGER NULL",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["InstanceID"] = shortTextType,
                    ["StationSchemeID"] = shortTextType,
                    ["OperationPlanID"] = shortTextType,
                    ["CellID"] = shortTextType,
                    ["CellName"] = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(100) NULL" : "TEXT NULL",
                    ["SortOrder"] = intType
                });

            EnsureResultTable(
                "operationoccupationtimerow",
                $@"{QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("OperationPlanID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("RowKey")} VARCHAR(100) NULL,
                   {QuoteIdentifier("RowType")} VARCHAR(20) NULL,
                   {QuoteIdentifier("SequenceText")} VARCHAR(50) NULL,
                   {QuoteIdentifier("RouteID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("RouteName")} VARCHAR(200) NULL,
                   {QuoteIdentifier("OperationCountText")} VARCHAR(50) NULL,
                   {QuoteIdentifier("SortOrder")} INT NULL",
                $@"{QuoteIdentifier("InstanceID")} TEXT NULL,
                   {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                   {QuoteIdentifier("OperationPlanID")} TEXT NULL,
                   {QuoteIdentifier("RowKey")} TEXT NULL,
                   {QuoteIdentifier("RowType")} TEXT NULL,
                   {QuoteIdentifier("SequenceText")} TEXT NULL,
                   {QuoteIdentifier("RouteID")} TEXT NULL,
                   {QuoteIdentifier("RouteName")} TEXT NULL,
                   {QuoteIdentifier("OperationCountText")} TEXT NULL,
                   {QuoteIdentifier("SortOrder")} INTEGER NULL",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["InstanceID"] = shortTextType,
                    ["StationSchemeID"] = shortTextType,
                    ["OperationPlanID"] = shortTextType,
                    ["RowKey"] = rowKeyTextType,
                    ["RowType"] = rowTypeTextType,
                    ["SequenceText"] = shortTextType,
                    ["RouteID"] = shortTextType,
                    ["RouteName"] = routeNameTextType,
                    ["OperationCountText"] = shortTextType,
                    ["SortOrder"] = intType
                });

            EnsureResultTable(
                "operationoccupationtimecell",
                $@"{QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("OperationPlanID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("RowKey")} VARCHAR(100) NULL,
                   {QuoteIdentifier("CellID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("CellValue")} DOUBLE NULL,
                   {QuoteIdentifier("InterruptCellValue")} DOUBLE NULL",
                $@"{QuoteIdentifier("InstanceID")} TEXT NULL,
                   {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                   {QuoteIdentifier("OperationPlanID")} TEXT NULL,
                   {QuoteIdentifier("RowKey")} TEXT NULL,
                   {QuoteIdentifier("CellID")} TEXT NULL,
                   {QuoteIdentifier("CellValue")} REAL NULL,
                   {QuoteIdentifier("InterruptCellValue")} REAL NULL",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["InstanceID"] = shortTextType,
                    ["StationSchemeID"] = shortTextType,
                    ["OperationPlanID"] = shortTextType,
                    ["RowKey"] = rowKeyTextType,
                    ["CellID"] = shortTextType,
                    ["CellValue"] = doubleType,
                    ["InterruptCellValue"] = doubleType
                });

            EnsureResultTable(
                "operationoccupationtimesubtable",
                $@"{QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("OperationPlanID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("SubTableID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("SubTableName")} VARCHAR(100) NULL,
                   {QuoteIdentifier("CellIDList")} LONGTEXT NULL,
                   {QuoteIdentifier("SortOrder")} INT NULL",
                $@"{QuoteIdentifier("InstanceID")} TEXT NULL,
                   {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                   {QuoteIdentifier("OperationPlanID")} TEXT NULL,
                   {QuoteIdentifier("SubTableID")} TEXT NULL,
                   {QuoteIdentifier("SubTableName")} TEXT NULL,
                   {QuoteIdentifier("CellIDList")} TEXT NULL,
                   {QuoteIdentifier("SortOrder")} INTEGER NULL",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["InstanceID"] = shortTextType,
                    ["StationSchemeID"] = shortTextType,
                    ["OperationPlanID"] = shortTextType,
                    ["SubTableID"] = shortTextType,
                    ["SubTableName"] = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(100) NULL" : "TEXT NULL",
                    ["CellIDList"] = longTextType,
                    ["SortOrder"] = intType
                });

            EnsureResultTable(
                "operationbottleneckanalysisresult",
                $@"{QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("OperationPlanID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("RouteID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("RouteName")} VARCHAR(200) NULL,
                   {QuoteIdentifier("OperationCount")} INT NULL,
                   {QuoteIdentifier("BottleneckCellID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("BottleneckCellName")} VARCHAR(100) NULL,
                   {QuoteIdentifier("BottleneckUtilization")} DOUBLE NULL,
                   {QuoteIdentifier("ThroughputCapacity")} DOUBLE NULL,
                   {QuoteIdentifier("SortOrder")} INT NULL",
                $@"{QuoteIdentifier("InstanceID")} TEXT NULL,
                   {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                   {QuoteIdentifier("OperationPlanID")} TEXT NULL,
                   {QuoteIdentifier("RouteID")} TEXT NULL,
                   {QuoteIdentifier("RouteName")} TEXT NULL,
                   {QuoteIdentifier("OperationCount")} INTEGER NULL,
                   {QuoteIdentifier("BottleneckCellID")} TEXT NULL,
                   {QuoteIdentifier("BottleneckCellName")} TEXT NULL,
                   {QuoteIdentifier("BottleneckUtilization")} REAL NULL,
                   {QuoteIdentifier("ThroughputCapacity")} REAL NULL,
                   {QuoteIdentifier("SortOrder")} INTEGER NULL",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["InstanceID"] = shortTextType,
                    ["StationSchemeID"] = shortTextType,
                    ["OperationPlanID"] = shortTextType,
                    ["RouteID"] = shortTextType,
                    ["RouteName"] = routeNameTextType,
                    ["OperationCount"] = intType,
                    ["BottleneckCellID"] = shortTextType,
                    ["BottleneckCellName"] = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(100) NULL" : "TEXT NULL",
                    ["BottleneckUtilization"] = doubleType,
                    ["ThroughputCapacity"] = doubleType,
                    ["SortOrder"] = intType
                });

            EnsureResultTable(
                "operationthroughputsummaryresult",
                $@"{QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("OperationPlanID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("CategoryID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("GroupKey")} VARCHAR(50) NULL,
                   {QuoteIdentifier("GroupText")} VARCHAR(100) NULL,
                   {QuoteIdentifier("RouteCount")} INT NULL,
                   {QuoteIdentifier("OperationCount")} INT NULL,
                   {QuoteIdentifier("CapacityTotal")} DOUBLE NULL,
                   {QuoteIdentifier("CapacityAverage")} DOUBLE NULL,
                   {QuoteIdentifier("SortOrder")} INT NULL",
                $@"{QuoteIdentifier("InstanceID")} TEXT NULL,
                   {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                   {QuoteIdentifier("OperationPlanID")} TEXT NULL,
                   {QuoteIdentifier("CategoryID")} TEXT NULL,
                   {QuoteIdentifier("GroupKey")} TEXT NULL,
                   {QuoteIdentifier("GroupText")} TEXT NULL,
                   {QuoteIdentifier("RouteCount")} INTEGER NULL,
                   {QuoteIdentifier("OperationCount")} INTEGER NULL,
                   {QuoteIdentifier("CapacityTotal")} REAL NULL,
                   {QuoteIdentifier("CapacityAverage")} REAL NULL,
                   {QuoteIdentifier("SortOrder")} INTEGER NULL",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["InstanceID"] = shortTextType,
                    ["StationSchemeID"] = shortTextType,
                    ["OperationPlanID"] = shortTextType,
                    ["CategoryID"] = shortTextType,
                    ["GroupKey"] = shortTextType,
                    ["GroupText"] = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(100) NULL" : "TEXT NULL",
                    ["RouteCount"] = intType,
                    ["OperationCount"] = intType,
                    ["CapacityTotal"] = doubleType,
                    ["CapacityAverage"] = doubleType,
                    ["SortOrder"] = intType
                });

            EnsureResultTable(
                "operationthroughputsummaryroute",
                $@"{QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("OperationPlanID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("CategoryID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("RouteID")} VARCHAR(50) NULL,
                   {QuoteIdentifier("SortOrder")} INT NULL",
                $@"{QuoteIdentifier("InstanceID")} TEXT NULL,
                   {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                   {QuoteIdentifier("OperationPlanID")} TEXT NULL,
                   {QuoteIdentifier("CategoryID")} TEXT NULL,
                   {QuoteIdentifier("RouteID")} TEXT NULL,
                   {QuoteIdentifier("SortOrder")} INTEGER NULL",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["InstanceID"] = shortTextType,
                    ["StationSchemeID"] = shortTextType,
                    ["OperationPlanID"] = shortTextType,
                    ["CategoryID"] = shortTextType,
                    ["RouteID"] = shortTextType,
                    ["SortOrder"] = intType
                });
        }

        private static void EnsureColumns(
            DBConnector dbConnector,
            string tableName,
            IReadOnlyDictionary<string, string> requiredColumns)
        {
            var existingColumns = GetColumnNames(dbConnector, tableName);
            var quotedTableName = QuoteIdentifier(tableName);
            foreach (var column in requiredColumns)
            {
                if (existingColumns.Any(existing => string.Equals(existing, column.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                dbConnector.ExecuteNonQuery(
                    $@"ALTER TABLE {quotedTableName} ADD COLUMN {QuoteIdentifier(column.Key)} {column.Value}");
            }
        }

        private static void BackfillOperationPlanID(DBConnector dbConnector, string tableName)
        {
            var columns = GetColumnNames(dbConnector, tableName);
            if (!columns.Any(column => string.Equals(column, "OperationPlanID", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            dbConnector.ExecuteNonQuery(
                $@"UPDATE {QuoteIdentifier(tableName)}
                   SET OperationPlanID = @operationPlanID
                   WHERE OperationPlanID IS NULL OR TRIM(OperationPlanID) = ''",
                new { operationPlanID = DefaultOperationPlanID });
        }

        private static void BackfillMovementTemplateSortOrder(DBConnector dbConnector)
        {
            var rows = dbConnector.Query<MovementTemplateRow>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, TrainTemplateID, MovementID, MinDuration, SortOrder
                   FROM {QuoteIdentifier("movementtemplate")}
                   ORDER BY InstanceID, StationSchemeID, OperationPlanID, TrainTemplateID,
                            SortOrder IS NULL, SortOrder, MinDuration IS NULL, MinDuration, MovementID") ?? new List<MovementTemplateRow>();
            foreach (var group in rows
                .Where(row => !string.IsNullOrWhiteSpace(row.InstanceID) &&
                              !string.IsNullOrWhiteSpace(row.StationSchemeID) &&
                              !string.IsNullOrWhiteSpace(row.OperationPlanID) &&
                              !string.IsNullOrWhiteSpace(row.TrainTemplateID) &&
                              !string.IsNullOrWhiteSpace(row.MovementID))
                .GroupBy(row => new
                {
                    row.InstanceID,
                    row.StationSchemeID,
                    row.OperationPlanID,
                    row.TrainTemplateID
                }))
            {
                var index = 0;
                foreach (var row in group)
                {
                    if (row.SortOrder == index)
                    {
                        index++;
                        continue;
                    }

                    dbConnector.ExecuteNonQuery(
                        $@"UPDATE {QuoteIdentifier("movementtemplate")}
                           SET SortOrder = @sortOrder
                           WHERE InstanceID = @instanceID
                             AND StationSchemeID = @stationSchemeID
                             AND OperationPlanID = @operationPlanID
                             AND TrainTemplateID = @trainTemplateID
                             AND MovementID = @movementID",
                        new
                        {
                            instanceID = row.InstanceID,
                            stationSchemeID = row.StationSchemeID,
                            operationPlanID = row.OperationPlanID,
                            trainTemplateID = row.TrainTemplateID,
                            movementID = row.MovementID,
                            sortOrder = index
                        });
                    index++;
                }
            }
        }

        private static void BackfillMovementSortOrder(DBConnector dbConnector)
        {
            var rows = dbConnector.Query<MovementRow>(
                $@"SELECT InstanceID, StationSchemeID, OperationPlanID, TrainID, MovementID, EarliestStartTime, SortOrder
                   FROM {QuoteIdentifier("movement")}
                   ORDER BY InstanceID, StationSchemeID, OperationPlanID, TrainID,
                            SortOrder IS NULL, SortOrder, EarliestStartTime, MovementID") ?? new List<MovementRow>();
            foreach (var group in rows
                .Where(row => !string.IsNullOrWhiteSpace(row.InstanceID) &&
                              !string.IsNullOrWhiteSpace(row.StationSchemeID) &&
                              !string.IsNullOrWhiteSpace(row.OperationPlanID) &&
                              !string.IsNullOrWhiteSpace(row.TrainID) &&
                              !string.IsNullOrWhiteSpace(row.MovementID))
                .GroupBy(row => new
                {
                    row.InstanceID,
                    row.StationSchemeID,
                    row.OperationPlanID,
                    row.TrainID
                }))
            {
                var index = 0;
                foreach (var row in group)
                {
                    if (row.SortOrder == index)
                    {
                        index++;
                        continue;
                    }

                    dbConnector.ExecuteNonQuery(
                        $@"UPDATE {QuoteIdentifier("movement")}
                           SET SortOrder = @sortOrder
                           WHERE InstanceID = @instanceID
                             AND StationSchemeID = @stationSchemeID
                             AND OperationPlanID = @operationPlanID
                             AND TrainID = @trainID
                             AND MovementID = @movementID",
                        new
                        {
                            instanceID = row.InstanceID,
                            stationSchemeID = row.StationSchemeID,
                            operationPlanID = row.OperationPlanID,
                            trainID = row.TrainID,
                            movementID = row.MovementID,
                            sortOrder = index
                        });
                    index++;
                }
            }
        }

        private static List<string> GetColumnNames(DBConnector dbConnector, string tableName)
        {
            if (!TableExists(dbConnector, tableName))
            {
                return new List<string>();
            }

            if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
            {
                return (dbConnector.Query<DatabaseNameLookupRow>(
                    @"SELECT COLUMN_NAME AS Name
                      FROM INFORMATION_SCHEMA.COLUMNS
                      WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName",
                    new { tableName }) ?? new List<DatabaseNameLookupRow>())
                    .Select(column => column.Name ?? string.Empty)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList();
            }

            return (dbConnector.Query<DatabaseNameLookupRow>(
                $@"PRAGMA table_info({QuoteIdentifier(tableName)})") ?? new List<DatabaseNameLookupRow>())
                .Select(column => column.Name ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
        }

        private static bool TableExists(DBConnector dbConnector, string tableName)
        {
            List<DatabaseNameLookupRow>? rows;
            if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
            {
                rows = dbConnector.Query<DatabaseNameLookupRow>(
                    @"SELECT TABLE_NAME AS Name
                      FROM INFORMATION_SCHEMA.TABLES
                      WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName
                      LIMIT 1",
                    new { tableName });
            }
            else
            {
                rows = dbConnector.Query<DatabaseNameLookupRow>(
                    @"SELECT name AS Name
                      FROM sqlite_master
                      WHERE type = 'table' AND lower(name) = lower(@tableName)
                      LIMIT 1",
                    new { tableName });
            }

            return rows?.Any() == true;
        }

        private sealed class DatabaseNameLookupRow
        {
            public string? Name { get; set; }
        }
    }
}
