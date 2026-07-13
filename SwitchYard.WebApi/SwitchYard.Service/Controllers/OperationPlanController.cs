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

        public OperationPlanController(
            ILogger<OperationPlanController> logger,
            SnowflakeIdGenerator snowflakeIdGenerator)
        {
            _logger = logger;
            _snowflakeIdGenerator = snowflakeIdGenerator;
        }

        [HttpGet(Name = "GetTrainTemplates")]
        public IActionResult GetTrainTemplates(
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

                EnsureOperationPlanTemplateSchema(dbConnector);
                return Ok(LoadTrainTemplates(dbConnector, scope.InstanceID!, scope.StationSchemeID!));
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
                if (string.IsNullOrWhiteSpace(template.TrainTemplateID))
                {
                    template.TrainTemplateID = GenerateTrainTemplateID(dbConnector, template.InstanceID!, template.StationSchemeID!);
                }

                if (TrainTemplateExists(dbConnector, template.InstanceID!, template.StationSchemeID!, template.TrainTemplateID!))
                {
                    return BadRequest("Train template ID already exists in the selected station scheme.");
                }

                var tableName = QuoteIdentifier("traintemplate");
                var result = dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {tableName} (
                           InstanceID, StationSchemeID, TrainTemplateID, Name, {QuoteIdentifier("Type")}, {QuoteIdentifier("Number")})
                       VALUES (
                           @InstanceID, @StationSchemeID, @TrainTemplateID, @Name, @Type, @Number)",
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
                if (!TrainTemplateExists(dbConnector, template.InstanceID!, template.StationSchemeID!, originalTrainTemplateID!))
                {
                    return NotFound("Train template not found.");
                }

                if (!string.Equals(originalTrainTemplateID, template.TrainTemplateID, StringComparison.OrdinalIgnoreCase) &&
                    TrainTemplateExists(dbConnector, template.InstanceID!, template.StationSchemeID!, template.TrainTemplateID!))
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
                           {QuoteIdentifier("Number")} = @Number
                       WHERE InstanceID = @InstanceID
                         AND StationSchemeID = @StationSchemeID
                         AND TrainTemplateID = @OriginalTrainTemplateID",
                    new
                    {
                        template.InstanceID,
                        template.StationSchemeID,
                        template.TrainTemplateID,
                        template.Name,
                        template.Type,
                        template.Number,
                        OriginalTrainTemplateID = originalTrainTemplateID
                    });

                if (!string.Equals(originalTrainTemplateID, template.TrainTemplateID, StringComparison.OrdinalIgnoreCase))
                {
                    dbConnector.ExecuteNonQuery(
                        $@"UPDATE {movementTableName}
                           SET TrainTemplateID = @TrainTemplateID
                           WHERE InstanceID = @InstanceID
                             AND StationSchemeID = @StationSchemeID
                             AND TrainTemplateID = @OriginalTrainTemplateID",
                        new
                        {
                            template.InstanceID,
                            template.StationSchemeID,
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
            [FromQuery] string? trainTemplateID = null)
        {
            DBConnector? dbConnector = null;
            try
            {
                var normalizedInstanceID = instanceID?.Trim();
                var normalizedStationSchemeID = stationSchemeID?.Trim();
                var normalizedTrainTemplateID = trainTemplateID?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedInstanceID) ||
                    string.IsNullOrWhiteSpace(normalizedStationSchemeID) ||
                    string.IsNullOrWhiteSpace(normalizedTrainTemplateID))
                {
                    return BadRequest("instanceID, stationSchemeID and trainTemplateID are required.");
                }

                dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationPlanTemplateSchema(dbConnector);
                if (!TrainTemplateExists(dbConnector, normalizedInstanceID, normalizedStationSchemeID, normalizedTrainTemplateID))
                {
                    return NotFound("Train template not found.");
                }

                dbConnector.BeginTransaction();
                DeleteMovementTemplatesForTrain(dbConnector, normalizedInstanceID, normalizedStationSchemeID, normalizedTrainTemplateID);
                dbConnector.ExecuteNonQuery(
                    $@"DELETE FROM {QuoteIdentifier("traintemplate")}
                       WHERE InstanceID = @normalizedInstanceID
                         AND StationSchemeID = @normalizedStationSchemeID
                         AND TrainTemplateID = @normalizedTrainTemplateID",
                    new { normalizedInstanceID, normalizedStationSchemeID, normalizedTrainTemplateID });
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
            [FromQuery] string? trainTemplateID = null)
        {
            try
            {
                var normalizedInstanceID = instanceID?.Trim();
                var normalizedStationSchemeID = stationSchemeID?.Trim();
                var normalizedTrainTemplateID = trainTemplateID?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedInstanceID) ||
                    string.IsNullOrWhiteSpace(normalizedStationSchemeID) ||
                    string.IsNullOrWhiteSpace(normalizedTrainTemplateID))
                {
                    return BadRequest("instanceID, stationSchemeID and trainTemplateID are required.");
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationPlanTemplateSchema(dbConnector);
                if (!TrainTemplateExists(dbConnector, normalizedInstanceID, normalizedStationSchemeID, normalizedTrainTemplateID))
                {
                    return Ok(new List<MovementTemplateRow>());
                }

                return Ok(LoadMovementTemplates(
                    dbConnector,
                    normalizedInstanceID,
                    normalizedStationSchemeID,
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
                if (!TrainTemplateExists(dbConnector, movement.InstanceID!, movement.StationSchemeID!, movement.TrainTemplateID!))
                {
                    return NotFound("Train template not found.");
                }

                if (string.IsNullOrWhiteSpace(movement.MovementID))
                {
                    movement.MovementID = GenerateMovementID(
                        dbConnector,
                        movement.InstanceID!,
                        movement.StationSchemeID!,
                        movement.TrainTemplateID!);
                }

                if (MovementTemplateExists(
                    dbConnector,
                    movement.InstanceID!,
                    movement.StationSchemeID!,
                    movement.TrainTemplateID!,
                    movement.MovementID!))
                {
                    return BadRequest("Movement template ID already exists under the selected train template.");
                }

                var tableName = QuoteIdentifier("movementtemplate");
                var result = dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {tableName} (
                           InstanceID, StationSchemeID, TrainTemplateID, MovementID, Name, RouteIDList, MinDuration)
                       VALUES (
                           @InstanceID, @StationSchemeID, @TrainTemplateID, @MovementID, @Name, @RouteIDList, @MinDuration)",
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
                if (!TrainTemplateExists(dbConnector, movement.InstanceID!, movement.StationSchemeID!, movement.TrainTemplateID!))
                {
                    return NotFound("Train template not found.");
                }

                if (!MovementTemplateExists(
                    dbConnector,
                    movement.InstanceID!,
                    movement.StationSchemeID!,
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
                           MinDuration = @MinDuration
                       WHERE InstanceID = @InstanceID
                         AND StationSchemeID = @StationSchemeID
                         AND TrainTemplateID = @TrainTemplateID
                         AND MovementID = @OriginalMovementID",
                    new
                    {
                        movement.InstanceID,
                        movement.StationSchemeID,
                        movement.TrainTemplateID,
                        movement.MovementID,
                        movement.Name,
                        movement.RouteIDList,
                        movement.MinDuration,
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

        [HttpDelete(Name = "DeleteMovementTemplate")]
        public IActionResult DeleteMovementTemplate(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null,
            [FromQuery] string? trainTemplateID = null,
            [FromQuery] string? movementID = null)
        {
            try
            {
                var normalizedInstanceID = instanceID?.Trim();
                var normalizedStationSchemeID = stationSchemeID?.Trim();
                var normalizedTrainTemplateID = trainTemplateID?.Trim();
                var normalizedMovementID = movementID?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedInstanceID) ||
                    string.IsNullOrWhiteSpace(normalizedStationSchemeID) ||
                    string.IsNullOrWhiteSpace(normalizedTrainTemplateID) ||
                    string.IsNullOrWhiteSpace(normalizedMovementID))
                {
                    return BadRequest("instanceID, stationSchemeID, trainTemplateID and movementID are required.");
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureOperationPlanTemplateSchema(dbConnector);
                if (!MovementTemplateExists(
                    dbConnector,
                    normalizedInstanceID,
                    normalizedStationSchemeID,
                    normalizedTrainTemplateID,
                    normalizedMovementID))
                {
                    return NotFound("Movement template not found.");
                }

                dbConnector.ExecuteNonQuery(
                    $@"DELETE FROM {QuoteIdentifier("movementtemplate")}
                       WHERE InstanceID = @normalizedInstanceID
                         AND StationSchemeID = @normalizedStationSchemeID
                         AND TrainTemplateID = @normalizedTrainTemplateID
                         AND MovementID = @normalizedMovementID",
                    new
                    {
                        normalizedInstanceID,
                        normalizedStationSchemeID,
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

                EnsureTrainOperationPlanSchema(dbConnector);
                return Ok(LoadTrainOperationPlan(dbConnector, scope.InstanceID!, scope.StationSchemeID!));
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
                var generatedPlan = BuildGeneratedTrainOperationPlan(
                    dbConnector,
                    normalized.InstanceID!,
                    normalized.StationSchemeID!,
                    normalized.StartMinutes,
                    normalized.EndMinutes);

                dbConnector.BeginTransaction();
                DeleteTrainOperationPlan(dbConnector, normalized.InstanceID!, normalized.StationSchemeID!);
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
                if (string.IsNullOrWhiteSpace(train.ID))
                {
                    train.ID = GenerateOperationTrainID(dbConnector, train.InstanceID!, train.StationSchemeID!);
                }
                if (string.IsNullOrWhiteSpace(train.TrainNumber))
                {
                    train.TrainNumber = GenerateOperationTrainNumber(dbConnector, train.InstanceID!, train.StationSchemeID!);
                }

                if (OperationTrainExists(dbConnector, train.InstanceID!, train.StationSchemeID!, train.ID!))
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
                if (!OperationTrainExists(dbConnector, train.InstanceID!, train.StationSchemeID!, train.ID!))
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
            [FromQuery] string? id = null)
        {
            DBConnector? dbConnector = null;
            try
            {
                var normalizedInstanceID = instanceID?.Trim();
                var normalizedStationSchemeID = stationSchemeID?.Trim();
                var normalizedID = id?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedInstanceID) ||
                    string.IsNullOrWhiteSpace(normalizedStationSchemeID) ||
                    string.IsNullOrWhiteSpace(normalizedID))
                {
                    return BadRequest("instanceID, stationSchemeID and id are required.");
                }

                dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureTrainOperationPlanSchema(dbConnector);
                if (!OperationTrainExists(dbConnector, normalizedInstanceID, normalizedStationSchemeID, normalizedID))
                {
                    return NotFound("Train not found.");
                }

                dbConnector.BeginTransaction();
                dbConnector.ExecuteNonQuery(
                    $@"DELETE FROM {QuoteIdentifier("movement")}
                       WHERE InstanceID = @normalizedInstanceID
                         AND StationSchemeID = @normalizedStationSchemeID
                         AND TrainID = @normalizedID",
                    new { normalizedInstanceID, normalizedStationSchemeID, normalizedID });
                dbConnector.ExecuteNonQuery(
                    $@"DELETE FROM {QuoteIdentifier("train")}
                       WHERE InstanceID = @normalizedInstanceID
                         AND StationSchemeID = @normalizedStationSchemeID
                         AND {QuoteIdentifier("ID")} = @normalizedID",
                    new { normalizedInstanceID, normalizedStationSchemeID, normalizedID });
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
                if (!OperationTrainExists(dbConnector, movement.InstanceID!, movement.StationSchemeID!, movement.TrainID!))
                {
                    return NotFound("Train not found.");
                }

                if (string.IsNullOrWhiteSpace(movement.MovementID))
                {
                    movement.MovementID = GenerateOperationMovementID(
                        dbConnector,
                        movement.InstanceID!,
                        movement.StationSchemeID!,
                        movement.TrainID!);
                }

                if (OperationMovementExists(dbConnector, movement.InstanceID!, movement.StationSchemeID!, movement.TrainID!, movement.MovementID!))
                {
                    return BadRequest("Movement ID already exists under the selected train.");
                }

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
                if (!OperationMovementExists(dbConnector, movement.InstanceID!, movement.StationSchemeID!, movement.TrainID!, movement.MovementID!))
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

        [HttpDelete(Name = "DeleteMovement")]
        public IActionResult DeleteMovement(
            [FromQuery] string? instanceID = null,
            [FromQuery] string? stationSchemeID = null,
            [FromQuery] string? trainID = null,
            [FromQuery] string? movementID = null)
        {
            try
            {
                var normalizedInstanceID = instanceID?.Trim();
                var normalizedStationSchemeID = stationSchemeID?.Trim();
                var normalizedTrainID = trainID?.Trim();
                var normalizedMovementID = movementID?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedInstanceID) ||
                    string.IsNullOrWhiteSpace(normalizedStationSchemeID) ||
                    string.IsNullOrWhiteSpace(normalizedTrainID) ||
                    string.IsNullOrWhiteSpace(normalizedMovementID))
                {
                    return BadRequest("instanceID, stationSchemeID, trainID and movementID are required.");
                }

                var dbConnector = GetCapacityDbConnector();
                var authResult = ValidateCapacityInstanceOwnershipOrFail(dbConnector, normalizedInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                EnsureTrainOperationPlanSchema(dbConnector);
                if (!OperationMovementExists(dbConnector, normalizedInstanceID, normalizedStationSchemeID, normalizedTrainID, normalizedMovementID))
                {
                    return NotFound("Movement not found.");
                }

                dbConnector.ExecuteNonQuery(
                    $@"DELETE FROM {QuoteIdentifier("movement")}
                       WHERE InstanceID = @normalizedInstanceID
                         AND StationSchemeID = @normalizedStationSchemeID
                         AND TrainID = @normalizedTrainID
                         AND MovementID = @normalizedMovementID",
                    new
                    {
                        normalizedInstanceID,
                        normalizedStationSchemeID,
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

                EnsureOperationBottleneckSummaryCategorySchema(dbConnector);
                return Ok(LoadOperationBottleneckSummaryCategories(dbConnector, scope.InstanceID!, scope.StationSchemeID!));
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
                dbConnector.BeginTransaction();
                DeleteOperationBottleneckSummaryCategories(dbConnector, normalized.InstanceID!, normalized.StationSchemeID!);
                InsertOperationBottleneckSummaryCategories(dbConnector, normalized.Categories);
                dbConnector.Commit();

                return Ok(LoadOperationBottleneckSummaryCategories(dbConnector, normalized.InstanceID!, normalized.StationSchemeID!));
            }
            catch (Exception ex)
            {
                dbConnector?.Rollback();
                _logger.LogError(ex, "Failed to save operation bottleneck summary categories.");
                return StatusCode(500, "Failed to save operation bottleneck summary categories.");
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

        private (TrainTemplateRow? Template, IActionResult? ErrorResult) NormalizeTrainTemplateRequest(
            TrainTemplateRequest? request,
            bool allowMissingID)
        {
            var scope = NormalizeScope(request?.InstanceID, request?.StationSchemeID);
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
                TrainTemplateID = trainTemplateID,
                Name = name,
                Type = request?.Type?.Trim() ?? string.Empty,
                Number = request?.Number
            }, null);
        }

        private (MovementTemplateRow? Movement, IActionResult? ErrorResult) NormalizeMovementTemplateRequest(
            MovementTemplateRequest? request,
            bool allowMissingID)
        {
            var scope = NormalizeScope(request?.InstanceID, request?.StationSchemeID);
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
                TrainTemplateID = trainTemplateID,
                MovementID = movementID,
                Name = name,
                RouteIDList = request?.RouteIDList?.Trim() ?? string.Empty,
                MinDuration = request?.MinDuration
            }, null);
        }

        private (string? InstanceID, string? StationSchemeID, int StartMinutes, int EndMinutes, IActionResult? ErrorResult)
            NormalizeTrainOperationPlanRequest(GenerateTrainOperationPlanRequest? request)
        {
            var scope = NormalizeScope(request?.InstanceID, request?.StationSchemeID);
            if (scope.ErrorResult != null)
            {
                return (null, null, 0, 0, scope.ErrorResult);
            }

            var startTime = string.IsNullOrWhiteSpace(request?.StartTime) ? "00:00" : request.StartTime.Trim();
            var endTime = string.IsNullOrWhiteSpace(request?.EndTime) ? "24:00" : request.EndTime.Trim();
            if (!TryParsePlanTime(startTime, out var startMinutes) ||
                !TryParsePlanTime(endTime, out var endMinutes))
            {
                return (null, null, 0, 0, BadRequest("startTime and endTime must be valid time values."));
            }

            while (endMinutes <= startMinutes)
            {
                endMinutes += 24 * 60;
            }

            return (scope.InstanceID, scope.StationSchemeID, startMinutes, endMinutes, null);
        }

        private (TrainRow? Train, IActionResult? ErrorResult) NormalizeTrainRowRequest(
            TrainRow? request,
            bool allowMissingID)
        {
            var scope = NormalizeScope(request?.InstanceID, request?.StationSchemeID);
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
                ID = id,
                TrainTemplateID = request?.TrainTemplateID?.Trim() ?? string.Empty,
                TrainNumber = TrimToMaxLength(request?.TrainNumber ?? string.Empty, 50),
                Name = request?.Name?.Trim() ?? string.Empty,
                TrainType = TrimToMaxLength(request?.TrainType ?? string.Empty, 20)
            }, null);
        }

        private (MovementRow? Movement, IActionResult? ErrorResult) NormalizeMovementRowRequest(
            MovementRow? request,
            bool allowMissingMovementID)
        {
            var scope = NormalizeScope(request?.InstanceID, request?.StationSchemeID);
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
                TrainID = trainID,
                TrainTemplateID = request?.TrainTemplateID?.Trim() ?? string.Empty,
                MovementID = movementID,
                Name = request?.Name?.Trim() ?? string.Empty,
                RouteIDList = request?.RouteIDList?.Trim() ?? string.Empty,
                MinDuration = request?.MinDuration,
                EarliestStartTime = request?.EarliestStartTime?.Trim() ?? string.Empty,
                LatestEndTime = request?.LatestEndTime?.Trim() ?? string.Empty,
                Route = request?.Route?.Trim() ?? string.Empty,
                Tag = request?.Tag?.Trim() ?? string.Empty
            }, null);
        }

        private (string? InstanceID, string? StationSchemeID, List<OperationBottleneckSummaryCategoryRow> Categories, IActionResult? ErrorResult)
            NormalizeOperationBottleneckSummaryCategorySaveRequest(OperationBottleneckSummaryCategorySaveRequest? request)
        {
            var scope = NormalizeScope(request?.InstanceID, request?.StationSchemeID);
            if (scope.ErrorResult != null)
            {
                return (null, null, new List<OperationBottleneckSummaryCategoryRow>(), scope.ErrorResult);
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
                    CategoryID = categoryID,
                    Name = TrimToMaxLength(requestedCategory.Name ?? $"Category {sortOrder + 1}", 100),
                    RouteIDList = routeIDList,
                    SortOrder = sortOrder
                });
            }

            return (scope.InstanceID, scope.StationSchemeID, categories, null);
        }

        private TrainOperationPlanResponse BuildGeneratedTrainOperationPlan(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            int startMinutes,
            int endMinutes)
        {
            var templates = LoadTrainTemplates(dbConnector, instanceID, stationSchemeID);
            var movementTemplatesByTrainTemplate = LoadMovementTemplatesByTrainTemplate(
                dbConnector,
                instanceID,
                stationSchemeID);
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
                        ID = trainID,
                        TrainTemplateID = trainTemplateID,
                        TrainNumber = (generatedTrainSequence++).ToString(),
                        Name = TrimToMaxLength(template.Name ?? string.Empty, 50),
                        TrainType = TrimToMaxLength(template.Type ?? string.Empty, 20)
                    });

                    var trainCursorMinutes = startMinutes;
                    foreach (var movementTemplate in movementTemplates)
                    {
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
                            TrainID = trainID,
                            TrainTemplateID = TrimToMaxLength(movementTemplate.TrainTemplateID ?? string.Empty, 50),
                            MovementID = TrimToMaxLength(movementTemplate.MovementID ?? string.Empty, 50),
                            Name = TrimToMaxLength(movementTemplate.Name ?? string.Empty, 50),
                            RouteIDList = movementTemplate.RouteIDList ?? string.Empty,
                            MinDuration = movementTemplate.MinDuration,
                            EarliestStartTime = FormatPlanTime(earliestStartMinutes),
                            LatestEndTime = FormatPlanTime(latestEndMinutes),
                            Route = TrimToMaxLength(route, 50),
                            Tag = TrimToMaxLength(movementTemplate.Name ?? string.Empty, 50)
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

        private List<TrainTemplateRow> LoadTrainTemplates(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID)
        {
            return dbConnector.Query<TrainTemplateRow>(
                $@"SELECT InstanceID, StationSchemeID, TrainTemplateID, Name,
                          {QuoteIdentifier("Type")} AS {QuoteIdentifier("Type")},
                          {QuoteIdentifier("Number")} AS {QuoteIdentifier("Number")}
                   FROM {QuoteIdentifier("traintemplate")}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID
                   ORDER BY {QuoteIdentifier("Number")} IS NULL,
                            {QuoteIdentifier("Number")},
                            TrainTemplateID",
                new { instanceID, stationSchemeID }) ?? new List<TrainTemplateRow>();
        }

        private List<MovementTemplateRow> LoadMovementTemplates(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string trainTemplateID)
        {
            return dbConnector.Query<MovementTemplateRow>(
                $@"SELECT InstanceID, StationSchemeID, TrainTemplateID, MovementID, Name, RouteIDList, MinDuration
                   FROM {QuoteIdentifier("movementtemplate")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND TrainTemplateID = @trainTemplateID
                   ORDER BY MinDuration IS NULL, MinDuration, MovementID",
                new { instanceID, stationSchemeID, trainTemplateID }) ?? new List<MovementTemplateRow>();
        }

        private Dictionary<string, List<MovementTemplateRow>> LoadMovementTemplatesByTrainTemplate(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID)
        {
            var rows = dbConnector.Query<MovementTemplateRow>(
                $@"SELECT InstanceID, StationSchemeID, TrainTemplateID, MovementID, Name, RouteIDList, MinDuration
                   FROM {QuoteIdentifier("movementtemplate")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                   ORDER BY TrainTemplateID, MovementID",
                new { instanceID, stationSchemeID }) ?? new List<MovementTemplateRow>();
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
            string stationSchemeID)
        {
            return new TrainOperationPlanResponse
            {
                Trains = LoadTrains(dbConnector, instanceID, stationSchemeID),
                Movements = LoadMovements(dbConnector, instanceID, stationSchemeID)
            };
        }

        private List<TrainRow> LoadTrains(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID)
        {
            var trains = dbConnector.Query<TrainRow>(
                $@"SELECT InstanceID, StationSchemeID, {QuoteIdentifier("ID")}, TrainTemplateID, TrainNumber, Name, TrainType
                   FROM {QuoteIdentifier("train")}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID",
                new { instanceID, stationSchemeID }) ?? new List<TrainRow>();
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
            string stationSchemeID)
        {
            return dbConnector.Query<MovementRow>(
                $@"SELECT InstanceID, StationSchemeID, TrainID, TrainTemplateID, MovementID, Name, RouteIDList,
                          MinDuration, EarliestStartTime, LatestEndTime,
                          {QuoteIdentifier("Route")}, Tag
                   FROM {QuoteIdentifier("movement")}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID
                   ORDER BY TrainID, EarliestStartTime, MovementID",
                new { instanceID, stationSchemeID }) ?? new List<MovementRow>();
        }

        private List<OperationBottleneckSummaryCategoryRow> LoadOperationBottleneckSummaryCategories(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID)
        {
            return dbConnector.Query<OperationBottleneckSummaryCategoryRow>(
                $@"SELECT InstanceID, StationSchemeID, CategoryID, Name, RouteIDList, SortOrder
                   FROM {QuoteIdentifier("operationbottlenecksummarycategory")}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID
                   ORDER BY SortOrder IS NULL, SortOrder, CategoryID",
                new { instanceID, stationSchemeID }) ?? new List<OperationBottleneckSummaryCategoryRow>();
        }

        private void DeleteOperationBottleneckSummaryCategories(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID)
        {
            dbConnector.ExecuteNonQuery(
                $@"DELETE FROM {QuoteIdentifier("operationbottlenecksummarycategory")}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID",
                new { instanceID, stationSchemeID });
        }

        private void InsertOperationBottleneckSummaryCategories(
            DBConnector dbConnector,
            IEnumerable<OperationBottleneckSummaryCategoryRow> categories)
        {
            foreach (var category in categories)
            {
                dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {QuoteIdentifier("operationbottlenecksummarycategory")} (
                           InstanceID, StationSchemeID, CategoryID, Name, RouteIDList, SortOrder)
                       VALUES (
                           @InstanceID, @StationSchemeID, @CategoryID, @Name, @RouteIDList, @SortOrder)",
                    category);
            }
        }

        private void DeleteTrainOperationPlan(DBConnector dbConnector, string instanceID, string stationSchemeID)
        {
            dbConnector.ExecuteNonQuery(
                $@"DELETE FROM {QuoteIdentifier("movement")}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID",
                new { instanceID, stationSchemeID });
            dbConnector.ExecuteNonQuery(
                $@"DELETE FROM {QuoteIdentifier("train")}
                   WHERE InstanceID = @instanceID AND StationSchemeID = @stationSchemeID",
                new { instanceID, stationSchemeID });
        }

        private void InsertTrainOperationPlan(DBConnector dbConnector, TrainOperationPlanResponse plan)
        {
            foreach (var train in plan.Trains)
            {
                dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {QuoteIdentifier("train")} (
                           InstanceID, StationSchemeID, {QuoteIdentifier("ID")}, TrainTemplateID, TrainNumber, Name, TrainType)
                       VALUES (
                           @InstanceID, @StationSchemeID, @ID, @TrainTemplateID, @TrainNumber, @Name, @TrainType)",
                    train);
            }

            foreach (var movement in plan.Movements)
            {
                dbConnector.ExecuteNonQuery(
                    $@"INSERT INTO {QuoteIdentifier("movement")} (
                           InstanceID, StationSchemeID, TrainID, TrainTemplateID, MovementID, Name, RouteIDList,
                           MinDuration, EarliestStartTime, LatestEndTime,
                           {QuoteIdentifier("Route")}, Tag)
                       VALUES (
                           @InstanceID, @StationSchemeID, @TrainID, @TrainTemplateID, @MovementID, @Name, @RouteIDList,
                           @MinDuration, @EarliestStartTime, @LatestEndTime,
                           @Route, @Tag)",
                    movement);
            }
        }

        private void InsertTrain(DBConnector dbConnector, TrainRow train)
        {
            dbConnector.ExecuteNonQuery(
                $@"INSERT INTO {QuoteIdentifier("train")} (
                       InstanceID, StationSchemeID, {QuoteIdentifier("ID")}, TrainTemplateID, TrainNumber, Name, TrainType)
                   VALUES (
                       @InstanceID, @StationSchemeID, @ID, @TrainTemplateID, @TrainNumber, @Name, @TrainType)",
                train);
        }

        private void UpdateTrain(DBConnector dbConnector, TrainRow train)
        {
            dbConnector.ExecuteNonQuery(
                $@"UPDATE {QuoteIdentifier("train")}
                   SET TrainTemplateID = @TrainTemplateID,
                       TrainNumber = @TrainNumber,
                       Name = @Name,
                       TrainType = @TrainType
                   WHERE InstanceID = @InstanceID
                     AND StationSchemeID = @StationSchemeID
                     AND {QuoteIdentifier("ID")} = @ID",
                train);
        }

        private void InsertMovement(DBConnector dbConnector, MovementRow movement)
        {
            dbConnector.ExecuteNonQuery(
                $@"INSERT INTO {QuoteIdentifier("movement")} (
                       InstanceID, StationSchemeID, TrainID, TrainTemplateID, MovementID, Name, RouteIDList,
                       MinDuration, EarliestStartTime, LatestEndTime,
                       {QuoteIdentifier("Route")}, Tag)
                   VALUES (
                       @InstanceID, @StationSchemeID, @TrainID, @TrainTemplateID, @MovementID, @Name, @RouteIDList,
                       @MinDuration, @EarliestStartTime, @LatestEndTime,
                       @Route, @Tag)",
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
                       Tag = @Tag
                   WHERE InstanceID = @InstanceID
                     AND StationSchemeID = @StationSchemeID
                     AND TrainID = @TrainID
                     AND MovementID = @MovementID",
                movement);
        }

        private bool TrainTemplateExists(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string trainTemplateID)
        {
            return (dbConnector.Query<TrainTemplateRow>(
                $@"SELECT TrainTemplateID
                   FROM {QuoteIdentifier("traintemplate")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND TrainTemplateID = @trainTemplateID
                   LIMIT 1",
                new { instanceID, stationSchemeID, trainTemplateID }) ?? new List<TrainTemplateRow>()).Any();
        }

        private bool MovementTemplateExists(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string trainTemplateID,
            string movementID)
        {
            return (dbConnector.Query<MovementTemplateRow>(
                $@"SELECT MovementID
                   FROM {QuoteIdentifier("movementtemplate")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND TrainTemplateID = @trainTemplateID
                     AND MovementID = @movementID
                   LIMIT 1",
                new { instanceID, stationSchemeID, trainTemplateID, movementID }) ?? new List<MovementTemplateRow>()).Any();
        }

        private bool OperationTrainExists(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string id)
        {
            return (dbConnector.Query<TrainRow>(
                $@"SELECT {QuoteIdentifier("ID")}
                   FROM {QuoteIdentifier("train")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND {QuoteIdentifier("ID")} = @id
                   LIMIT 1",
                new { instanceID, stationSchemeID, id }) ?? new List<TrainRow>()).Any();
        }

        private bool OperationMovementExists(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string trainID,
            string movementID)
        {
            return (dbConnector.Query<MovementRow>(
                $@"SELECT MovementID
                   FROM {QuoteIdentifier("movement")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND TrainID = @trainID
                     AND MovementID = @movementID
                   LIMIT 1",
                new { instanceID, stationSchemeID, trainID, movementID }) ?? new List<MovementRow>()).Any();
        }

        private void DeleteMovementTemplatesForTrain(
            DBConnector dbConnector,
            string instanceID,
            string stationSchemeID,
            string trainTemplateID)
        {
            dbConnector.ExecuteNonQuery(
                $@"DELETE FROM {QuoteIdentifier("movementtemplate")}
                   WHERE InstanceID = @instanceID
                     AND StationSchemeID = @stationSchemeID
                     AND TrainTemplateID = @trainTemplateID",
                new { instanceID, stationSchemeID, trainTemplateID });
        }

        private string GenerateTrainTemplateID(DBConnector dbConnector, string instanceID, string stationSchemeID)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var candidate = _snowflakeIdGenerator.NextIdString();
                if (!TrainTemplateExists(dbConnector, instanceID, stationSchemeID, candidate))
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
            string trainTemplateID)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var candidate = _snowflakeIdGenerator.NextIdString();
                if (!MovementTemplateExists(dbConnector, instanceID, stationSchemeID, trainTemplateID, candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Failed to generate a unique movement template ID.");
        }

        private string GenerateOperationTrainID(DBConnector dbConnector, string instanceID, string stationSchemeID)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var candidate = _snowflakeIdGenerator.NextIdString();
                if (!OperationTrainExists(dbConnector, instanceID, stationSchemeID, candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Failed to generate a unique train ID.");
        }

        private string GenerateOperationTrainNumber(DBConnector dbConnector, string instanceID, string stationSchemeID)
        {
            var maxTrainNumber = LoadTrains(dbConnector, instanceID, stationSchemeID)
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
            string trainID)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var candidate = _snowflakeIdGenerator.NextIdString();
                if (!OperationMovementExists(dbConnector, instanceID, stationSchemeID, trainID, candidate))
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
            EnsureTrainTemplateSchema(dbConnector);
            EnsureMovementTemplateSchema(dbConnector);
        }

        private static void EnsureTrainOperationPlanSchema(DBConnector dbConnector)
        {
            EnsureOperationPlanTemplateSchema(dbConnector);
            EnsureTrainSchema(dbConnector);
            EnsureMovementSchema(dbConnector);
            EnsureOperationBottleneckSummaryCategorySchema(dbConnector);
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
                            {QuoteIdentifier("TrainTemplateID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Name")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Type")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Number")} INT NULL
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                            {QuoteIdentifier("TrainTemplateID")} TEXT NULL,
                            {QuoteIdentifier("Name")} TEXT NULL,
                            {QuoteIdentifier("Type")} TEXT NULL,
                            {QuoteIdentifier("Number")} INTEGER NULL
                        )");
                }

                return;
            }

            var textType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(50) NULL" : "TEXT NULL";
            var intType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "INT NULL" : "INTEGER NULL";
            EnsureColumns(dbConnector, "traintemplate", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["InstanceID"] = textType,
                ["StationSchemeID"] = textType,
                ["TrainTemplateID"] = textType,
                ["Name"] = textType,
                ["Type"] = textType,
                ["Number"] = intType
            });
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
                            {QuoteIdentifier("TrainTemplateID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("MovementID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Name")} VARCHAR(50) NULL,
                            {QuoteIdentifier("RouteIDList")} LONGTEXT NULL,
                            {QuoteIdentifier("MinDuration")} INT NULL
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                            {QuoteIdentifier("TrainTemplateID")} TEXT NULL,
                            {QuoteIdentifier("MovementID")} TEXT NULL,
                            {QuoteIdentifier("Name")} TEXT NULL,
                            {QuoteIdentifier("RouteIDList")} TEXT NULL,
                            {QuoteIdentifier("MinDuration")} INTEGER NULL
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
                ["TrainTemplateID"] = shortTextType,
                ["MovementID"] = shortTextType,
                ["Name"] = shortTextType,
                ["RouteIDList"] = longTextType,
                ["MinDuration"] = intType
            });
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
                            {QuoteIdentifier("ID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("TrainTemplateID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("TrainNumber")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Name")} VARCHAR(50) NULL,
                            {QuoteIdentifier("TrainType")} VARCHAR(20) NULL
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                            {QuoteIdentifier("ID")} TEXT NULL,
                            {QuoteIdentifier("TrainTemplateID")} TEXT NULL,
                            {QuoteIdentifier("TrainNumber")} TEXT NULL,
                            {QuoteIdentifier("Name")} TEXT NULL,
                            {QuoteIdentifier("TrainType")} TEXT NULL
                        )");
                }

                return;
            }

            var shortTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(50) NULL" : "TEXT NULL";
            var trainTypeTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(20) NULL" : "TEXT NULL";
            EnsureColumns(dbConnector, "train", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["InstanceID"] = shortTextType,
                ["StationSchemeID"] = shortTextType,
                ["ID"] = shortTextType,
                ["TrainTemplateID"] = shortTextType,
                ["TrainNumber"] = shortTextType,
                ["Name"] = shortTextType,
                ["TrainType"] = trainTypeTextType
            });
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
                            {QuoteIdentifier("TrainID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("TrainTemplateID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("MovementID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Name")} VARCHAR(50) NULL,
                            {QuoteIdentifier("RouteIDList")} LONGTEXT NULL,
                            {QuoteIdentifier("MinDuration")} INT NULL,
                            {QuoteIdentifier("EarliestStartTime")} VARCHAR(50) NULL,
                            {QuoteIdentifier("LatestEndTime")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Route")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Tag")} VARCHAR(50) NULL
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                            {QuoteIdentifier("TrainID")} TEXT NULL,
                            {QuoteIdentifier("TrainTemplateID")} TEXT NULL,
                            {QuoteIdentifier("MovementID")} TEXT NULL,
                            {QuoteIdentifier("Name")} TEXT NULL,
                            {QuoteIdentifier("RouteIDList")} TEXT NULL,
                            {QuoteIdentifier("MinDuration")} INTEGER NULL,
                            {QuoteIdentifier("EarliestStartTime")} TEXT NULL,
                            {QuoteIdentifier("LatestEndTime")} TEXT NULL,
                            {QuoteIdentifier("Route")} TEXT NULL,
                            {QuoteIdentifier("Tag")} TEXT NULL
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
                ["TrainID"] = shortTextType,
                ["TrainTemplateID"] = shortTextType,
                ["MovementID"] = shortTextType,
                ["Name"] = shortTextType,
                ["RouteIDList"] = longTextType,
                ["MinDuration"] = intType,
                ["EarliestStartTime"] = shortTextType,
                ["LatestEndTime"] = shortTextType,
                ["Route"] = shortTextType,
                ["Tag"] = shortTextType
            });
        }

        private static void EnsureOperationBottleneckSummaryCategorySchema(DBConnector dbConnector)
        {
            var tableName = QuoteIdentifier("operationbottlenecksummarycategory");
            if (!TableExists(dbConnector, "operationbottlenecksummarycategory"))
            {
                if (DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName))
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("StationSchemeID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("CategoryID")} VARCHAR(50) NULL,
                            {QuoteIdentifier("Name")} VARCHAR(100) NULL,
                            {QuoteIdentifier("RouteIDList")} LONGTEXT NULL,
                            {QuoteIdentifier("SortOrder")} INT NULL
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
                }
                else
                {
                    dbConnector.ExecuteNonQuery(
                        $@"CREATE TABLE IF NOT EXISTS {tableName} (
                            {QuoteIdentifier("InstanceID")} TEXT NULL,
                            {QuoteIdentifier("StationSchemeID")} TEXT NULL,
                            {QuoteIdentifier("CategoryID")} TEXT NULL,
                            {QuoteIdentifier("Name")} TEXT NULL,
                            {QuoteIdentifier("RouteIDList")} TEXT NULL,
                            {QuoteIdentifier("SortOrder")} INTEGER NULL
                        )");
                }

                return;
            }

            var shortTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(50) NULL" : "TEXT NULL";
            var nameTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "VARCHAR(100) NULL" : "TEXT NULL";
            var longTextType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "LONGTEXT NULL" : "TEXT NULL";
            var intType = DBConnector.IsMySql(DBConnector.CapacityDatabaseSectionName) ? "INT NULL" : "INTEGER NULL";
            EnsureColumns(dbConnector, "operationbottlenecksummarycategory", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["InstanceID"] = shortTextType,
                ["StationSchemeID"] = shortTextType,
                ["CategoryID"] = shortTextType,
                ["Name"] = nameTextType,
                ["RouteIDList"] = longTextType,
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
