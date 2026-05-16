using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MySqlX.XDevAPI;
using SwitchYard.Hump;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;
using SwitchYard.Service.Utils;
using System.Collections.Generic;
using System.Data.Common;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace SwitchYard.Service.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    [Authorize] // 整个控制器需要授权
    public class HumpController : Controller
    {
        IConfiguration _config;
        ILogger<HumpController> _logger;
        SnowflakeIdGenerator _snowflakeIdGenerator;
        InstanceAuthorizationService _authService;
        UserService _userService;
        HumpInstanceCopyService _humpInstanceCopyService;
        private const string UnknownLogValue = "N/A";
        private const string LogInstanceIdKey = "__log_instance_id";
        private const string LogObjectIdKey = "__log_object_id";

        public HumpController(
            ILogger<HumpController> logger,
            IConfiguration configuration,
            SnowflakeIdGenerator snowflakeIdGenerator,
            InstanceAuthorizationService authService,
            UserService userService,
            HumpInstanceCopyService humpInstanceCopyService)
        {
            _logger = logger;
            _config = configuration;
            _snowflakeIdGenerator = snowflakeIdGenerator;
            _authService = authService;
            _userService = userService;
            _humpInstanceCopyService = humpInstanceCopyService;
        }

        private string NormalizeLogValue(object? value)
        {
            var text = value?.ToString();
            return string.IsNullOrWhiteSpace(text) ? UnknownLogValue : text;
        }

        private string GetCurrentUsername()
        {
            return string.IsNullOrWhiteSpace(User?.Identity?.Name) ? UnknownLogValue : User.Identity.Name;
        }

        private string GetCurrentInstanceId()
        {
            return NormalizeLogValue(HttpContext?.Items[LogInstanceIdKey]);
        }

        private bool IsCurrentUserAdmin()
        {
            var username = User?.Identity?.Name;
            if (string.Equals(username, "Admin", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var role = User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return string.Equals(role, "Admin", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool TryNormalizeOwnerForPersistence(string? ownerInput, out string normalizedOwner, out string errorMessage)
        {
            normalizedOwner = string.Empty;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(ownerInput))
            {
                errorMessage = "Owner is required.";
                return false;
            }

            var trimmedOwner = ownerInput.Trim();
            var userById = _userService.GetUserById(trimmedOwner);
            if (userById != null)
            {
                normalizedOwner = userById.Name;
                return true;
            }

            var userByName = _userService.GetUserByUsername(trimmedOwner);
            if (userByName != null)
            {
                normalizedOwner = userByName.Name;
                return true;
            }

            errorMessage = "Owner does not exist.";
            return false;
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

        private void SetLogInstanceId(string? instanceID)
        {
            if (HttpContext != null && !string.IsNullOrWhiteSpace(instanceID))
            {
                HttpContext.Items[LogInstanceIdKey] = instanceID;
            }
        }

        private void LogInformationWithContext(string message, params object?[] args)
        {
            var prefix = $"{GetCurrentUsername()} on {GetCurrentInstanceId()}: ";
            _logger.LogInformation(prefix + message, args);
        }

        private void LogErrorWithContext(Exception ex, string message, params object?[] args)
        {
            var prefix = $"{GetCurrentUsername()} on {GetCurrentInstanceId()}: ";
            _logger.LogError(ex, prefix + message, args);
        }

        /// <summary>
        /// 验证实例所有权并返回相应的ActionResult
        /// </summary>
        private IActionResult? ValidateInstanceOwnershipOrFail(string instanceID)
        {
            SetLogInstanceId(instanceID);
            if (IsCurrentUserAdmin())
            {
                return null;
            }

            var username = User.Identity?.Name;
            var result = _authService.ValidateInstanceOwnership(instanceID, username);

            if (!result.IsAuthorized)
            {
                if (result.IsNotFound)
                {
                    _logger.LogWarning("Instance ownership validation failed: {ErrorMessage}", result.ErrorMessage);
                    return NotFound(result.ErrorMessage);
                }
                if (result.IsError)
                {
                    _logger.LogWarning("Instance ownership validation encountered an internal error: {ErrorMessage}", result.ErrorMessage);
                    return StatusCode(500, result.ErrorMessage);
                }
                _logger.LogWarning("Instance ownership validation failed: {ErrorMessage}", result.ErrorMessage);
                return Unauthorized(result.ErrorMessage ?? "Instance not found or not owned by user.");
            }

            return null; // 验证通过
        }

        [HttpGet(Name = "GetInstances")]
        public IActionResult GetInstances()
        {
            try
            {
                var username = User.Identity.Name;
                var isAdmin = IsCurrentUserAdmin();

                DBConnector dbConnector = DBConnector.GetDBConnector();
                var instanceList = isAdmin
                    ? dbConnector.Query<HumpInstance>("SELECT * FROM humpinstance ORDER BY CreatedDate DESC, ID DESC")
                    : dbConnector.Query<HumpInstance>("SELECT * FROM humpinstance WHERE Owner = @username ORDER BY CreatedDate DESC, ID DESC", new { username });
                LogInformationWithContext("Retrieved {InstanceCount} HumpInstances.", instanceList?.Count ?? 0);
                return Ok(instanceList);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error getting HumpInstance.");
                return StatusCode(500, "Internal server error while getting HumpInstance.");
            }
        }

        [HttpGet(Name = "GetInstancePage")]
        public IActionResult GetInstancePage([FromQuery] PaginationQuery query)
        {
            try
            {
                var username = User.Identity?.Name;
                var isAdmin = IsCurrentUserAdmin();
                if (!isAdmin && string.IsNullOrWhiteSpace(username))
                {
                    return Unauthorized("Invalid user context.");
                }

                DBConnector dbConnector = DBConnector.GetDBConnector();
                var whereSql = isAdmin ? string.Empty : "WHERE Owner = @username";
                object? baseParameters = isAdmin
                    ? null
                    : new { username };

                var totalCount = (dbConnector.Query<int>(
                    $"SELECT COUNT(1) FROM humpinstance {whereSql}",
                    baseParameters) ?? new List<int> { 0 }).FirstOrDefault();

                var items = dbConnector.Query<HumpInstance>(
                    $@"SELECT * FROM humpinstance
                       {whereSql}
                       ORDER BY CreatedDate DESC, ID DESC
                       LIMIT @pageSize OFFSET @offset",
                    isAdmin
                        ? new { pageSize = query.PageSize, offset = query.Offset }
                        : new { username, pageSize = query.PageSize, offset = query.Offset }) ?? new List<HumpInstance>();

                var result = new PagedResult<HumpInstance>
                {
                    Items = items,
                    PageNumber = query.PageNumber,
                    PageSize = query.PageSize,
                    TotalCount = totalCount
                };

                LogInformationWithContext(
                    "Retrieved paged HumpInstances. Page {PageNumber}, PageSize {PageSize}, TotalCount {TotalCount}.",
                    query.PageNumber,
                    query.PageSize,
                    totalCount);
                return Ok(result);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error getting paged HumpInstance.");
                return StatusCode(500, "Internal server error while getting paged HumpInstance.");
            }
        }

        [HttpPost(Name = "CreateInstance")]
        public IActionResult CreateInstance([FromBody] CreateHumpInstanceRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest("Invalid instance payload or missing name.");
                }

                var username = User.Identity?.Name;
                if (string.IsNullOrWhiteSpace(username))
                {
                    return Unauthorized("Invalid user context.");
                }

                var isAdmin = IsCurrentUserAdmin();
                var targetOwner = username;
                if (isAdmin)
                {
                    if (!TryNormalizeOwnerForPersistence(request.Owner, out var normalizedOwner, out var ownerError))
                    {
                        return BadRequest(ownerError);
                    }

                    targetOwner = normalizedOwner;
                }

                // All server-controlled fields are explicitly set here to avoid mass assignment.
                var instance = new HumpInstance
                {
                    ID = _snowflakeIdGenerator.NextIdString(),
                    Name = request.Name.Trim(),
                    Owner = targetOwner,
                    CreatedDate = DateTime.Now,
                    IsActive = 1
                };

                DBConnector dbConnector = DBConnector.GetDBConnector();
                var result = dbConnector.ExecuteNonQuery("INSERT INTO humpinstance (ID, Name, Owner, CreatedDate, IsActive) VALUES (@ID, @Name, @Owner, @CreatedDate, @IsActive)",
                    instance);
                if (result > 0)
                {
                    LogInformationWithContext("Created HumpInstance with ID {InstanceID}.", instance.ID);
                    return Ok(instance);
                }
                else
                {
                    _logger.LogWarning("Failed to create HumpInstance for user {Username}.", username);
                    return StatusCode(500, "Failed to create instance.");
                }
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error creating HumpInstance.");
                return StatusCode(500, "Internal server error while creating HumpInstance.");
            }
        }

        /// <summary>
        /// Request DTO for creating a HumpInstance. Only fields intended for
        /// user input are exposed; server-managed fields (ID, CreatedDate,
        /// IsActive) cannot be set by the client.
        /// </summary>
        public sealed class CreateHumpInstanceRequest
        {
            public string Name { get; set; } = string.Empty;

            /// <summary>
            /// Optional. Only honored when the caller is an administrator.
            /// </summary>
            public string? Owner { get; set; }
        }

        public sealed class CopyHumpInstanceRequest
        {
            public string SourceInstanceID { get; set; } = string.Empty;

            public string NewInstanceName { get; set; } = string.Empty;

            public string? Owner { get; set; }
        }

        [HttpPost(Name = "CopyHumpInstance")]
        public IActionResult CopyHumpInstance([FromBody] CopyHumpInstanceRequest request)
        {
            if (request == null)
            {
                return BadRequest("Invalid copy request.");
            }

            var sourceInstanceID = request.SourceInstanceID?.Trim() ?? string.Empty;
            var newInstanceName = request.NewInstanceName?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(sourceInstanceID) || string.IsNullOrWhiteSpace(newInstanceName))
            {
                return BadRequest("Source instance ID and new instance name are required.");
            }

            SetLogInstanceId(sourceInstanceID);

            var username = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized("Invalid user context.");
            }

            string normalizedOwner;
            if (IsCurrentUserAdmin())
            {
                var ownerInput = request.Owner;
                if (string.IsNullOrWhiteSpace(ownerInput))
                {
                    DBConnector dbConnector = DBConnector.GetDBConnector();
                    var sourceInstance = (dbConnector.Query<HumpInstance>(
                        "SELECT * FROM humpinstance WHERE ID = @id",
                        new { id = sourceInstanceID }) ?? new List<HumpInstance>()).FirstOrDefault();
                    if (sourceInstance == null)
                    {
                        return NotFound("Source instance not found.");
                    }

                    ownerInput = sourceInstance.Owner;
                }

                if (!TryNormalizeOwnerForPersistence(ownerInput, out normalizedOwner, out var ownerError))
                {
                    return BadRequest(ownerError);
                }
            }
            else
            {
                var authResult = ValidateInstanceOwnershipOrFail(sourceInstanceID);
                if (authResult != null) return authResult;

                normalizedOwner = username;
            }

            var copyResult = _humpInstanceCopyService.CopyInstance(sourceInstanceID, newInstanceName, normalizedOwner);
            if (copyResult.Success && copyResult.CopiedInstance != null)
            {
                SetLogInstanceId(copyResult.CopiedInstance.ID);
                LogInformationWithContext("Copied HumpInstance from {SourceInstanceID} to {TargetInstanceID}.", sourceInstanceID, copyResult.CopiedInstance.ID);
                return Ok(copyResult.CopiedInstance);
            }

            return copyResult.StatusCode switch
            {
                400 => BadRequest(copyResult.ErrorMessage),
                404 => NotFound(copyResult.ErrorMessage),
                _ => StatusCode(copyResult.StatusCode, copyResult.ErrorMessage ?? "Internal server error while copying HumpInstance.")
            };
        }

        [HttpPut(Name = "EditInstance")]
        public IActionResult EditInstance(HumpInstance instance)
        {
            try
            {
                if (instance == null || string.IsNullOrWhiteSpace(instance.ID) || string.IsNullOrWhiteSpace(instance.Name))
                {
                    return BadRequest("Invalid instance payload or missing ID/name.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(instance.ID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;
                var isAdmin = IsCurrentUserAdmin();
                if (!isAdmin && string.IsNullOrWhiteSpace(username))
                {
                    return Unauthorized("Invalid user context.");
                }

                var normalizedIsActive = instance.IsActive == 0 ? 0 : 1;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var trimmedName = instance.Name.Trim();

                int result;
                if (isAdmin)
                {
                    if (!TryNormalizeOwnerForPersistence(instance.Owner, out var normalizedOwner, out var ownerError))
                    {
                        return BadRequest(ownerError);
                    }

                    result = dbConnector.ExecuteNonQuery(
                        "UPDATE humpinstance SET Name = @Name, Owner = @Owner, IsActive = @IsActive WHERE ID = @ID",
                        new { Name = trimmedName, Owner = normalizedOwner, IsActive = normalizedIsActive, ID = instance.ID });
                }
                else
                {
                    result = dbConnector.ExecuteNonQuery(
                        "UPDATE humpinstance SET Name = @Name, IsActive = @IsActive WHERE ID = @ID AND Owner = @Owner",
                        new { Name = trimmedName, IsActive = normalizedIsActive, ID = instance.ID, Owner = username });
                }

                if (result > 0)
                {
                    LogInformationWithContext("Updated HumpInstance with ID {InstanceID}.", instance.ID);
                    return Ok("Instance updated successfully.");
                }
                else
                {
                    _logger.LogWarning("Failed to update HumpInstance.");
                    return StatusCode(500, "Failed to update instance.");
                }
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error updating HumpInstance.");
                return StatusCode(500, "Internal server error while updating HumpInstance.");
            }
        }

        [HttpDelete(Name = "DeleteInstance")]
        public IActionResult DeleteInstance(string id)
        {
            DBConnector? dbConnector = null;
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(id);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;
                var isAdmin = IsCurrentUserAdmin();
                dbConnector = DBConnector.GetDBConnector();
                var existingInstance = (dbConnector.Query<HumpInstance>(
                    "SELECT * FROM humpinstance WHERE ID = @id",
                    new { id }) ?? new List<HumpInstance>()).FirstOrDefault();
                if (existingInstance == null)
                {
                    return NotFound("Instance not found.");
                }

                dbConnector.BeginTransaction();
                DeleteInstanceDependencies(dbConnector, id);
                var result = isAdmin
                    ? dbConnector.ExecuteNonQuery("DELETE FROM humpinstance WHERE ID = @id", new { id })
                    : dbConnector.ExecuteNonQuery("DELETE FROM humpinstance WHERE ID = @id AND Owner = @username", new { id, username });
                if (result > 0)
                {
                    dbConnector.Commit();
                    LogInformationWithContext("Deleted HumpInstance with ID {InstanceID}.", id);
                    return Ok("Instance deleted successfully.");
                }
                else
                {
                    dbConnector.Rollback();
                    _logger.LogWarning("Failed to delete HumpInstance.");
                    return StatusCode(500, "Failed to delete instance.");
                }
            }
            catch (Exception ex)
            {
                dbConnector?.Rollback();
                LogErrorWithContext(ex, "Error deleting HumpInstance.");
                return StatusCode(500, "Internal server error while deleting HumpInstance.");
            }
        }


        [HttpGet(Name = "GetSlopeLines")]
        public IActionResult GetSlopeLines(string instanceID)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(instanceID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var slopeLines = dbConnector.Query<SwitchYard.Hump.SlopeLine>("SELECT * FROM slopeline WHERE instanceID = @instanceID", new { instanceID });
                LogInformationWithContext("Retrieved {SlopeLineCount} SlopeLines.", slopeLines?.Count ?? 0);
                return Ok(slopeLines);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error getting SlopeLines.");
                return StatusCode(500, "Internal server error while getting SlopeLines.");
            }
        }

        [HttpPost(Name = "CreateSlopeLine")]
        public IActionResult CreateSlopeLine(SlopeLine slopeLine)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(slopeLine.InstanceID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                slopeLine.ID = _snowflakeIdGenerator.NextIdString();
                var result = dbConnector.ExecuteNonQuery("INSERT INTO slopeline (ID, InstanceID, Name) VALUES (@ID, @InstanceID, @Name)",
                    new { slopeLine.ID, slopeLine.InstanceID, slopeLine.Name });
                if (result > 0)
                {
                    LogInformationWithContext("Created SlopeLine with ID {SlopeLineID}.", slopeLine.ID);
                    return Ok(slopeLine);
                }
                else
                {
                    _logger.LogWarning("Failed to create SlopeLine with ID {SlopeLineID}.", slopeLine.ID);
                    return StatusCode(500, "Failed to create slope line.");
                }
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error creating SlopeLine.");
                return StatusCode(500, "Internal server error while creating SlopeLine.");
            }
        }

        [HttpPut(Name = "EditSlopeLine")]
        public IActionResult EditSlopeLine(SlopeLine slopeLine)
        {
            try
            {
                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var existing = dbConnector.Query<SlopeLine>("SELECT * FROM slopeline WHERE ID = @id", new { id = slopeLine.ID }).FirstOrDefault();
                if (existing == null)
                {
                    return NotFound("SlopeLine not found.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(existing.InstanceID);
                if (authResult != null) return authResult;

                var result = dbConnector.ExecuteNonQuery("UPDATE slopeline SET Name = @Name WHERE ID = @ID",
                    new { slopeLine.Name, slopeLine.ID });
                if (result > 0)
                {
                    LogInformationWithContext("Updated SlopeLine with ID {SlopeLineID}.", slopeLine.ID);
                    return Ok("SlopeLine updated successfully.");
                }
                else
                {
                    _logger.LogWarning("Failed to update SlopeLine with ID {SlopeLineID}.", slopeLine.ID);
                    return StatusCode(500, "Failed to update slope line.");
                }
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error updating SlopeLine.");
                return StatusCode(500, "Internal server error while updating SlopeLine.");
            }
        }

        [HttpDelete(Name = "DeleteSlopeLine")]
        public IActionResult DeleteSlopeLine(string id)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            try
            {
                var username = User.Identity?.Name;
                var slopeLine = dbConnector.Query<SlopeLine>("SELECT * FROM slopeline WHERE ID = @id", new { id }).FirstOrDefault();
                if (slopeLine == null)
                {
                    return NotFound("SlopeLine not found.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(slopeLine.InstanceID);
                if (authResult != null) return authResult;

                dbConnector.BeginTransaction();
                DeleteSlopeLineDependencies(dbConnector, slopeLine.InstanceID, slopeLine.ID);

                var result = dbConnector.ExecuteNonQuery(
                    "DELETE FROM slopeline WHERE ID = @id AND InstanceID = @instanceID",
                    new { id, instanceID = slopeLine.InstanceID });
                if (result > 0)
                {
                    dbConnector.Commit();
                    LogInformationWithContext("Deleted SlopeLine with ID {SlopeLineID}.", id);
                    return Ok("SlopeLine deleted successfully.");
                }
                else
                {
                    dbConnector.Rollback();
                    _logger.LogWarning("Failed to delete SlopeLine with ID {SlopeLineID}.", id);
                    return StatusCode(500, "Failed to delete slope line.");
                }
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                LogErrorWithContext(ex, "Error deleting SlopeLine.");
                return StatusCode(500, "Internal server error while deleting SlopeLine.");
            }
        }

        public sealed class CopySlopeLineRequest
        {
            public string SourceSlopeLineID { get; set; } = string.Empty;
            public string? NewName { get; set; }
        }

        /// <summary>
        /// 复制溜放线及其全部下属数据（位置点、区段、道岔、减速器）
        /// </summary>
        [HttpPost(Name = "CopySlopeLine")]
        public IActionResult CopySlopeLine([FromBody] CopySlopeLineRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.SourceSlopeLineID))
            {
                return BadRequest("Source slope line ID is required.");
            }

            DBConnector dbConnector = DBConnector.GetDBConnector();
            try
            {
                var source = (dbConnector.Query<SlopeLine>(
                    "SELECT * FROM slopeline WHERE ID = @id",
                    new { id = request.SourceSlopeLineID }) ?? new List<SlopeLine>()).FirstOrDefault();
                if (source == null)
                {
                    return NotFound("SlopeLine not found.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(source.InstanceID);
                if (authResult != null) return authResult;

                var newName = string.IsNullOrWhiteSpace(request.NewName)
                    ? $"{source.Name}副本"
                    : request.NewName.Trim();

                dbConnector.BeginTransaction();

                var newSlopeLineID = _snowflakeIdGenerator.NextIdString();
                dbConnector.ExecuteNonQuery(
                    "INSERT INTO slopeline (ID, InstanceID, Name) VALUES (@ID, @InstanceID, @Name)",
                    new { ID = newSlopeLineID, InstanceID = source.InstanceID, Name = newName });

                // 位置点 ID 保持不变（position 表无主键约束，副本与源溜放线共享原始节点 ID）
                var positionIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
                var sourcePositions = dbConnector.Query<HPosition>(
                    "SELECT * FROM position WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID",
                    new { instanceID = source.InstanceID, slopeLineID = source.ID }) ?? new List<HPosition>();
                foreach (var p in sourcePositions)
                {
                    dbConnector.ExecuteNonQuery(
                        "INSERT INTO position (ID, InstanceID, SlopeLineID, X, Height) VALUES (@ID, @InstanceID, @SlopeLineID, @X, @Height)",
                        new { ID = p.ID, InstanceID = source.InstanceID, SlopeLineID = newSlopeLineID, p.X, p.Height });
                }

                // 区段 ID 也保持不变（positionsegment 的外键引用沿用原值）
                var segmentIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
                var sourceSegments = dbConnector.Query<HPositionSegment>(
                    "SELECT * FROM positionsegment WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID",
                    new { instanceID = source.InstanceID, slopeLineID = source.ID }) ?? new List<HPositionSegment>();
                foreach (var seg in sourceSegments)
                {
                    dbConnector.ExecuteNonQuery(
                        "INSERT INTO positionsegment (ID, InstanceID, SlopeLineID, StartPositionID, EndPositionID, Length, CurveDegree, CurveDirection, LocationParam) VALUES (@ID, @InstanceID, @SlopeLineID, @StartPositionID, @EndPositionID, @Length, @CurveDegree, @CurveDirection, @LocationParam)",
                        new
                        {
                            ID = seg.ID,
                            InstanceID = source.InstanceID,
                            SlopeLineID = newSlopeLineID,
                            StartPositionID = MapId(positionIdMap, seg.StartPositionID),
                            EndPositionID = MapId(positionIdMap, seg.EndPositionID),
                            seg.Length,
                            seg.CurveDegree,
                            seg.CurveDirection,
                            seg.LocationParam
                        });
                }

                var sourceSwitches = dbConnector.Query<SwitchYard.Hump.Switch>(
                    "SELECT * FROM switch WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID",
                    new { instanceID = source.InstanceID, slopeLineID = source.ID }) ?? new List<SwitchYard.Hump.Switch>();
                foreach (var sw in sourceSwitches)
                {
                    dbConnector.ExecuteNonQuery(
                        "INSERT INTO switch (ID, InstanceID, SlopeLineID, BindingPositionID, BindingPositionSegmentID, CurveDegree, Type, Direction, Side) VALUES (@ID, @InstanceID, @SlopeLineID, @BindingPositionID, @BindingPositionSegmentID, @CurveDegree, @Type, @Direction, @Side)",
                        new
                        {
                            ID = _snowflakeIdGenerator.NextIdString(),
                            InstanceID = source.InstanceID,
                            SlopeLineID = newSlopeLineID,
                            BindingPositionID = MapId(positionIdMap, sw.BindingPositionID),
                            BindingPositionSegmentID = MapId(segmentIdMap, sw.BindingPositionSegmentID),
                            sw.CurveDegree,
                            sw.Type,
                            sw.Direction,
                            sw.Side
                        });
                }

                var sourceRetarders = dbConnector.Query<Retarder>(
                    "SELECT * FROM retarder WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID",
                    new { instanceID = source.InstanceID, slopeLineID = source.ID }) ?? new List<Retarder>();
                foreach (var r in sourceRetarders)
                {
                    dbConnector.ExecuteNonQuery(
                        "INSERT INTO retarder (ID, InstanceID, SlopeLineID, BindingPositionSegmentID, Numbers) VALUES (@ID, @InstanceID, @SlopeLineID, @BindingPositionSegmentID, @Numbers)",
                        new
                        {
                            ID = _snowflakeIdGenerator.NextIdString(),
                            InstanceID = source.InstanceID,
                            SlopeLineID = newSlopeLineID,
                            BindingPositionSegmentID = MapId(segmentIdMap, r.BindingPositionSegmentID),
                            r.Numbers
                        });
                }

                dbConnector.Commit();
                LogInformationWithContext("Copied SlopeLine {SourceSlopeLineID} -> {NewSlopeLineID}.", source.ID, newSlopeLineID);
                return Ok(new SlopeLine { ID = newSlopeLineID, InstanceID = source.InstanceID, Name = newName });
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                LogErrorWithContext(ex, "Error copying SlopeLine.");
                return StatusCode(500, "Internal server error while copying SlopeLine.");
            }
        }

        private static string? MapId(Dictionary<string, string> idMap, string? sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId)) return sourceId;
            return idMap.TryGetValue(sourceId, out var mapped) ? mapped : sourceId;
        }

        /// <summary>
        /// 获取驼峰溜放部分的平面布置图
        /// </summary>
        /// <returns></returns>
        [HttpGet(Name = "GetFlatLayout")]
        public IActionResult GetFlatLayout(string instanceID, string slopeLineID)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(instanceID);
                if (authResult != null) return authResult;

                var flatLayout = LoadFlatLayout(instanceID, slopeLineID);

                return Ok(flatLayout);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error retrieving FlatLayout.");
                return StatusCode(500, "Internal server error while retrieving FlatLayout.");
            }
        }

        /// <summary>
        /// 加载驼峰溜放部分的平面布置图
        /// </summary>
        /// <returns></returns>
        private Hump.FlatLayout LoadFlatLayout(string instanceID, string slopeLineID)
        {
            var flatLayout = new SwitchYard.Hump.FlatLayout();
            flatLayout.InstanceID = instanceID;
            flatLayout.SlopeLineID = slopeLineID;

            DBConnector dbConnector = DBConnector.GetDBConnector();
            try
            {
                dbConnector.BeginTransaction();
                flatLayout.PositionList = dbConnector.Query<SwitchYard.Hump.HPosition>("SELECT * FROM position WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID", new { instanceID, slopeLineID });
                flatLayout.PositionSegmentList = dbConnector.Query<SwitchYard.Hump.HPositionSegment>("SELECT * FROM positionsegment WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID", new { instanceID, slopeLineID });
                flatLayout.SwitchList = dbConnector.Query<SwitchYard.Hump.Switch>("SELECT * FROM switch WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID", new { instanceID, slopeLineID });
                flatLayout.RetarderList = dbConnector.Query<SwitchYard.Hump.Retarder>("SELECT * FROM retarder WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID", new { instanceID, slopeLineID });
                dbConnector.Commit();

                foreach (var seg in flatLayout.PositionSegmentList)
                {
                    seg.StartPosition = flatLayout.PositionList.Find(p => p.ID == seg.StartPositionID);
                    seg.EndPosition = flatLayout.PositionList.Find(p => p.ID == seg.EndPositionID);
                }

                foreach (var sw in flatLayout.SwitchList)
                {
                    sw.BindingPosition = flatLayout.PositionList.Find(p => p.ID == sw.BindingPositionID);
                    sw.BindingPositionSegment = flatLayout.PositionSegmentList.Find(s => s.ID == sw.BindingPositionSegmentID);
                }

                foreach (var retarder in flatLayout.RetarderList)
                {
                    retarder.BindingPositionSegment = flatLayout.PositionSegmentList.Find(s => s.ID == retarder.BindingPositionSegmentID);
                }

                LogInformationWithContext("FlatLayout retrieved, slope line {SlopeLineID} with {PositionCount} positions, {SegmentCount} segments, {SwitchCount} switches, and {RetarderCount} retarders.",
                    slopeLineID,
                    flatLayout.PositionList?.Count ?? 0,
                    flatLayout.PositionSegmentList?.Count ?? 0,
                    flatLayout.SwitchList?.Count ?? 0,
                    flatLayout.RetarderList?.Count ?? 0);

                return flatLayout;
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error getting FlatLayout.");
                return null;
            }
        }

        /// <summary>
        /// 保存修改后的平面布置图
        /// </summary>
        /// <param name="flatLayout"></param>
        /// <returns></returns>
        [HttpPut(Name = "EditFlatLayout")]
        public IActionResult EditFlatLayout(SwitchYard.Hump.FlatLayout flatLayout)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();

            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(flatLayout.InstanceID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;

                // Delete existing records
                dbConnector.BeginTransaction();
                dbConnector.ExecuteNonQuery("DELETE FROM retarder WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID", new { instanceID = flatLayout.InstanceID, slopeLineID = flatLayout.SlopeLineID });
                dbConnector.ExecuteNonQuery("DELETE FROM switch WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID", new { instanceID = flatLayout.InstanceID, slopeLineID = flatLayout.SlopeLineID });
                dbConnector.ExecuteNonQuery("DELETE FROM positionsegment WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID", new { instanceID = flatLayout.InstanceID, slopeLineID = flatLayout.SlopeLineID });
                dbConnector.ExecuteNonQuery("DELETE FROM position WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID", new { instanceID = flatLayout.InstanceID, slopeLineID = flatLayout.SlopeLineID });

                // Insert positions
                foreach (var position in flatLayout.PositionList)
                {
                    if (string.IsNullOrEmpty(position.ID))
                    {
                        position.ID = _snowflakeIdGenerator.NextIdString();
                    }
                    dbConnector.ExecuteNonQuery("INSERT INTO position (ID, InstanceID, SlopeLineID, X, Height) VALUES (@ID, @InstanceID, @SlopeLineID, @X, @Height)",
                        new { ID = position.ID, InstanceID = flatLayout.InstanceID, SlopeLineID = flatLayout.SlopeLineID, X = position.X, Height = position.Height });
                }

                // Insert position segments
                foreach (var segment in flatLayout.PositionSegmentList)
                {
                    if (string.IsNullOrEmpty(segment.ID))
                    {
                        segment.ID = _snowflakeIdGenerator.NextIdString();
                    }
                    dbConnector.ExecuteNonQuery("INSERT INTO positionsegment (ID, InstanceID, SlopeLineID, StartPositionID, EndPositionID, Length, CurveDegree, CurveDirection, LocationParam) VALUES (@ID, @InstanceID, @SlopeLineID, @StartPositionID, @EndPositionID, @Length, @CurveDegree, @CurveDirection, @LocationParam)",
                        new { ID = segment.ID, InstanceID = flatLayout.InstanceID, SlopeLineID = flatLayout.SlopeLineID, StartPositionID = segment.StartPositionID, EndPositionID = segment.EndPositionID, Length = segment.Length, CurveDegree = ((HPositionSegment)segment).CurveDegree, CurveDirection = ((HPositionSegment)segment).CurveDirection, LocationParam = ((HPositionSegment)segment).LocationParam });
                }

                // Insert switches
                var swIDSet = flatLayout.SwitchList.Select(sw => sw.ID).ToHashSet();
                if (swIDSet.Count < flatLayout.SwitchList.Count)
                {
                    throw new ApplicationException("Switch ID Duplicated!");
                }

                foreach (var sw in flatLayout.SwitchList)
                {
                    //var id = _snowflakeIdGenerator.NextIdString();
                    dbConnector.ExecuteNonQuery("INSERT INTO switch (ID, InstanceID, SlopeLineID, BindingPositionID, BindingPositionSegmentID, CurveDegree, Type, Direction, Side) VALUES (@ID, @InstanceID, @SlopeLineID, @BindingPositionID, @BindingPositionSegmentID, @CurveDegree, @Type, @Direction, @Side)",
                        new { ID = sw.ID, InstanceID = flatLayout.InstanceID, SlopeLineID = flatLayout.SlopeLineID, BindingPositionID = sw.BindingPositionID, BindingPositionSegmentID = sw.BindingPositionSegmentID, CurveDegree = sw.CurveDegree, Type = sw.Type, Direction = sw.Direction, Side = sw.Side });
                }

                // Insert retarders
                var retarderIDSet = flatLayout.RetarderList.Select(r => r.ID).ToHashSet();
                if (retarderIDSet.Count < flatLayout.RetarderList.Count)
                {
                    throw new ApplicationException("Retarder ID Duplicated!");
                }

                foreach (var retarder in flatLayout.RetarderList)
                {
                    //var id = _snowflakeIdGenerator.NextIdString();
                    dbConnector.ExecuteNonQuery("INSERT INTO retarder (ID, InstanceID, SlopeLineID, BindingPositionSegmentID, Numbers) VALUES (@ID, @InstanceID, @SlopeLineID, @BindingPositionSegmentID, @Numbers)",
                        new { ID = retarder.ID, InstanceID = flatLayout.InstanceID, SlopeLineID = flatLayout.SlopeLineID, BindingPositionSegmentID = retarder.BindingPositionSegmentID, Numbers = retarder.Numbers });
                }
                dbConnector.Commit();
                LogInformationWithContext("Updated FlatLayout, slope line {SlopeLineID}.", flatLayout.SlopeLineID);
                return Ok("FlatLayout updated successfully.");
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                LogErrorWithContext(ex, "Error updating FlatLayout.");
                return StatusCode(500, "Internal server error while updating FlatLayout.");
            }
        }

        [HttpDelete(Name = "DeleteFlatLayout")]
        public IActionResult DeleteFlatLayout(SwitchYard.Hump.FlatLayout flatLayout)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(flatLayout.InstanceID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;

                // Delete existing records
                dbConnector.BeginTransaction();
                dbConnector.ExecuteNonQuery("DELETE FROM retarder WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID", new { instanceID = flatLayout.InstanceID, slopeLineID = flatLayout.SlopeLineID });
                dbConnector.ExecuteNonQuery("DELETE FROM switch WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID", new { instanceID = flatLayout.InstanceID, slopeLineID = flatLayout.SlopeLineID });
                dbConnector.ExecuteNonQuery("DELETE FROM positionsegment WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID", new { instanceID = flatLayout.InstanceID, slopeLineID = flatLayout.SlopeLineID });
                dbConnector.ExecuteNonQuery("DELETE FROM position WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID", new { instanceID = flatLayout.InstanceID, slopeLineID = flatLayout.SlopeLineID });
                dbConnector.Commit();
                LogInformationWithContext("Deleted FlatLayout, slope line {SlopeLineID}.", flatLayout.SlopeLineID);
                return Ok("FlatLayout deleted successfully.");
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                LogErrorWithContext(ex, "Error deleting FlatLayout.");
                return StatusCode(500, "Internal server error while deleting FlatLayout.");
            }
        }

        /// <summary>
        /// 获取车辆概念列表
        /// </summary>
        /// <returns></returns>
        [HttpGet(Name = "GetWagonConcept")]
        public IActionResult GetWagonConcept(string instanceID)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(instanceID);
                if (authResult != null) return authResult;

                var wagonConceptList = LoadWagonConcept(instanceID);
                LogInformationWithContext("WagonConcept retrieved with {WagonConceptCount} entries.", wagonConceptList?.Count ?? 0);
                return Ok(wagonConceptList);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error retrieving WagonConcept.");
                return StatusCode(500, "Internal server error while retrieving WagonConcept.");
            }
        }

        /// <summary>
        /// 创建车辆概念
        /// </summary>
        [HttpPost(Name = "CreateWagonConcept")]
        public IActionResult CreateWagonConcept(WagonConcept wagonConcept)
        {
            try
            {
                if (wagonConcept == null ||
                    string.IsNullOrWhiteSpace(wagonConcept.InstanceID) ||
                    string.IsNullOrWhiteSpace(wagonConcept.TypeName))
                {
                    return BadRequest("Invalid WagonConcept or missing required fields.");
                }

                wagonConcept.InstanceID = wagonConcept.InstanceID.Trim();
                wagonConcept.TypeName = wagonConcept.TypeName.Trim();

                var authResult = ValidateInstanceOwnershipOrFail(wagonConcept.InstanceID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var existing = LoadWagonConceptByTypeName(dbConnector, wagonConcept.InstanceID, wagonConcept.TypeName);
                if (existing != null)
                {
                    return Conflict("WagonConcept already exists.");
                }

                var result = dbConnector.ExecuteNonQuery(
                    "INSERT INTO wagonconcept (InstanceID, TypeName, Length, NetMass, LoadingMass, WindwardArea, AxleNumber, Label, g) VALUES (@InstanceID, @TypeName, @Length, @NetMass, @LoadingMass, @WindwardArea, @AxleNumber, @Label, @g)",
                    new
                    {
                        wagonConcept.InstanceID,
                        wagonConcept.TypeName,
                        wagonConcept.Length,
                        wagonConcept.NetMass,
                        wagonConcept.LoadingMass,
                        wagonConcept.WindwardArea,
                        wagonConcept.AxleNumber,
                        wagonConcept.Label,
                        wagonConcept.g
                    });

                if (result > 0)
                {
                    LogInformationWithContext("Created WagonConcept {TypeName}.", wagonConcept.TypeName);
                    return Ok(wagonConcept);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to create WagonConcept {TypeName} for instance {InstanceID} by user {Username}.",
                        wagonConcept.TypeName,
                        wagonConcept.InstanceID,
                        username);
                    return StatusCode(500, "Failed to create WagonConcept.");
                }
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error creating WagonConcept.");
                return StatusCode(500, "Internal server error while creating WagonConcept.");
            }
        }

        /// <summary>
        /// 更新车辆概念
        /// </summary>
        [HttpPut(Name = "EditWagonConcept")]
        public IActionResult EditWagonConcept(WagonConcept wagonConcept)
        {
            try
            {
                if (wagonConcept == null ||
                    string.IsNullOrWhiteSpace(wagonConcept.InstanceID) ||
                    string.IsNullOrWhiteSpace(wagonConcept.TypeName))
                {
                    return BadRequest("Invalid WagonConcept or missing required fields.");
                }

                wagonConcept.InstanceID = wagonConcept.InstanceID.Trim();
                wagonConcept.TypeName = wagonConcept.TypeName.Trim();

                var authResult = ValidateInstanceOwnershipOrFail(wagonConcept.InstanceID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var existing = LoadWagonConceptByTypeName(dbConnector, wagonConcept.InstanceID, wagonConcept.TypeName);
                if (existing == null)
                {
                    return NotFound("WagonConcept not found.");
                }

                var result = dbConnector.ExecuteNonQuery(
                    "UPDATE wagonconcept SET Length = @Length, NetMass = @NetMass, LoadingMass = @LoadingMass, WindwardArea = @WindwardArea, AxleNumber = @AxleNumber, Label = @Label, g = @g WHERE TypeName = @TypeName AND InstanceID = @InstanceID",
                    new
                    {
                        wagonConcept.Length,
                        wagonConcept.NetMass,
                        wagonConcept.LoadingMass,
                        wagonConcept.WindwardArea,
                        wagonConcept.AxleNumber,
                        wagonConcept.Label,
                        wagonConcept.g,
                        wagonConcept.TypeName,
                        InstanceID = existing.InstanceID
                    });

                if (result > 0)
                {
                    LogInformationWithContext("Updated WagonConcept {TypeName}.", wagonConcept.TypeName);
                    return Ok("WagonConcept updated successfully.");
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to update WagonConcept {TypeName} for instance {InstanceID} by user {Username}.",
                        wagonConcept.TypeName,
                        existing.InstanceID,
                        username);
                    return StatusCode(500, "Failed to update WagonConcept.");
                }
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error updating WagonConcept.");
                return StatusCode(500, "Internal server error while updating WagonConcept.");
            }
        }

        /// <summary>
        /// 删除车辆概念
        /// </summary>
        [HttpDelete(Name = "DeleteWagonConcept")]
        public IActionResult DeleteWagonConcept(string instanceID, string typeName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(instanceID) || string.IsNullOrWhiteSpace(typeName))
                {
                    return BadRequest("InstanceID and TypeName are required.");
                }

                instanceID = instanceID.Trim();
                typeName = typeName.Trim();

                var authResult = ValidateInstanceOwnershipOrFail(instanceID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var existing = LoadWagonConceptByTypeName(dbConnector, instanceID, typeName);
                if (existing == null)
                {
                    return NotFound("WagonConcept not found.");
                }

                var result = dbConnector.ExecuteNonQuery(
                    "DELETE FROM wagonconcept WHERE InstanceID = @instanceID AND TypeName = @typeName",
                    new { instanceID, typeName });
                if (result > 0)
                {
                    LogInformationWithContext("Deleted WagonConcept {TypeName}.", typeName);
                    return Ok("WagonConcept deleted successfully.");
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to delete WagonConcept {TypeName} for instance {InstanceID} by user {Username}.",
                        typeName,
                        existing.InstanceID,
                        username);
                    return StatusCode(500, "Failed to delete WagonConcept.");
                }
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error deleting WagonConcept.");
                return StatusCode(500, "Internal server error while deleting WagonConcept.");
            }
        }

        private List<WagonConcept>? LoadWagonConcept(string instanceID)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            var wagonConceptList = dbConnector.Query<SwitchYard.Hump.WagonConcept>("SELECT * FROM wagonconcept WHERE InstanceID = @instanceID",
                new { instanceID = instanceID });
            return wagonConceptList;
        }

        private WagonConcept? LoadWagonConceptByTypeName(DBConnector dbConnector, string instanceID, string typeName)
        {
            var wagonConceptList = dbConnector.Query<WagonConcept>(
                "SELECT * FROM wagonconcept WHERE InstanceID = @instanceID AND TypeName = @typeName",
                new { instanceID, typeName });
            return wagonConceptList?.FirstOrDefault();
        }

        /// <summary>
        /// 获取运行条件列表
        /// </summary>
        [HttpGet(Name = "GetOperationConditions")]
        public IActionResult GetOperationConditions(string instanceID)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(instanceID);
                if (authResult != null) return authResult;

                DBConnector dbConnector = DBConnector.GetDBConnector();

                var list = dbConnector.Query<OperationCondition>("SELECT * FROM operationcondition WHERE InstanceID = @instanceID", new { instanceID });
                LogInformationWithContext("Retrieved {Count} OperationConditions.", list?.Count ?? 0);
                return Ok(list);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error getting OperationConditions.");
                return StatusCode(500, "Internal server error while getting OperationConditions.");
            }
        }

        private OperationCondition LoadOperationCondition(string instanceID, string id)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            var condition = dbConnector.Query<OperationCondition>("SELECT * FROM operationcondition WHERE InstanceID = @instanceID AND ID = @id", new { instanceID = instanceID, id = id }).FirstOrDefault();
            return condition;
        }

        /// <summary>
        /// 创建运行条件
        /// </summary>
        [HttpPost(Name = "CreateOperationCondition")]
        public IActionResult CreateOperationCondition(OperationCondition condition)
        {
            try
            {
                if (condition == null || string.IsNullOrEmpty(condition.InstanceID))
                {
                    return BadRequest("Invalid OperationCondition or missing InstanceID.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(condition.InstanceID);
                if (authResult != null) return authResult;

                DBConnector dbConnector = DBConnector.GetDBConnector();

                if (string.IsNullOrEmpty(condition.ID))
                {
                    condition.ID = _snowflakeIdGenerator.NextIdString();
                }

                var result = dbConnector.ExecuteNonQuery(
                    "INSERT INTO operationcondition (InstanceID, ID, WagonVelocityOnTop, WagonVelocityOnSlope, WagonVelocityOnYard, WindVelocity, IsHeadWind, AirDensity, Temperature, Name) VALUES (@InstanceID, @ID, @WagonVelocityOnTop, @WagonVelocityOnSlope, @WagonVelocityOnYard, @WindVelocity, @IsHeadWind, @AirDensity, @Temperature, @Name)",
                    new
                    {
                        condition.InstanceID,
                        condition.ID,
                        condition.WagonVelocityOnTop,
                        condition.WagonVelocityOnSlope,
                        condition.WagonVelocityOnYard,
                        condition.WindVelocity,
                        condition.IsHeadWind,
                        condition.AirDensity,
                        condition.Temperature,
                        condition.Name
                    });

                if (result > 0)
                {
                    LogInformationWithContext("Created OperationCondition {ID}.", condition.ID);
                    return Ok(condition);
                }
                else
                {
                    _logger.LogWarning("Failed to create OperationCondition.", condition.InstanceID);
                    return StatusCode(500, "Failed to create OperationCondition.");
                }
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error creating OperationCondition.");
                return StatusCode(500, "Internal server error while creating OperationCondition.");
            }
        }

        /// <summary>
        /// 更新运行条件
        /// </summary>
        [HttpPut(Name = "EditOperationCondition")]
        public IActionResult EditOperationCondition(OperationCondition condition)
        {
            try
            {
                if (condition == null || string.IsNullOrEmpty(condition.ID))
                {
                    return BadRequest("Invalid OperationCondition or missing ID.");
                }

                DBConnector dbConnector = DBConnector.GetDBConnector();
                var existing = dbConnector.Query<OperationCondition>("SELECT * FROM operationcondition WHERE ID = @id", new { id = condition.ID }).FirstOrDefault();
                if (existing == null)
                {
                    return NotFound("OperationCondition not found.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(existing.InstanceID);
                if (authResult != null) return authResult;

                var result = dbConnector.ExecuteNonQuery(
                    "UPDATE operationcondition SET WagonVelocityOnTop = @WagonVelocityOnTop, WagonVelocityOnSlope = @WagonVelocityOnSlope, WagonVelocityOnYard = @WagonVelocityOnYard, WindVelocity = @WindVelocity, IsHeadWind = @IsHeadWind, AirDensity = @AirDensity, Temperature = @Temperature, Name = @Name WHERE ID = @ID",
                    new
                    {
                        condition.WagonVelocityOnTop,
                        condition.WagonVelocityOnSlope,
                        condition.WagonVelocityOnYard,
                        condition.WindVelocity,
                        condition.IsHeadWind,
                        condition.AirDensity,
                        condition.Temperature,
                        condition.Name,
                        ID = condition.ID
                    });

                if (result > 0)
                {
                    LogInformationWithContext("Updated OperationCondition {ID}.", condition.ID);
                    return Ok("OperationCondition updated successfully.");
                }
                else
                {
                    _logger.LogWarning("Failed to update OperationCondition {ID}.", condition.ID, existing.InstanceID);
                    return StatusCode(500, "Failed to update OperationCondition.");
                }
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error updating OperationCondition.");
                return StatusCode(500, "Internal server error while updating OperationCondition.");
            }
        }

        /// <summary>
        /// 删除运行条件
        /// </summary>
        [HttpDelete(Name = "DeleteOperationCondition")]
        public IActionResult DeleteOperationCondition(string id)
        {
            try
            {
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var existing = dbConnector.Query<OperationCondition>("SELECT * FROM operationcondition WHERE ID = @id", new { id }).FirstOrDefault();
                if (existing == null)
                {
                    return NotFound("OperationCondition not found.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(existing.InstanceID);
                if (authResult != null) return authResult;

                var result = dbConnector.ExecuteNonQuery("DELETE FROM operationcondition WHERE ID = @id", new { id });
                if (result > 0)
                {
                    LogInformationWithContext("Deleted OperationCondition {ID}.", id);
                    return Ok("OperationCondition deleted successfully.");
                }
                else
                {
                    _logger.LogWarning("Failed to delete OperationCondition {ID}.", id, existing.InstanceID);
                    return StatusCode(500, "Failed to delete OperationCondition.");
                }
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error deleting OperationCondition.");
                return StatusCode(500, "Internal server error while deleting OperationCondition.");
            }
        }

        /// <summary>
        /// 获取纵断面
        /// </summary>
        /// <returns></returns>
        [HttpGet(Name = "GetSlopeLayout")]
        public IActionResult GetSlopeLayout(string instanceID, string humpSchemeID)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(instanceID);
                if (authResult != null) return authResult;

                var slopeLayout = LoadSlopeLayout(instanceID, humpSchemeID);

                LogInformationWithContext("SlopeLayout retrieved, hump scheme {HumpSchemeID} with {PositionCount} positions and {SegmentCount} segments.",
                    humpSchemeID,
                    slopeLayout.PositionList?.Count ?? 0,
                    slopeLayout.PositionSegmentList?.Count ?? 0);
                return Ok(slopeLayout);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error retrieving SlopeLayout.");
                return StatusCode(500, "Internal server error while retrieving SlopeLayout.");
            }
        }

        private SlopeLayout LoadSlopeLayout(string instanceID, string humpSchemeID)
        {
            var slopeLayout = new SwitchYard.Hump.SlopeLayout();
            DBConnector dbConnector = DBConnector.GetDBConnector();
            slopeLayout.PositionList = dbConnector.Query<SwitchYard.Hump.VPosition>("SELECT * FROM vposition WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID;", new { instanceID = instanceID, humpSchemeID = humpSchemeID });
            slopeLayout.PositionSegmentList = dbConnector.Query<SwitchYard.Hump.VPositionSegment>("SELECT * FROM vpositionsegment WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID", new { instanceID = instanceID, humpSchemeID = humpSchemeID });
            foreach (var seg in slopeLayout.PositionSegmentList)
            {
                seg.StartPosition = slopeLayout.PositionList.Find(p => p.ID == seg.StartPositionID);
                seg.EndPosition = slopeLayout.PositionList.Find(p => p.ID == seg.EndPositionID);
            }
            return slopeLayout;
        }

        private static bool NeedsServerGeneratedId(string? id)
        {
            return string.IsNullOrWhiteSpace(id) || id.StartsWith("tmp-", StringComparison.OrdinalIgnoreCase);
        }

        private static string RemapPositionId(string positionId, Dictionary<string, string> generatedPositionIdMap)
        {
            if (string.IsNullOrWhiteSpace(positionId))
            {
                return positionId;
            }

            if (generatedPositionIdMap.TryGetValue(positionId, out var mappedId))
            {
                return mappedId;
            }

            return positionId;
        }

        /// <summary>
        /// 创建新的纵断面
        /// </summary>
        /// <param name="slopeLayout"></param>
        /// <param name="instanceID"></param>
        /// <param name="humpSchemeID"></param>
        /// <returns></returns>
        [HttpPost(Name = "CreateSlopeLayout")]
        public IActionResult CreateSlopeLayout(SwitchYard.Hump.SlopeLayout slopeLayout, string instanceID, string humpSchemeID)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();

            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(instanceID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;

                dbConnector.BeginTransaction();
                var generatedPositionIdMap = new Dictionary<string, string>();

                // Insert positions
                foreach (var position in slopeLayout.PositionList)
                {
                    var originalPositionId = position.ID;
                    if (NeedsServerGeneratedId(originalPositionId))
                    {
                        position.ID = _snowflakeIdGenerator.NextIdString();
                        if (!string.IsNullOrWhiteSpace(originalPositionId))
                        {
                            generatedPositionIdMap[originalPositionId] = position.ID;
                        }
                    }
                    position.InstanceID = instanceID;
                    position.HumpSchemeID = humpSchemeID;
                    dbConnector.ExecuteNonQuery("INSERT INTO vposition (ID, InstanceID, HumpSchemeID, X, Height) VALUES (@ID, @InstanceID, @HumpSchemeID, @X, @Height)",
                        new { ID = position.ID, InstanceID = instanceID, HumpSchemeID = humpSchemeID, X = position.X, Height = position.Height });
                }

                // Insert position segments
                foreach (var segment in slopeLayout.PositionSegmentList)
                {
                    segment.StartPositionID = RemapPositionId(segment.StartPositionID, generatedPositionIdMap);
                    segment.EndPositionID = RemapPositionId(segment.EndPositionID, generatedPositionIdMap);
                    if (NeedsServerGeneratedId(segment.ID))
                    {
                        segment.ID = _snowflakeIdGenerator.NextIdString();
                    }
                    segment.InstanceID = instanceID;
                    segment.HumpSchemeID = humpSchemeID;
                    dbConnector.ExecuteNonQuery("INSERT INTO vpositionsegment (ID, InstanceID, HumpSchemeID, StartPositionID, EndPositionID, Length, Gradient, Height) VALUES (@ID, @InstanceID, @HumpSchemeID, @StartPositionID, @EndPositionID, @Length, @Gradient, @Height)",
                        new { ID = segment.ID, InstanceID = instanceID, HumpSchemeID = humpSchemeID, StartPositionID = segment.StartPositionID, EndPositionID = segment.EndPositionID, Length = segment.Length, Gradient = ((VPositionSegment)segment).Gradient, Height = ((VPositionSegment)segment).Height });
                }

                dbConnector.Commit();
                LogInformationWithContext("Created SlopeLayout, hump scheme {HumpSchemeID}.", humpSchemeID);
                return Ok(slopeLayout);
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                LogErrorWithContext(ex, "Error creating SlopeLayout.");
                return StatusCode(500, "Internal server error while creating SlopeLayout.");
            }
        }

        /// <summary>
        /// 保存修改后的纵断面
        /// </summary>
        /// <param name="slopeLayout"></param>
        /// <param name="instanceID"></param>
        /// <param name="humpSchemeID"></param>
        /// <returns></returns>
        [HttpPut(Name = "EditSlopeLayout")]
        public IActionResult EditSlopeLayout(SwitchYard.Hump.SlopeLayout slopeLayout, string instanceID, string humpSchemeID)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();

            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(instanceID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;

                // Delete existing records
                dbConnector.BeginTransaction();
                dbConnector.ExecuteNonQuery("DELETE FROM vpositionsegment WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID", new { instanceID, humpSchemeID });
                dbConnector.ExecuteNonQuery("DELETE FROM vposition WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID", new { instanceID, humpSchemeID });
                var generatedPositionIdMap = new Dictionary<string, string>();

                // Insert positions
                foreach (var position in slopeLayout.PositionList)
                {
                    var originalPositionId = position.ID;
                    if (NeedsServerGeneratedId(originalPositionId))
                    {
                        position.ID = _snowflakeIdGenerator.NextIdString();
                        if (!string.IsNullOrWhiteSpace(originalPositionId))
                        {
                            generatedPositionIdMap[originalPositionId] = position.ID;
                        }
                    }
                    position.InstanceID = instanceID;
                    position.HumpSchemeID = humpSchemeID;
                    dbConnector.ExecuteNonQuery("INSERT INTO vposition (ID, InstanceID, HumpSchemeID, X, Height) VALUES (@ID, @InstanceID, @HumpSchemeID, @X, @Height)",
                        new { ID = position.ID, InstanceID = instanceID, HumpSchemeID = humpSchemeID, X = position.X, Height = position.Height });
                }

                // Insert position segments
                foreach (var segment in slopeLayout.PositionSegmentList)
                {
                    segment.StartPositionID = RemapPositionId(segment.StartPositionID, generatedPositionIdMap);
                    segment.EndPositionID = RemapPositionId(segment.EndPositionID, generatedPositionIdMap);
                    if (NeedsServerGeneratedId(segment.ID))
                    {
                        segment.ID = _snowflakeIdGenerator.NextIdString();
                    }
                    segment.InstanceID = instanceID;
                    segment.HumpSchemeID = humpSchemeID;
                    dbConnector.ExecuteNonQuery("INSERT INTO vpositionsegment (ID, InstanceID, HumpSchemeID, StartPositionID, EndPositionID, Length, Gradient, Height) VALUES (@ID, @InstanceID, @HumpSchemeID, @StartPositionID, @EndPositionID, @Length, @Gradient, @Height)",
                        new { ID = segment.ID, InstanceID = instanceID, HumpSchemeID = humpSchemeID, StartPositionID = segment.StartPositionID, EndPositionID = segment.EndPositionID, Length = segment.Length, Gradient = ((VPositionSegment)segment).Gradient, Height = ((VPositionSegment)segment).Height });
                }

                dbConnector.Commit();
                LogInformationWithContext("Updated SlopeLayout, hump scheme {HumpSchemeID}.", humpSchemeID);
                return Ok("SlopeLayout updated successfully.");
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                LogErrorWithContext(ex, "Error updating SlopeLayout.");
                return StatusCode(500, "Internal server error while updating SlopeLayout.");
            }
        }

        /// <summary>
        /// 删除纵断面
        /// </summary>
        /// <param name="instanceID"></param>
        /// <param name="humpSchemeID"></param>
        /// <returns></returns>
        [HttpDelete(Name = "DeleteSlopeLayout")]
        public IActionResult DeleteSlopeLayout(string instanceID, string humpSchemeID)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(instanceID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;

                // Delete existing records
                dbConnector.BeginTransaction();
                dbConnector.ExecuteNonQuery("DELETE FROM vpositionsegment WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID", new { instanceID, humpSchemeID });
                dbConnector.ExecuteNonQuery("DELETE FROM vposition WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID", new { instanceID, humpSchemeID });
                dbConnector.Commit();
                LogInformationWithContext("Deleted SlopeLayout, hump scheme {HumpSchemeID}.", humpSchemeID);
                return Ok("SlopeLayout deleted successfully.");
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                LogErrorWithContext(ex, "Error deleting SlopeLayout.");
                return StatusCode(500, "Internal server error while deleting SlopeLayout.");
            }
        }

        /// <summary>
        /// 执行能高计算
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        [HttpPost(Name = "ExecuteCalculation")]
        public IActionResult ExecuteEnergyHeightCalculation(EnergyCalculationParams parameters)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            try
            {
                // 身份验证
                var authResult = ValidateInstanceOwnershipOrFail(parameters.InstanceID);
                if (authResult != null) return authResult;

                // 载入所有计算参数
                var humpCalculation = GetHumpCalculation(parameters.InstanceID, parameters.HumpSchemeID, parameters.ID);

                var slopeLine = LoadSlopeLine(parameters.InstanceID, parameters.SlopeLineID);
                var flatLayout = LoadFlatLayout(parameters.InstanceID, parameters.SlopeLineID);
                slopeLine.FlatLayout = flatLayout;

                var slopeLayout = LoadSlopeLayout(parameters.InstanceID, parameters.HumpSchemeID);
                var wagonConceptList = LoadWagonConcept(parameters.InstanceID);

                parameters.SlopeLine = slopeLine;
                parameters.Wagon = wagonConceptList.Find(w => w.TypeName == parameters.WagonTypeName);
                parameters.OperationCondition = LoadOperationCondition(parameters.InstanceID, parameters.OperationConditionID);

                humpCalculation.Data = new List<HumpCalculationData>();

                foreach (var p in slopeLayout.PositionList)
                {
                    // 计算动能高
                    var kineticEnergyHeight = HumpEnergyHeightCalculator.CalculateKineticEnergyHeight(flatLayout, slopeLayout, p.X, parameters, p.ID);

                    // 计算阻力能高
                    var resistanceEnergyHeight = HumpEnergyHeightCalculator.CalculateResistanceEnergyHeight(flatLayout, p.X, parameters);

                    // 计算制动能高
                    var breakingEnergyHeight = HumpEnergyHeightCalculator.CalculateBreakingEnergyHeight(flatLayout, p.X, parameters);

                    HumpCalculationData data = new HumpCalculationData()
                    {
                        InstanceID = parameters.InstanceID,
                        HumpSchemeID = parameters.HumpSchemeID,
                        HumpCalculationID = parameters.ID,
                        X = p.X,
                        GravityEnergyHeight = kineticEnergyHeight.GravitationHeight,
                        InitTotalEnergyHeight = kineticEnergyHeight.OrgKineticEnergyHeight,
                        KineticEnergyHeight = kineticEnergyHeight.KineticEnergyHeight,
                        ResistanceEnergyHeight = resistanceEnergyHeight,
                        BreakingEnergyHeight = breakingEnergyHeight
                    };
                    humpCalculation.Data.Add(data);
                }

                // 写入数据库
                if (humpCalculation.Data.Count == 0)
                {
                    // 返回空
                    LogInformationWithContext("No HumpCalculationData to insert, hump scheme {HumpSchemeID}.", parameters.HumpSchemeID);
                    return NoContent();
                }

                dbConnector.BeginTransaction();
                dbConnector.ExecuteNonQuery(
                    "DELETE FROM humpcalculationdata WHERE InstanceID = @InstanceID AND HumpSchemeID = @HumpSchemeID AND HumpCalculationID = @HumpCalculationID",
                    new
                    {
                        parameters.InstanceID,
                        parameters.HumpSchemeID,
                        HumpCalculationID = parameters.ID
                    });

                foreach (var data in humpCalculation.Data)
                {
                    dbConnector.ExecuteNonQuery(
                        "INSERT INTO humpcalculationdata (InstanceID, HumpSchemeID, HumpCalculationID, X, GravityEnergyHeight, ResistanceEnergyHeight, KineticEnergyHeight, BreakingEnergyHeight, InitTotalEnergyHeight) VALUES (@InstanceID, @HumpSchemeID, @HumpCalculationID, @X, @GravityEnergyHeight, @ResistanceEnergyHeight, @KineticEnergyHeight, @BreakingEnergyHeight, @InitTotalEnergyHeight)",
                        new
                        {
                            data.InstanceID,
                            data.HumpSchemeID,
                            data.HumpCalculationID,
                            data.X,
                            data.GravityEnergyHeight,
                            data.ResistanceEnergyHeight,
                            data.KineticEnergyHeight,
                            data.BreakingEnergyHeight,
                            data.InitTotalEnergyHeight
                        });
                }
                dbConnector.Commit();
                LogInformationWithContext("Inserted {DataCount} HumpCalculationData records, hump scheme {HumpSchemeID}, hump calculation {HumpCalculationID}.", humpCalculation.Data?.Count ?? 0, parameters.HumpSchemeID, parameters.ID);

                // 返回计算结果
                LogInformationWithContext("Energy height calculation executed with parameters: {Parameters}.", parameters);
                return Ok(humpCalculation);
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                LogErrorWithContext(ex, "Error calculating resistance.");
                return StatusCode(500, "Internal server error while calculating resistance.");
            }
        }

        /// <summary>
        /// 计算动能能高
        /// </summary>
        /// <param name="parameters">能高计算参数</param>
        /// <returns></returns>
        [HttpPost(Name = "GetKineticEnergyHeight")]
        public IActionResult GetKineticEnergyHeight(EnergyCalculationParams parameters)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(parameters.InstanceID);
                if (authResult != null) return authResult;

                var slopeLine = LoadSlopeLine(parameters.InstanceID, parameters.SlopeLineID);
                var flatLayout = LoadFlatLayout(parameters.InstanceID, parameters.SlopeLineID);
                slopeLine.FlatLayout = flatLayout;

                var slopeLayout = LoadSlopeLayout(parameters.InstanceID, parameters.HumpSchemeID);
                var wagonConceptList = LoadWagonConcept(parameters.InstanceID);


                parameters.Wagon = wagonConceptList.Find(w => w.TypeName == parameters.WagonTypeName);
                parameters.OperationCondition = LoadOperationCondition(parameters.InstanceID, parameters.OperationConditionID);

                var kineticEnergyHeightList = new List<object>();

                foreach (var p in slopeLayout.PositionList)
                {
                    var energyHeightResult = HumpEnergyHeightCalculator.CalculateKineticEnergyHeight(flatLayout, slopeLayout, p.X, parameters, p.ID);
                    kineticEnergyHeightList.Add(new { x = p.X, result = energyHeightResult });
                }
                LogInformationWithContext("Kinetic Energy Height calculated for {PositionCount} positions, slope line {SlopeLineID}, hump scheme {HumpSchemeID}, operation condition {OperationConditionID}.", kineticEnergyHeightList?.Count ?? 0, parameters.SlopeLineID, parameters.HumpSchemeID, parameters.OperationConditionID);
                return Ok(kineticEnergyHeightList);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error calculating Kinetic Energy Height.");
                return StatusCode(500, "Internal server error while calculating Kinetic Energy Height.");
            }
        }

        private SlopeLine LoadSlopeLine(string instanceID, string id)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            var slopeLine = dbConnector.Query<SlopeLine>("SELECT * FROM slopeline WHERE InstanceID = @instanceID AND ID = @id", new { instanceID, id }).FirstOrDefault();
            return slopeLine;
        }

        private const double EnergyHeightControlPointTolerance = 1e-6;

        private static List<double> NormalizeEnergyHeightControlXList(IEnumerable<double> xList)
        {
            var result = new List<double>();

            foreach (var x in xList.Where(double.IsFinite).OrderBy(x => x))
            {
                if (result.Count == 0 || Math.Abs(result[^1] - x) > EnergyHeightControlPointTolerance)
                {
                    result.Add(x);
                }
            }

            return result;
        }

        private static List<double> BuildEnergyHeightControlXList(Hump.FlatLayout flatLayout, SlopeLayout slopeLayout)
        {
            var xList = new List<double>();

            if (flatLayout.PositionList != null)
            {
                xList.AddRange(flatLayout.PositionList.Select(position => position.X));
            }

            if (slopeLayout.PositionList != null)
            {
                xList.AddRange(slopeLayout.PositionList.Select(position => position.X));
            }

            return NormalizeEnergyHeightControlXList(xList);
        }

        /// <summary>
        /// 计算阻力能高
        /// </summary>
        /// <param name="parameters">能高计算参数</param>
        /// <returns></returns>
        [HttpPost(Name = "GetResistanceEnergyHeight")]
        public IActionResult GetResistanceEnergyHeight(EnergyCalculationParams parameters, double? currentX = null)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(parameters.InstanceID);
                if (authResult != null) return authResult;

                var slopeLine = LoadSlopeLine(parameters.InstanceID, parameters.SlopeLineID);
                var flatLayout = LoadFlatLayout(parameters.InstanceID, parameters.SlopeLineID);
                slopeLine.FlatLayout = flatLayout;

                var slopeLayout = LoadSlopeLayout(parameters.InstanceID, parameters.HumpSchemeID);
                var wagonConceptList = LoadWagonConcept(parameters.InstanceID);


                parameters.Wagon = wagonConceptList.Find(w => w.TypeName == parameters.WagonTypeName);
                parameters.OperationCondition = LoadOperationCondition(parameters.InstanceID, parameters.OperationConditionID);

                var resistanceEnergyHeightList = new List<object>();

                if (currentX != null)
                {
                    var energyHeight = HumpEnergyHeightCalculator.CalculateResistanceEnergyHeight(flatLayout, Convert.ToDouble(currentX), parameters);
                    resistanceEnergyHeightList.Add(new { x = currentX, height = Math.Round(energyHeight, 3) });
                }
                else
                {
                    foreach (var x in BuildEnergyHeightControlXList(flatLayout, slopeLayout))
                    {
                        var energyHeight = HumpEnergyHeightCalculator.CalculateResistanceEnergyHeight(flatLayout, x, parameters);
                        resistanceEnergyHeightList.Add(new { x, height = Math.Round(energyHeight, 3) });
                    }
                }
                LogInformationWithContext("Resistance Energy Height calculated for {PositionCount} positions, slope line {SlopeLineID}, hump scheme {HumpSchemeID}, operation condition {OperationConditionID}.", resistanceEnergyHeightList?.Count ?? 0, parameters.SlopeLineID, parameters.HumpSchemeID, parameters.OperationConditionID);
                return Ok(resistanceEnergyHeightList);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error calculating Resistance Energy Height.");
                return StatusCode(500, "Internal server error while calculating Resistance Energy Height.");
            }
        }

        /// <summary>
        /// 计算指定位置阻力能高的明细分解（基本阻力、风阻力、道岔阻力、曲线阻力），并附带计算各项时使用的参数
        /// </summary>
        /// <param name="parameters">能高计算参数</param>
        /// <param name="x">指定位置X坐标</param>
        /// <returns>阻力能高分项明细</returns>
        [HttpPost(Name = "GetResistanceEnergyHeightDetail")]
        public IActionResult GetResistanceEnergyHeightDetail(EnergyCalculationParams parameters, double x)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(parameters.InstanceID);
                if (authResult != null) return authResult;

                var slopeLine = LoadSlopeLine(parameters.InstanceID, parameters.SlopeLineID);
                var flatLayout = LoadFlatLayout(parameters.InstanceID, parameters.SlopeLineID);
                slopeLine.FlatLayout = flatLayout;

                var wagonConceptList = LoadWagonConcept(parameters.InstanceID);
                parameters.Wagon = wagonConceptList.Find(w => w.TypeName == parameters.WagonTypeName);
                parameters.OperationCondition = LoadOperationCondition(parameters.InstanceID, parameters.OperationConditionID);

                var detail = HumpEnergyHeightCalculator.CalculateResistanceEnergyHeightDetail(flatLayout, x, parameters);
                LogInformationWithContext("Resistance Energy Height detail calculated at x={X}, slope line {SlopeLineID}, hump scheme {HumpSchemeID}.", x, parameters.SlopeLineID, parameters.HumpSchemeID);
                return Ok(detail);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error calculating Resistance Energy Height detail.");
                return StatusCode(500, "Internal server error while calculating Resistance Energy Height detail.");
            }
        }

        /// <summary>
        /// 计算制动能高
        /// </summary>
        /// <param name="parameters">能高计算参数</param>
        /// <returns></returns>
        [HttpPost(Name = "GetBreakingEnergyHeight")]
        public IActionResult GetBreakingEnergyHeight(EnergyCalculationParams parameters)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(parameters.InstanceID);
                if (authResult != null) return authResult;

                var flatLayout = LoadFlatLayout(parameters.InstanceID, parameters.SlopeLineID);
                var slopeLayout = LoadSlopeLayout(parameters.InstanceID, parameters.HumpSchemeID);
                var wagonConceptList = LoadWagonConcept(parameters.InstanceID);

                parameters.Wagon = wagonConceptList.Find(w => w.TypeName == parameters.WagonTypeName);
                parameters.OperationCondition = LoadOperationCondition(parameters.InstanceID, parameters.OperationConditionID);

                var breakingEnergyHeightDict = new Dictionary<double, object>();

                var retarderXList = new List<double>();
                if (flatLayout.PositionList?.Count > 0)
                {
                    retarderXList.Add(flatLayout.PositionList[0].X);
                    retarderXList.Add(flatLayout.PositionList.Last().X);
                }

                if (flatLayout.RetarderList != null)
                {
                    retarderXList.AddRange(flatLayout.RetarderList
                        .SelectMany(retarder => new double?[]
                        {
                            retarder.BindingPositionSegment?.StartPosition?.X,
                            retarder.BindingPositionSegment?.EndPosition?.X
                        })
                        .Where(x => x.HasValue)
                        .Select(x => x!.Value));
                }

                var displayXList = NormalizeEnergyHeightControlXList(retarderXList);
                var sampleXList = NormalizeEnergyHeightControlXList(
                    BuildEnergyHeightControlXList(flatLayout, slopeLayout).Concat(displayXList));

                foreach (var x in sampleXList)
                {
                    var kineticEnergyHeight = HumpEnergyHeightCalculator.CalculateKineticEnergyHeight(flatLayout, slopeLayout, x, parameters);

                    breakingEnergyHeightDict.Add(x,
                        new
                        {
                            BreakingEnergyHeight = kineticEnergyHeight.BreakingHeight,
                            GravityEnergyHeight = kineticEnergyHeight.GravitationHeight,
                            KineticEnergyHeight = kineticEnergyHeight.KineticEnergyHeight,
                            Display = displayXList.Any(displayX => Math.Abs(displayX - x) <= EnergyHeightControlPointTolerance)
                        });
                }
                LogInformationWithContext("Breaking Energy Height calculated for {PositionCount} positions, slope line {SlopeLineID}, hump scheme {HumpSchemeID}.", breakingEnergyHeightDict?.Count ?? 0, parameters.SlopeLineID, parameters.HumpSchemeID);
                return Ok(breakingEnergyHeightDict);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error calculating Breaking Energy Height.");
                return StatusCode(500, "Internal server error while calculating Breaking Energy Height.");
            }
        }

        private List<object> GetVelocityList(EnergyCalculationParams parameters)
        {
            var stepSize = 10;

            var flatLayout = LoadFlatLayout(parameters.InstanceID, parameters.SlopeLineID);
            var slopeLayout = LoadSlopeLayout(parameters.InstanceID, parameters.HumpSchemeID);
            var wagonConceptList = LoadWagonConcept(parameters.InstanceID);

            parameters.Wagon = wagonConceptList.Find(w => w.TypeName == parameters.Wagon.TypeName);

            var velocityList = new List<object>();

            var flatXPositionList = flatLayout.PositionList.Select(position => { return position.X; }).ToList();
            var slopeXPositionList = slopeLayout.PositionList.Select(position => { return position.X; }).ToList();
            var xPositionList = flatXPositionList.Union(slopeXPositionList).Distinct().OrderBy(x => x).ToList();

            var normXList = new List<double>();
            for (var x = flatLayout.PositionList.First().X; x < flatLayout.PositionList.Last().X; x += stepSize)
            {
                normXList.Add(x);
            }

            xPositionList = xPositionList.Union(normXList).Distinct().OrderBy(x => x).ToList();

            foreach (var p in xPositionList)
            {
                var energyHeightResult = HumpEnergyHeightCalculator.CalculateKineticEnergyHeight(flatLayout, slopeLayout, p, parameters);
                velocityList.Add(new { x = p, velocity = energyHeightResult.Velocity });
            }

            return velocityList;
        }

        [HttpPost(Name = "GetVelocityCurve")]
        public IActionResult GetVelocityCurve(EnergyCalculationParams parameters)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(parameters.InstanceID);
                if (authResult != null) return authResult;

                var velocityList = GetVelocityList(parameters);
                LogInformationWithContext("Velocity calculated for {PositionCount} positions, slope line {SlopeLineID}, hump scheme {HumpSchemeID}.", velocityList?.Count ?? 0, parameters.SlopeLineID, parameters.HumpSchemeID);
                return Ok(velocityList);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error calculating Velocity.");
                return StatusCode(500, "Internal server error while calculating Velocity.");
            }
        }

        [HttpPost(Name = "GetTimeCurve")]
        public IActionResult GetTimeCurve(EnergyCalculationParams parameters)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(parameters.InstanceID);
                if (authResult != null) return authResult;

                var timeList = new List<object>();
                var velocityList = GetVelocityList(parameters);

                double startX = ((dynamic)velocityList[0]).x;
                double cumulativeTime = 0.0;

                timeList.Add(new { x = startX, time = cumulativeTime });

                for (var i = 1; i < velocityList.Count; i++)
                {
                    var item_0 = velocityList[i - 1];
                    var item_t = velocityList[i];

                    var v0 = ((dynamic)item_0).velocity;
                    var vt = ((dynamic)item_t).velocity;

                    var x0 = ((dynamic)item_0).x;
                    var xt = ((dynamic)item_t).x;

                    double duration = 2 * (xt - x0) / (v0 + vt);
                    cumulativeTime = cumulativeTime + duration;

                    timeList.Add(new { x = xt, time = Math.Round(cumulativeTime, 2) });
                }

                foreach (var item in velocityList)
                {
                }

                LogInformationWithContext("Time calculated for {PositionCount} positions, slope line {SlopeLineID}, hump scheme {HumpSchemeID}.", velocityList?.Count ?? 0, parameters.SlopeLineID, parameters.HumpSchemeID);
                return Ok(timeList);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error calculating Time.");
                return StatusCode(500, "Internal server error while calculating Time.");
            }
        }

        /// <summary>
        /// 获取驼峰方案列表
        /// </summary>
        [HttpGet(Name = "GetHumpSchemes")]
        public IActionResult GetHumpSchemes(string instanceID)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(instanceID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var humpSchemes = dbConnector.Query<HumpScheme>("SELECT * FROM humpscheme WHERE InstanceID = @instanceID", new { instanceID });
                LogInformationWithContext("Retrieved {HumpSchemeCount} HumpSchemes.", humpSchemes?.Count ?? 0);
                return Ok(humpSchemes);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error getting HumpSchemes.");
                return StatusCode(500, "Internal server error while getting HumpSchemes.");
            }
        }

        /// <summary>
        /// 创建驼峰方案
        /// </summary>
        [HttpPost(Name = "CreateHumpScheme")]
        public IActionResult CreateHumpScheme(HumpScheme humpScheme)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            try
            {
                if (humpScheme == null || string.IsNullOrEmpty(humpScheme.InstanceID))
                {
                    _logger.LogWarning("Invalid HumpScheme or missing InstanceID.");
                    return BadRequest("Invalid HumpScheme or missing InstanceID.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(humpScheme.InstanceID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;
                humpScheme.ID = _snowflakeIdGenerator.NextIdString();

                dbConnector.BeginTransaction();
                var result = dbConnector.ExecuteNonQuery("INSERT INTO humpscheme (InstanceID, ID, Name) VALUES (@InstanceID, @ID, @Name)",
                    new { humpScheme.InstanceID, humpScheme.ID, humpScheme.Name });
                if (result > 0)
                {
                    CreateDefaultSlopeLayoutForHumpScheme(dbConnector, humpScheme.InstanceID, humpScheme.ID);
                    dbConnector.Commit();
                    LogInformationWithContext("Created HumpScheme with ID {HumpSchemeID}.", humpScheme.ID);
                    return Ok(humpScheme);
                }
                else
                {
                    dbConnector.Rollback();
                    _logger.LogWarning("Failed to create HumpScheme.", humpScheme.InstanceID, username);
                    return StatusCode(500, "Failed to create HumpScheme.");
                }
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                LogErrorWithContext(ex, "Error creating HumpScheme.");
                return StatusCode(500, "Internal server error while creating HumpScheme.");
            }
        }

        private sealed class SlopeLineRightmostPosition
        {
            public string? ID { get; set; }
            public string? Name { get; set; }
            public double? MaxX { get; set; }
        }

        private double ResolveDefaultHumpSchemeEndX(DBConnector dbConnector, string instanceID)
        {
            const double fallbackEndX = 100.0;
            var endpoints = dbConnector.Query<SlopeLineRightmostPosition>(
                @"SELECT sl.ID, sl.Name, MAX(p.X) AS MaxX
                  FROM slopeline sl
                  LEFT JOIN position p ON p.InstanceID = sl.InstanceID AND p.SlopeLineID = sl.ID
                  WHERE sl.InstanceID = @instanceID
                  GROUP BY sl.ID, sl.Name",
                new { instanceID }) ?? new List<SlopeLineRightmostPosition>();

            var hardLineEndX = endpoints
                .Where(line => !string.IsNullOrWhiteSpace(line.Name) && line.Name.Contains("难行") && line.MaxX.HasValue)
                .Select(line => line.MaxX!.Value)
                .DefaultIfEmpty(double.NaN)
                .Max();
            if (double.IsFinite(hardLineEndX) && hardLineEndX > 0)
            {
                return hardLineEndX;
            }

            var rightmostEndX = endpoints
                .Where(line => line.MaxX.HasValue)
                .Select(line => line.MaxX!.Value)
                .DefaultIfEmpty(double.NaN)
                .Max();
            if (double.IsFinite(rightmostEndX) && rightmostEndX > 0)
            {
                return rightmostEndX;
            }

            return fallbackEndX;
        }

        private void CreateDefaultSlopeLayoutForHumpScheme(DBConnector dbConnector, string instanceID, string humpSchemeID)
        {
            const double startX = 0.0;
            const double startHeight = 3.0;
            const double endHeight = 0.0;

            var endX = ResolveDefaultHumpSchemeEndX(dbConnector, instanceID);
            var length = Math.Max(0, endX - startX);
            var gradient = length > 0 ? (endHeight - startHeight) / length * 1000 : 0;
            var startPositionID = _snowflakeIdGenerator.NextIdString();
            var endPositionID = _snowflakeIdGenerator.NextIdString();
            var segmentID = _snowflakeIdGenerator.NextIdString();

            dbConnector.ExecuteNonQuery(
                "INSERT INTO vposition (ID, InstanceID, HumpSchemeID, X, Height) VALUES (@ID, @InstanceID, @HumpSchemeID, @X, @Height)",
                new { ID = startPositionID, InstanceID = instanceID, HumpSchemeID = humpSchemeID, X = startX, Height = startHeight });
            dbConnector.ExecuteNonQuery(
                "INSERT INTO vposition (ID, InstanceID, HumpSchemeID, X, Height) VALUES (@ID, @InstanceID, @HumpSchemeID, @X, @Height)",
                new { ID = endPositionID, InstanceID = instanceID, HumpSchemeID = humpSchemeID, X = endX, Height = endHeight });
            dbConnector.ExecuteNonQuery(
                "INSERT INTO vpositionsegment (ID, InstanceID, HumpSchemeID, StartPositionID, EndPositionID, Length, Gradient, Height) VALUES (@ID, @InstanceID, @HumpSchemeID, @StartPositionID, @EndPositionID, @Length, @Gradient, @Height)",
                new
                {
                    ID = segmentID,
                    InstanceID = instanceID,
                    HumpSchemeID = humpSchemeID,
                    StartPositionID = startPositionID,
                    EndPositionID = endPositionID,
                    Length = length,
                    Gradient = gradient,
                    Height = endHeight
                });
        }

        /// <summary>
        /// 更新驼峰方案
        /// </summary>
        [HttpPut(Name = "EditHumpScheme")]
        public IActionResult EditHumpScheme(HumpScheme humpScheme)
        {
            try
            {
                if (humpScheme == null || string.IsNullOrEmpty(humpScheme.ID))
                {
                    return BadRequest("Invalid HumpScheme or missing ID.");
                }

                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var existing = dbConnector.Query<HumpScheme>("SELECT * FROM humpscheme WHERE ID = @id", new { id = humpScheme.ID }).FirstOrDefault();
                if (existing == null)
                {
                    _logger.LogWarning("HumpScheme {HumpSchemeID} not found.", humpScheme.ID);
                    return NotFound("HumpScheme not found.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(existing.InstanceID);
                if (authResult != null) return authResult;

                var result = dbConnector.ExecuteNonQuery("UPDATE humpscheme SET Name = @Name WHERE ID = @ID",
                    new { humpScheme.Name, humpScheme.ID });
                if (result > 0)
                {
                    LogInformationWithContext("Updated HumpScheme with ID {HumpSchemeID}.", humpScheme.ID);
                    return Ok("HumpScheme updated successfully.");
                }
                else
                {
                    _logger.LogWarning("Failed to update HumpScheme.", existing.InstanceID, username);
                    return StatusCode(500, "Failed to update HumpScheme.");
                }
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error updating HumpScheme.");
                return StatusCode(500, "Internal server error while updating HumpScheme.");
            }
        }

        /// <summary>
        /// 删除驼峰方案
        /// </summary>
        [HttpDelete(Name = "DeleteHumpScheme")]
        public IActionResult DeleteHumpScheme(string id)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            try
            {
                var username = User.Identity?.Name;
                var humpScheme = dbConnector.Query<HumpScheme>("SELECT * FROM humpscheme WHERE ID = @id", new { id }).FirstOrDefault();
                if (humpScheme == null)
                {
                    _logger.LogWarning("HumpScheme {HumpSchemeID} not found.", id);
                    return NotFound("HumpScheme not found.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(humpScheme.InstanceID);
                if (authResult != null) return authResult;

                dbConnector.BeginTransaction();
                DeleteHumpSchemeDependencies(dbConnector, humpScheme.InstanceID, id);

                var result = dbConnector.ExecuteNonQuery("DELETE FROM humpscheme WHERE ID = @id", new { id });
                if (result > 0)
                {
                    dbConnector.Commit();
                    LogInformationWithContext("Deleted HumpScheme with ID {HumpSchemeID}.", id);
                    return Ok("HumpScheme deleted successfully.");
                }
                else
                {
                    dbConnector.Rollback();
                    _logger.LogWarning("Failed to delete HumpScheme.", humpScheme.InstanceID, username);
                    return StatusCode(500, "Failed to delete HumpScheme.");
                }
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                LogErrorWithContext(ex, "Error deleting HumpScheme.");
                return StatusCode(500, "Internal server error while deleting HumpScheme.");
            }
        }

        public sealed class CopyHumpSchemeRequest
        {
            public string SourceHumpSchemeID { get; set; } = string.Empty;
            public string? NewName { get; set; }
        }

        /// <summary>
        /// 复制驼峰纵断面方案及其全部下属数据（纵断面位置点、区段）
        /// </summary>
        [HttpPost(Name = "CopyHumpScheme")]
        public IActionResult CopyHumpScheme([FromBody] CopyHumpSchemeRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.SourceHumpSchemeID))
            {
                return BadRequest("Source hump scheme ID is required.");
            }

            DBConnector dbConnector = DBConnector.GetDBConnector();
            try
            {
                var source = (dbConnector.Query<HumpScheme>(
                    "SELECT * FROM humpscheme WHERE ID = @id",
                    new { id = request.SourceHumpSchemeID }) ?? new List<HumpScheme>()).FirstOrDefault();
                if (source == null)
                {
                    return NotFound("HumpScheme not found.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(source.InstanceID);
                if (authResult != null) return authResult;

                var newName = string.IsNullOrWhiteSpace(request.NewName)
                    ? $"{source.Name}副本"
                    : request.NewName.Trim();

                dbConnector.BeginTransaction();

                var newHumpSchemeID = _snowflakeIdGenerator.NextIdString();
                dbConnector.ExecuteNonQuery(
                    "INSERT INTO humpscheme (InstanceID, ID, Name) VALUES (@InstanceID, @ID, @Name)",
                    new { InstanceID = source.InstanceID, ID = newHumpSchemeID, Name = newName });

                var vPositionIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
                var sourceVPositions = dbConnector.Query<VPosition>(
                    "SELECT * FROM vposition WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID",
                    new { instanceID = source.InstanceID, humpSchemeID = source.ID }) ?? new List<VPosition>();
                foreach (var vp in sourceVPositions)
                {
                    var newID = _snowflakeIdGenerator.NextIdString();
                    vPositionIdMap[vp.ID] = newID;
                    dbConnector.ExecuteNonQuery(
                        "INSERT INTO vposition (ID, InstanceID, HumpSchemeID, X, Height) VALUES (@ID, @InstanceID, @HumpSchemeID, @X, @Height)",
                        new { ID = newID, InstanceID = source.InstanceID, HumpSchemeID = newHumpSchemeID, vp.X, vp.Height });
                }

                var sourceVSegments = dbConnector.Query<VPositionSegment>(
                    "SELECT * FROM vpositionsegment WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID",
                    new { instanceID = source.InstanceID, humpSchemeID = source.ID }) ?? new List<VPositionSegment>();
                foreach (var seg in sourceVSegments)
                {
                    dbConnector.ExecuteNonQuery(
                        "INSERT INTO vpositionsegment (ID, InstanceID, HumpSchemeID, StartPositionID, EndPositionID, Length, Gradient, Height) VALUES (@ID, @InstanceID, @HumpSchemeID, @StartPositionID, @EndPositionID, @Length, @Gradient, @Height)",
                        new
                        {
                            ID = _snowflakeIdGenerator.NextIdString(),
                            InstanceID = source.InstanceID,
                            HumpSchemeID = newHumpSchemeID,
                            StartPositionID = MapId(vPositionIdMap, seg.StartPositionID),
                            EndPositionID = MapId(vPositionIdMap, seg.EndPositionID),
                            seg.Length,
                            seg.Gradient,
                            seg.Height
                        });
                }

                dbConnector.Commit();
                LogInformationWithContext("Copied HumpScheme {SourceHumpSchemeID} -> {NewHumpSchemeID}.", source.ID, newHumpSchemeID);
                return Ok(new HumpScheme { ID = newHumpSchemeID, InstanceID = source.InstanceID, Name = newName });
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                LogErrorWithContext(ex, "Error copying HumpScheme.");
                return StatusCode(500, "Internal server error while copying HumpScheme.");
            }
        }

        /// <summary>
        /// 获取驼峰计算列表
        /// </summary>
        [HttpGet(Name = "GetHumpCalculations")]
        public IActionResult GetHumpCalculations(string instanceID, string humpSchemeID)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(instanceID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var humpCalculations = dbConnector.Query<HumpCalculation>("SELECT * FROM humpcalculation WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID", new { instanceID = instanceID, humpSchemeID = humpSchemeID }) ?? new List<HumpCalculation>();

                var humpCalculationIds = humpCalculations
                    .Where(c => !string.IsNullOrWhiteSpace(c.ID))
                    .Select(c => c.ID)
                    .ToList();
                var retarderStatusMap = LoadRetarderStatusMap(dbConnector, instanceID, humpCalculationIds);

                foreach (var calculation in humpCalculations)
                {
                    if (!string.IsNullOrWhiteSpace(calculation.ID) && retarderStatusMap.TryGetValue(calculation.ID, out var retarderStatusList))
                    {
                        calculation.RetarderStatusList = retarderStatusList;
                    }
                    else
                    {
                        calculation.RetarderStatusList = new List<RetarderStatus>();
                    }
                }

                LogInformationWithContext("Retrieved {HumpCalculationCount} HumpCalculations, hump scheme {HumpSchemeID}.", humpCalculations.Count, humpSchemeID);
                return Ok(humpCalculations);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error getting HumpCalculations.");
                return StatusCode(500, "Internal server error while getting HumpCalculations.");
            }
        }

        /// <summary>
        /// 创建驼峰计算
        /// </summary>
        [HttpPost(Name = "CreateHumpCalculation")]
        public IActionResult CreateHumpCalculation(HumpCalculation humpCalculation)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            try
            {
                if (humpCalculation == null || string.IsNullOrEmpty(humpCalculation.InstanceID))
                {
                    return BadRequest("Invalid HumpCalculation or missing InstanceID.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(humpCalculation.InstanceID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;
                humpCalculation.ID = _snowflakeIdGenerator.NextIdString();

                dbConnector.BeginTransaction();

                var result = dbConnector.ExecuteNonQuery(
                    "INSERT INTO humpcalculation (InstanceID, HumpSchemeID, ID, WagonType, OperationConditionID, SlopeLineID) VALUES (@InstanceID, @HumpSchemeID, @ID, @WagonType, @OperationConditionID, @SlopeLineID)",
                    new
                    {
                        humpCalculation.InstanceID,
                        humpCalculation.HumpSchemeID,
                        humpCalculation.ID,
                        humpCalculation.WagonType,
                        humpCalculation.OperationConditionID,
                        humpCalculation.SlopeLineID
                    });

                if (result <= 0)
                {
                    dbConnector.Rollback();
                    _logger.LogWarning("Failed to create HumpCalculation.", humpCalculation.InstanceID, username);
                    return StatusCode(500, "Failed to create HumpCalculation.");
                }

                SaveRetarderStatusList(dbConnector, humpCalculation.InstanceID, humpCalculation.ID, humpCalculation.RetarderStatusList);
                dbConnector.Commit();

                LogInformationWithContext("Created HumpCalculation with ID {HumpCalculationID}.", humpCalculation.ID);
                return Ok(humpCalculation);
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                LogErrorWithContext(ex, "Error creating HumpCalculation.");
                return StatusCode(500, "Internal server error while creating HumpCalculation.");
            }
        }

        /// <summary>
        /// 更新驼峰计算
        /// </summary>
        [HttpPut(Name = "EditHumpCalculation")]
        public IActionResult EditHumpCalculation(HumpCalculation humpCalculation)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            try
            {
                if (humpCalculation == null || string.IsNullOrEmpty(humpCalculation.ID))
                {
                    return BadRequest("Invalid HumpCalculation or missing ID.");
                }

                var username = User.Identity?.Name;
                var existing = (dbConnector.Query<HumpCalculation>("SELECT * FROM humpcalculation WHERE ID = @id", new { id = humpCalculation.ID }) ?? new List<HumpCalculation>()).FirstOrDefault();
                if (existing == null)
                {
                    return NotFound("HumpCalculation not found.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(existing.InstanceID);
                if (authResult != null) return authResult;

                dbConnector.BeginTransaction();

                var result = dbConnector.ExecuteNonQuery(
                    "UPDATE humpcalculation SET HumpSchemeID = @HumpSchemeID, WagonType = @WagonType, OperationConditionID = @OperationConditionID, SlopeLineID = @SlopeLineID WHERE ID = @ID",
                    new
                    {
                        humpCalculation.HumpSchemeID,
                        humpCalculation.WagonType,
                        humpCalculation.OperationConditionID,
                        humpCalculation.SlopeLineID,
                        humpCalculation.ID
                    });

                if (result <= 0)
                {
                    dbConnector.Rollback();
                    _logger.LogWarning("Failed to update HumpCalculation.", existing.InstanceID, username);
                    return StatusCode(500, "Failed to update HumpCalculation.");
                }

                SaveRetarderStatusList(dbConnector, existing.InstanceID, humpCalculation.ID, humpCalculation.RetarderStatusList);
                dbConnector.Commit();

                LogInformationWithContext("Updated HumpCalculation with ID {HumpCalculationID}.", humpCalculation.ID);
                return Ok("HumpCalculation updated successfully.");
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                LogErrorWithContext(ex, "Error updating HumpCalculation.");
                return StatusCode(500, "Internal server error while updating HumpCalculation.");
            }
        }

        /// <summary>
        /// 删除驼峰计算
        /// </summary>
        [HttpDelete(Name = "DeleteHumpCalculation")]
        public IActionResult DeleteHumpCalculation(string instanceID, string humpSchemeID, string id)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            try
            {
                var username = User.Identity?.Name;
                var humpCalculation = (dbConnector.Query<HumpCalculation>("SELECT * FROM humpcalculation WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID AND ID = @id", new { instanceID = instanceID, humpSchemeID = humpSchemeID, id = id }) ?? new List<HumpCalculation>()).FirstOrDefault();
                if (humpCalculation == null)
                {
                    return NotFound("HumpCalculation not found.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(humpCalculation.InstanceID);
                if (authResult != null) return authResult;

                dbConnector.BeginTransaction();
                DeleteHumpCalculationArtifactsByCalculation(dbConnector, humpCalculation.InstanceID, humpCalculation.HumpSchemeID, id);

                var result = dbConnector.ExecuteNonQuery("DELETE FROM humpcalculation WHERE ID = @id", new { id });
                if (result > 0)
                {
                    dbConnector.Commit();
                    LogInformationWithContext("Deleted HumpCalculation with ID {HumpCalculationID}.", id);
                    return Ok("HumpCalculation deleted successfully.");
                }
                else
                {
                    dbConnector.Rollback();
                    _logger.LogWarning("Failed to delete HumpCalculation.", humpCalculation.InstanceID, username);
                    return StatusCode(500, "Failed to delete HumpCalculation.");
                }
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                LogErrorWithContext(ex, "Error deleting HumpCalculation.");
                return StatusCode(500, "Internal server error while deleting HumpCalculation.");
            }
        }

        /// <summary>
        /// 根据ID获取单个驼峰计算
        /// </summary>
        [HttpGet(Name = "GetHumpCalculationById")]
        public IActionResult GetHumpCalculationById(string instanceID, string humpSchemeID, string id)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(instanceID);
                if (authResult != null) return authResult;
                var humpCalculation = GetHumpCalculation(instanceID, humpSchemeID, id);
                LogInformationWithContext("Retrieved HumpCalculation with ID {HumpCalculationID}.", id);
                return Ok(humpCalculation);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error getting HumpCalculation with ID {HumpCalculationID}.", id);
                return StatusCode(500, "Internal server error while getting HumpCalculation.");
            }
        }

        private HumpCalculation GetHumpCalculation(string instanceID, string humpSchemeID, string id)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            var humpCalculation = (dbConnector.Query<HumpCalculation>("SELECT * FROM humpcalculation WHERE ID = @id AND InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID", new { id = id, instanceID = instanceID, humpSchemeID = humpSchemeID }) ?? new List<HumpCalculation>()).FirstOrDefault();
            if (humpCalculation != null)
            {
                humpCalculation.RetarderStatusList = LoadRetarderStatusList(dbConnector, instanceID, humpCalculation.ID);
            }
            return humpCalculation;
        }

        private Dictionary<string, List<RetarderStatus>> LoadRetarderStatusMap(DBConnector dbConnector, string instanceID, List<string> humpCalculationIds)
        {
            var retarderStatusMap = new Dictionary<string, List<RetarderStatus>>();
            if (humpCalculationIds == null || humpCalculationIds.Count == 0)
            {
                return retarderStatusMap;
            }

            var rows = dbConnector.Query<RetarderStatus>(
                "SELECT HumpCalculationID, RetarderID, COALESCE(IsActivated, 0) AS IsActivated, COALESCE(Output, 0) AS Output, COALESCE(TotalEnergyHeight, 0) AS TotalEnergyHeight FROM retarderstatus WHERE InstanceID = @instanceID AND HumpCalculationID IN @humpCalculationIds",
                new { instanceID, humpCalculationIds }) ?? new List<RetarderStatus>();

            retarderStatusMap = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.HumpCalculationID))
                .GroupBy(r => r.HumpCalculationID!)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(r => new RetarderStatus
                    {
                        RetarderID = r.RetarderID ?? string.Empty,
                        IsActivated = r.IsActivated,
                        Output = r.Output,
                        TotalEnergyHeight = r.TotalEnergyHeight
                    }).ToList());

            return retarderStatusMap;
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

        /// <summary>
        /// 获取追踪间隔检算方案列表
        /// </summary>
        private void DeleteInstanceDependencies(DBConnector dbConnector, string instanceID)
        {
            var parameters = new { instanceID };

            // Delete the most dependent records first so the order remains safe
            // even if database-level foreign keys are added later.
            dbConnector.ExecuteNonQuery(
                "DELETE FROM headwaycheckdata WHERE InstanceID = @instanceID",
                parameters);
            dbConnector.ExecuteNonQuery(
                "DELETE FROM headwaycheckresult WHERE InstanceID = @instanceID",
                parameters);
            dbConnector.ExecuteNonQuery(
                "DELETE FROM headwaycheckwagon WHERE InstanceID = @instanceID",
                parameters);
            dbConnector.ExecuteNonQuery(
                "DELETE FROM headwaycheckscheme WHERE InstanceID = @instanceID",
                parameters);

            dbConnector.ExecuteNonQuery(
                "DELETE FROM retarderstatus WHERE InstanceID = @instanceID",
                parameters);
            dbConnector.ExecuteNonQuery(
                "DELETE FROM humpcalculationdata WHERE InstanceID = @instanceID",
                parameters);
            dbConnector.ExecuteNonQuery(
                "DELETE FROM humpcalculation WHERE InstanceID = @instanceID",
                parameters);

            dbConnector.ExecuteNonQuery(
                "DELETE FROM vpositionsegment WHERE InstanceID = @instanceID",
                parameters);
            dbConnector.ExecuteNonQuery(
                "DELETE FROM vposition WHERE InstanceID = @instanceID",
                parameters);
            dbConnector.ExecuteNonQuery(
                "DELETE FROM humpscheme WHERE InstanceID = @instanceID",
                parameters);

            dbConnector.ExecuteNonQuery(
                "DELETE FROM retarder WHERE InstanceID = @instanceID",
                parameters);
            dbConnector.ExecuteNonQuery(
                "DELETE FROM switch WHERE InstanceID = @instanceID",
                parameters);
            dbConnector.ExecuteNonQuery(
                "DELETE FROM positionsegment WHERE InstanceID = @instanceID",
                parameters);
            dbConnector.ExecuteNonQuery(
                "DELETE FROM position WHERE InstanceID = @instanceID",
                parameters);
            dbConnector.ExecuteNonQuery(
                "DELETE FROM slopeline WHERE InstanceID = @instanceID",
                parameters);

            dbConnector.ExecuteNonQuery(
                "DELETE FROM wagonconcept WHERE InstanceID = @instanceID",
                parameters);
            dbConnector.ExecuteNonQuery(
                "DELETE FROM operationcondition WHERE InstanceID = @instanceID",
                parameters);
        }

        private void DeleteHumpSchemeDependencies(DBConnector dbConnector, string instanceID, string humpSchemeID)
        {
            var headwayCheckIds = (dbConnector.Query<HeadwayCheckScheme>(
                "SELECT * FROM headwaycheckscheme WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID",
                new { instanceID, humpSchemeID }) ?? new List<HeadwayCheckScheme>())
                .Select(scheme => scheme.ID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            DeleteHeadwayCheckArtifacts(dbConnector, instanceID, headwayCheckIds);
            dbConnector.ExecuteNonQuery(
                "DELETE FROM headwaycheckscheme WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID",
                new { instanceID, humpSchemeID });

            var humpCalculationIds = (dbConnector.Query<HumpCalculation>(
                "SELECT * FROM humpcalculation WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID",
                new { instanceID, humpSchemeID }) ?? new List<HumpCalculation>())
                .Select(calculation => calculation.ID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            DeleteHumpCalculationArtifactsByScheme(dbConnector, instanceID, humpSchemeID, humpCalculationIds);

            dbConnector.ExecuteNonQuery(
                "DELETE FROM vpositionsegment WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID",
                new { instanceID, humpSchemeID });
            dbConnector.ExecuteNonQuery(
                "DELETE FROM vposition WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID",
                new { instanceID, humpSchemeID });
        }

        private void DeleteSlopeLineDependencies(DBConnector dbConnector, string instanceID, string slopeLineID)
        {
            var headwayCheckIds = (dbConnector.Query<HeadwayCheckScheme>(
                "SELECT * FROM headwaycheckscheme WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID",
                new { instanceID, slopeLineID }) ?? new List<HeadwayCheckScheme>())
                .Select(scheme => scheme.ID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            DeleteHeadwayCheckArtifacts(dbConnector, instanceID, headwayCheckIds);
            dbConnector.ExecuteNonQuery(
                "DELETE FROM headwaycheckscheme WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID",
                new { instanceID, slopeLineID });

            var humpCalculationIds = (dbConnector.Query<HumpCalculation>(
                "SELECT * FROM humpcalculation WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID",
                new { instanceID, slopeLineID }) ?? new List<HumpCalculation>())
                .Select(calculation => calculation.ID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            DeleteHumpCalculationArtifactsByIds(dbConnector, instanceID, humpCalculationIds);
            dbConnector.ExecuteNonQuery(
                "DELETE FROM humpcalculation WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID",
                new { instanceID, slopeLineID });

            dbConnector.ExecuteNonQuery(
                "DELETE FROM retarder WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID",
                new { instanceID, slopeLineID });
            dbConnector.ExecuteNonQuery(
                "DELETE FROM switch WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID",
                new { instanceID, slopeLineID });
            dbConnector.ExecuteNonQuery(
                "DELETE FROM positionsegment WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID",
                new { instanceID, slopeLineID });
            dbConnector.ExecuteNonQuery(
                "DELETE FROM position WHERE InstanceID = @instanceID AND SlopeLineID = @slopeLineID",
                new { instanceID, slopeLineID });
        }

        private void DeleteHumpCalculationArtifactsByIds(DBConnector dbConnector, string instanceID, List<string> humpCalculationIds)
        {
            if (humpCalculationIds == null || humpCalculationIds.Count == 0)
            {
                return;
            }

            dbConnector.ExecuteNonQuery(
                "DELETE FROM humpcalculationdata WHERE InstanceID = @instanceID AND HumpCalculationID IN @humpCalculationIds",
                new { instanceID, humpCalculationIds });
            dbConnector.ExecuteNonQuery(
                "DELETE FROM retarderstatus WHERE InstanceID = @instanceID AND HumpCalculationID IN @humpCalculationIds",
                new { instanceID, humpCalculationIds });
        }

        private void DeleteHumpCalculationArtifactsByScheme(DBConnector dbConnector, string instanceID, string humpSchemeID, List<string> humpCalculationIds)
        {
            dbConnector.ExecuteNonQuery(
                "DELETE FROM humpcalculationdata WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID",
                new { instanceID, humpSchemeID });

            if (humpCalculationIds != null && humpCalculationIds.Count > 0)
            {
                dbConnector.ExecuteNonQuery(
                    "DELETE FROM retarderstatus WHERE InstanceID = @instanceID AND HumpCalculationID IN @humpCalculationIds",
                    new { instanceID, humpCalculationIds });
            }

            dbConnector.ExecuteNonQuery(
                "DELETE FROM humpcalculation WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID",
                new { instanceID, humpSchemeID });
        }

        private void DeleteHumpCalculationArtifactsByCalculation(DBConnector dbConnector, string instanceID, string humpSchemeID, string humpCalculationID)
        {
            dbConnector.ExecuteNonQuery(
                "DELETE FROM humpcalculationdata WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID AND HumpCalculationID = @humpCalculationID",
                new { instanceID, humpSchemeID, humpCalculationID });
            dbConnector.ExecuteNonQuery(
                "DELETE FROM retarderstatus WHERE InstanceID = @instanceID AND HumpCalculationID = @humpCalculationID",
                new { instanceID, humpCalculationID });
        }

        private void DeleteHeadwayCheckArtifacts(DBConnector dbConnector, string instanceID, List<string> headwayCheckIds)
        {
            if (headwayCheckIds == null || headwayCheckIds.Count == 0)
            {
                return;
            }

            dbConnector.ExecuteNonQuery(
                "DELETE FROM headwaycheckdata WHERE InstanceID = @instanceID AND HeadwayCheckID IN @headwayCheckIds",
                new { instanceID, headwayCheckIds });
            dbConnector.ExecuteNonQuery(
                "DELETE FROM headwaycheckresult WHERE InstanceID = @instanceID AND HeadwayCheckID IN @headwayCheckIds",
                new { instanceID, headwayCheckIds });
            dbConnector.ExecuteNonQuery(
                "DELETE FROM headwaycheckwagon WHERE InstanceID = @instanceID AND HeadwayCheckID IN @headwayCheckIds",
                new { instanceID, headwayCheckIds });
        }

        /// <summary>
        /// Gets the headway check schemes for an instance.
        /// </summary>
        [HttpGet(Name = "GetHeadwayCheckSchemes")]
        public IActionResult GetHeadwayCheckSchemes(string instanceID)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(instanceID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var schemes = dbConnector.Query<HeadwayCheckScheme>("SELECT * FROM headwaycheckscheme WHERE InstanceID = @instanceID", new { instanceID });
                LogInformationWithContext("Retrieved {SchemeCount} HeadwayCheckSchemes.", schemes?.Count ?? 0);
                return Ok(schemes);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error getting HeadwayCheckSchemes.");
                return StatusCode(500, "Internal server error while getting HeadwayCheckSchemes.");
            }
        }

        /// <summary>
        /// 创建追踪间隔检算方案
        /// /// </summary>
        [HttpPost(Name = "CreateHeadwayCheckScheme")]
        public IActionResult CreateHeadwayCheckScheme(HeadwayCheckScheme scheme)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            try
            {
                if (scheme == null || string.IsNullOrEmpty(scheme.InstanceID))
                {
                    return BadRequest("Invalid HeadwayCheckScheme or missing InstanceID.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(scheme.InstanceID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;
                scheme.ID = _snowflakeIdGenerator.NextIdString();

                dbConnector.BeginTransaction();

                var result = dbConnector.ExecuteNonQuery(
                    "INSERT INTO headwaycheckscheme (InstanceID, ID, Name, HumpSchemeID, WagonVelocityOnTop, SlopeLineID) VALUES (@InstanceID, @ID, @Name, @HumpSchemeID, @WagonVelocityOnTop, @SlopeLineID)",
                    new
                    {
                        scheme.InstanceID,
                        scheme.ID,
                        scheme.Name,
                        scheme.HumpSchemeID,
                        scheme.WagonVelocityOnTop,
                        scheme.SlopeLineID
                    });

                if (result <= 0)
                {
                    dbConnector.Rollback();
                    _logger.LogWarning("Failed to create HeadwayCheckScheme.", scheme.InstanceID, username);
                    return StatusCode(500, "Failed to create HeadwayCheckScheme.");
                }

                if (scheme.WagonList != null && scheme.WagonList.Count > 0)
                {
                    foreach (var wagon in scheme.WagonList)
                    {
                        dbConnector.ExecuteNonQuery(
                            "INSERT INTO headwaycheckwagon (InstanceID, HeadwayCheckID, Sequence, HumpCalculationID) VALUES (@InstanceID, @HeadwayCheckID, @Sequence, @HumpCalculationID)",
                            new
                            {
                                InstanceID = scheme.InstanceID,
                                HeadwayCheckID = scheme.ID,
                                wagon.Sequence,
                                wagon.HumpCalculationID
                            });
                    }
                }

                dbConnector.Commit();
                LogInformationWithContext("Created HeadwayCheckScheme with ID {SchemeID} and {WagonCount} wagons.", scheme.ID, scheme.WagonList?.Count ?? 0);
                return Ok(scheme);
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                LogErrorWithContext(ex, "Error creating HeadwayCheckScheme.");
                return StatusCode(500, "Internal server error while creating HeadwayCheckScheme.");
            }
        }

        /// <summary>
        /// 更新追踪间隔检算方案
        /// </summary>
        [HttpPut(Name = "EditHeadwayCheckScheme")]
        public IActionResult EditHeadwayCheckScheme(HeadwayCheckScheme scheme)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            try
            {
                if (scheme == null || string.IsNullOrEmpty(scheme.ID))
                {
                    return BadRequest("Invalid HeadwayCheckScheme or missing ID.");
                }

                var username = User.Identity?.Name;
                var existing = dbConnector.Query<HeadwayCheckScheme>("SELECT * FROM headwaycheckscheme WHERE ID = @id", new { id = scheme.ID }).FirstOrDefault();
                if (existing == null)
                {
                    return NotFound("HeadwayCheckScheme not found.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(existing.InstanceID);
                if (authResult != null) return authResult;

                dbConnector.BeginTransaction();

                var result = dbConnector.ExecuteNonQuery(
                    "UPDATE headwaycheckscheme SET Name = @Name, HumpSchemeID = @HumpSchemeID, WagonVelocityOnTop = @WagonVelocityOnTop, SlopeLineID = @SlopeLineID WHERE ID = @ID",
                    new
                    {
                        scheme.Name,
                        scheme.HumpSchemeID,
                        scheme.WagonVelocityOnTop,
                        scheme.SlopeLineID,
                        scheme.ID
                    });

                if (result <= 0)
                {
                    dbConnector.Rollback();
                    _logger.LogWarning("Failed to update HeadwayCheckScheme.", existing.InstanceID, username);
                    return StatusCode(500, "Failed to update HeadwayCheckScheme.");
                }

                dbConnector.ExecuteNonQuery("DELETE FROM headwaycheckwagon WHERE InstanceID = @instanceID AND HeadwayCheckID = @headwayCheckID",
                    new { instanceID = existing.InstanceID, headwayCheckID = scheme.ID });

                if (scheme.WagonList != null && scheme.WagonList.Count > 0)
                {
                    foreach (var wagon in scheme.WagonList)
                    {
                        dbConnector.ExecuteNonQuery(
                            "INSERT INTO headwaycheckwagon (InstanceID, HeadwayCheckID, Sequence, HumpCalculationID) VALUES (@InstanceID, @HeadwayCheckID, @Sequence, @HumpCalculationID)",
                            new
                            {
                                InstanceID = existing.InstanceID,
                                HeadwayCheckID = scheme.ID,
                                wagon.Sequence,
                                wagon.HumpCalculationID
                            });
                    }
                }

                dbConnector.Commit();
                LogInformationWithContext("Updated HeadwayCheckScheme with ID {SchemeID} and {WagonCount} wagons.", scheme.ID, scheme.WagonList?.Count ?? 0);
                return Ok("HeadwayCheckScheme updated successfully.");
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                LogErrorWithContext(ex, "Error updating HeadwayCheckScheme.");
                return StatusCode(500, "Internal server error while updating HeadwayCheckScheme.");
            }
        }

        /// <summary>
        /// 删除追踪间隔检算方案
        /// </summary>
        [HttpDelete(Name = "DeleteHeadwayCheckScheme")]
        public IActionResult DeleteHeadwayCheckScheme(string id)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            try
            {
                var username = User.Identity?.Name;
                var scheme = dbConnector.Query<HeadwayCheckScheme>("SELECT * FROM headwaycheckscheme WHERE ID = @id", new { id }).FirstOrDefault();
                if (scheme == null)
                {
                    return NotFound("HeadwayCheckScheme not found.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(scheme.InstanceID);
                if (authResult != null) return authResult;

                dbConnector.BeginTransaction();
                DeleteHeadwayCheckArtifacts(dbConnector, scheme.InstanceID, new List<string> { id });

                var result = dbConnector.ExecuteNonQuery("DELETE FROM headwaycheckscheme WHERE ID = @id", new { id });

                if (result > 0)
                {
                    dbConnector.Commit();
                    LogInformationWithContext("Deleted HeadwayCheckScheme with ID {SchemeID}.", id);
                    return Ok("HeadwayCheckScheme deleted successfully.");
                }
                else
                {
                    dbConnector.Rollback();
                    _logger.LogWarning("Failed to delete HeadwayCheckScheme.", scheme.InstanceID, username);
                    return StatusCode(500, "Failed to delete HeadwayCheckScheme.");
                }
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                LogErrorWithContext(ex, "Error deleting HeadwayCheckScheme.");
                return StatusCode(500, "Internal server error while deleting HeadwayCheckScheme.");
            }
        }

        /// <summary>
        /// 加载追踪间隔检算方案（包含车辆列表）
        /// </summary>
        private HeadwayCheckScheme LoadHeadwayCheckScheme(string instanceID, string id)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            var scheme = dbConnector.Query<HeadwayCheckScheme>("SELECT * FROM headwaycheckscheme WHERE InstanceID = @instanceID AND ID = @id", new { instanceID, id }).FirstOrDefault();

            if (scheme != null)
            {
                scheme.WagonList = dbConnector.Query<HeadwayCheckWagon>("SELECT * FROM headwaycheckwagon WHERE InstanceID = @instanceID AND HeadwayCheckID = @headwayCheckID ORDER BY Sequence",
                    new { instanceID, headwayCheckID = scheme.ID });
            }

            foreach (var hcWagon in scheme.WagonList)
            {
                hcWagon.HumpCalculation = GetHumpCalculation(instanceID, scheme.HumpSchemeID, hcWagon.HumpCalculationID);
            }

            return scheme;
        }

        /// <summary>
        /// 根据ID获取单个追踪间隔检算方案
        /// </summary>
        [HttpGet(Name = "GetHeadwayCheckSchemeById")]
        public IActionResult GetHeadwayCheckSchemeById(string instanceID, string id)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(instanceID);
                if (authResult != null) return authResult;

                var scheme = LoadHeadwayCheckScheme(instanceID, id);
                if (scheme == null)
                {
                    return NotFound("HeadwayCheckScheme not found.");
                }

                LogInformationWithContext("Retrieved HeadwayCheckScheme with ID {SchemeID} and {WagonCount} wagons.", id, scheme.WagonList?.Count ?? 0);
                return Ok(scheme);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error getting HeadwayCheckScheme with ID {SchemeID}.", id);
                return StatusCode(500, "Internal server error while getting HeadwayCheckScheme.");
            }
        }

        /// <summary>
        /// 计算勾车溜放的速度曲线
        /// </summary>
        /// <param name="instanceID"></param>
        /// <param name="headwayCheckSchemeID"></param>
        /// <returns></returns>
        [HttpGet(Name = "CalculateSpeedProfile")]
        public IActionResult CalculateSpeedProfile(string instanceID, string headwayCheckSchemeID, double spaceStepSize)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(instanceID);
                if (authResult != null) return authResult;

                var scheme = LoadHeadwayCheckScheme(instanceID, headwayCheckSchemeID);
                var flatLayout = LoadFlatLayout(instanceID, scheme.SlopeLineID);
                var slopeLayout = LoadSlopeLayout(instanceID, scheme.HumpSchemeID);
                var slopeLine = LoadSlopeLine(instanceID, scheme.SlopeLineID);
                var wagonConceptList = LoadWagonConcept(instanceID);

                var speedProfileList = new List<HeadwayCheckWagonSpeedProfile>();

                foreach (var hcWagon in scheme.WagonList)  // 分别对每勾车计算速度曲线
                {
                    var humpCalc = hcWagon.HumpCalculation;
                    var operationCondition = LoadOperationCondition(instanceID, humpCalc.OperationConditionID);

                    hcWagon.EnergyCalculationParams = new EnergyCalculationParams
                    {
                        InstanceID = instanceID,
                        HumpSchemeID = scheme.HumpSchemeID,
                        ID = humpCalc.ID,
                        SlopeLineID = humpCalc.SlopeLineID,
                        SlopeLine = slopeLine,
                        WagonTypeName = humpCalc.WagonType,
                        Wagon = wagonConceptList?.Find(w => w.TypeName == humpCalc.WagonType),
                        OperationConditionID = humpCalc.OperationConditionID,
                        OperationCondition = operationCondition,
                        RetarderStatusList = humpCalc.RetarderStatusList // TODO: 如果需要减速器状态，需要从HumpCalculation中获取RetarderStatusID并加载
                    };

                    var speedProfile = SpeedProfileGenerator.Generate(hcWagon, flatLayout, slopeLayout, spaceStepSize);

                    speedProfileList.Add(speedProfile);
                }

                LogInformationWithContext("Calculated speed profile for HeadwayCheckScheme with ID {SchemeID}.", headwayCheckSchemeID);
                return Ok(speedProfileList);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error calculating speed profile with HeadwayCheckScheme ID {SchemeID}.", headwayCheckSchemeID);
                return StatusCode(500, "Internal server error while calculating speed profile.");
            }
        }

        /// <summary>
        /// 计算运行时间
        /// </summary>
        /// <param name="instanceID">驼峰计算实例ID</param>
        /// <param name="headwayCheckSchemeID">驼峰检算方案ID</param>
        /// <returns></returns>
        [HttpGet(Name = "CalculateRunningTime")]
        public IActionResult CalculateRunningTime(string instanceID, string headwayCheckSchemeID)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(instanceID);
                if (authResult != null) return authResult;

                var scheme = LoadHeadwayCheckScheme(instanceID, headwayCheckSchemeID);
                var flatLayout = LoadFlatLayout(instanceID, scheme.SlopeLineID);
                var slopeLayout = LoadSlopeLayout(instanceID, scheme.HumpSchemeID);

                var wagonConceptList = LoadWagonConcept(instanceID);
                var slopeLine = LoadSlopeLine(instanceID, scheme.SlopeLineID);

                foreach (var hcWagon in scheme.WagonList)  // 分别对每勾车计算速度曲线
                {
                    var humpCalc = hcWagon.HumpCalculation;
                    var operationCondition = LoadOperationCondition(instanceID, humpCalc.OperationConditionID);

                    hcWagon.EnergyCalculationParams = new EnergyCalculationParams
                    {
                        InstanceID = instanceID,
                        HumpSchemeID = scheme.HumpSchemeID,
                        ID = humpCalc.ID,
                        SlopeLineID = humpCalc.SlopeLineID,
                        SlopeLine = slopeLine,
                        WagonTypeName = humpCalc.WagonType,
                        Wagon = wagonConceptList?.Find(w => w.TypeName == humpCalc.WagonType),
                        OperationConditionID = humpCalc.OperationConditionID,
                        OperationCondition = operationCondition,
                        RetarderStatusList = humpCalc.RetarderStatusList
                    };
                }

                var rtData = HeadwayChecker.CalculateRunningTime(scheme, flatLayout, slopeLayout);

                LogInformationWithContext("HeadwayCheck with ID {SchemeID} has been executed.", headwayCheckSchemeID);
                return Ok(rtData);
            }
            catch (Exception ex)
            {
                LogErrorWithContext(ex, "Error executing HeadwayCheck with ID {SchemeID}.", headwayCheckSchemeID);
                return StatusCode(500, "Internal server error while executing HeadwayCheck.");
            }
        }
    }
}

