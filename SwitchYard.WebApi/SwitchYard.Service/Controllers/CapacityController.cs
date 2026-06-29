using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwitchYard.Capacity;
using SwitchYard.Service.Models;
using SwitchYard.Service.Services;
using SwitchYard.Service.Utils;

namespace SwitchYard.Service.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    [Authorize]
    public class CapacityController : ControllerBase
    {
        private readonly ILogger<CapacityController> _logger;
        private readonly SnowflakeIdGenerator _snowflakeIdGenerator;
        private readonly UserService _userService;

        public CapacityController(
            ILogger<CapacityController> logger,
            SnowflakeIdGenerator snowflakeIdGenerator,
            UserService userService)
        {
            _logger = logger;
            _snowflakeIdGenerator = snowflakeIdGenerator;
            _userService = userService;
        }

        [HttpGet]
        public IActionResult GetInstances()
        {
            try
            {
                var username = User.Identity?.Name;
                var isAdmin = IsCurrentUserAdmin();
                if (!isAdmin && string.IsNullOrWhiteSpace(username))
                {
                    return Unauthorized("Invalid user context.");
                }

                var dbConnector = GetCapacityDbConnector();
                var instances = isAdmin
                    ? dbConnector.Query<CapacityInstance>("SELECT * FROM capacityinstance ORDER BY CreatedDate DESC, ID DESC")
                    : dbConnector.Query<CapacityInstance>(
                        "SELECT * FROM capacityinstance WHERE Owner = @username ORDER BY CreatedDate DESC, ID DESC",
                        new { username });

                _logger.LogInformation("Retrieved {InstanceCount} CapacityInstances.", instances?.Count ?? 0);
                return Ok(instances ?? new List<CapacityInstance>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting CapacityInstances.");
                return StatusCode(500, "Internal server error while getting CapacityInstances.");
            }
        }

        [HttpGet]
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

                var dbConnector = GetCapacityDbConnector();
                var whereSql = isAdmin ? string.Empty : "WHERE Owner = @username";
                object? baseParameters = isAdmin ? null : new { username };

                var totalCount = (dbConnector.Query<int>(
                    $"SELECT COUNT(1) FROM capacityinstance {whereSql}",
                    baseParameters) ?? new List<int> { 0 }).FirstOrDefault();

                var items = dbConnector.Query<CapacityInstance>(
                    $@"SELECT * FROM capacityinstance
                       {whereSql}
                       ORDER BY CreatedDate DESC, ID DESC
                       LIMIT @pageSize OFFSET @offset",
                    isAdmin
                        ? new { pageSize = query.PageSize, offset = query.Offset }
                        : new { username, pageSize = query.PageSize, offset = query.Offset }) ?? new List<CapacityInstance>();

                var result = new PagedResult<CapacityInstance>
                {
                    Items = items,
                    PageNumber = query.PageNumber,
                    PageSize = query.PageSize,
                    TotalCount = totalCount
                };

                _logger.LogInformation(
                    "Retrieved paged CapacityInstances. Page {PageNumber}, PageSize {PageSize}, TotalCount {TotalCount}.",
                    query.PageNumber,
                    query.PageSize,
                    totalCount);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paged CapacityInstances.");
                return StatusCode(500, "Internal server error while getting paged CapacityInstances.");
            }
        }

        [HttpPost]
        public IActionResult CreateInstance([FromBody] CreateCapacityInstanceRequest request)
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

                var targetOwner = username;
                if (IsCurrentUserAdmin())
                {
                    if (!TryNormalizeOwnerForPersistence(request.Owner, out var normalizedOwner, out var ownerError))
                    {
                        return BadRequest(ownerError);
                    }

                    targetOwner = normalizedOwner;
                }

                var dbConnector = GetCapacityDbConnector();
                var instance = new CapacityInstance
                {
                    ID = GenerateUniqueInstanceId(dbConnector),
                    Name = request.Name.Trim(),
                    Owner = targetOwner,
                    CreatedDate = DateTime.Now,
                    IsActive = NormalizeIsActive(request.IsActive)
                };

                var result = dbConnector.ExecuteNonQuery(
                    "INSERT INTO capacityinstance (ID, Name, Owner, CreatedDate, IsActive) VALUES (@ID, @Name, @Owner, @CreatedDate, @IsActive)",
                    instance);

                if (result > 0)
                {
                    _logger.LogInformation("Created CapacityInstance with ID {InstanceID}.", instance.ID);
                    return Ok(instance);
                }

                _logger.LogWarning("Failed to create CapacityInstance for user {Username}.", username);
                return StatusCode(500, "Failed to create instance.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating CapacityInstance.");
                return StatusCode(500, "Internal server error while creating CapacityInstance.");
            }
        }

        [HttpPost]
        public IActionResult CopyCapacityInstance([FromBody] CopyCapacityInstanceRequest request)
        {
            try
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

                var authResult = ValidateCapacityInstanceOwnershipOrFail(sourceInstanceID);
                if (authResult != null)
                {
                    return authResult;
                }

                var username = User.Identity?.Name;
                if (string.IsNullOrWhiteSpace(username))
                {
                    return Unauthorized("Invalid user context.");
                }

                var dbConnector = GetCapacityDbConnector();
                var sourceInstance = GetCapacityInstanceById(dbConnector, sourceInstanceID);
                if (sourceInstance == null)
                {
                    return NotFound("Source instance not found.");
                }

                var targetOwner = username;
                if (IsCurrentUserAdmin())
                {
                    var ownerInput = string.IsNullOrWhiteSpace(request.Owner) ? sourceInstance.Owner : request.Owner;
                    if (!TryNormalizeOwnerForPersistence(ownerInput, out var normalizedOwner, out var ownerError))
                    {
                        return BadRequest(ownerError);
                    }

                    targetOwner = normalizedOwner;
                }

                var copiedInstance = new CapacityInstance
                {
                    ID = GenerateUniqueInstanceId(dbConnector),
                    Name = newInstanceName,
                    Owner = targetOwner,
                    CreatedDate = DateTime.Now,
                    IsActive = NormalizeIsActive(sourceInstance.IsActive)
                };

                var result = dbConnector.ExecuteNonQuery(
                    "INSERT INTO capacityinstance (ID, Name, Owner, CreatedDate, IsActive) VALUES (@ID, @Name, @Owner, @CreatedDate, @IsActive)",
                    copiedInstance);

                if (result > 0)
                {
                    _logger.LogInformation(
                        "Copied CapacityInstance from {SourceInstanceID} to {TargetInstanceID}.",
                        sourceInstanceID,
                        copiedInstance.ID);
                    return Ok(copiedInstance);
                }

                _logger.LogWarning("Failed to copy CapacityInstance from {SourceInstanceID}.", sourceInstanceID);
                return StatusCode(500, "Failed to copy instance.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying CapacityInstance.");
                return StatusCode(500, "Internal server error while copying CapacityInstance.");
            }
        }

        [HttpPut]
        public IActionResult EditInstance([FromBody] CapacityInstance instance)
        {
            try
            {
                if (instance == null || string.IsNullOrWhiteSpace(instance.ID) || string.IsNullOrWhiteSpace(instance.Name))
                {
                    return BadRequest("Invalid instance payload or missing ID/name.");
                }

                var authResult = ValidateCapacityInstanceOwnershipOrFail(instance.ID);
                if (authResult != null)
                {
                    return authResult;
                }

                var username = User.Identity?.Name;
                var isAdmin = IsCurrentUserAdmin();
                if (!isAdmin && string.IsNullOrWhiteSpace(username))
                {
                    return Unauthorized("Invalid user context.");
                }

                var dbConnector = GetCapacityDbConnector();
                var trimmedName = instance.Name.Trim();
                var normalizedIsActive = NormalizeIsActive(instance.IsActive);

                int result;
                if (isAdmin)
                {
                    if (!TryNormalizeOwnerForPersistence(instance.Owner, out var normalizedOwner, out var ownerError))
                    {
                        return BadRequest(ownerError);
                    }

                    result = dbConnector.ExecuteNonQuery(
                        "UPDATE capacityinstance SET Name = @Name, Owner = @Owner, IsActive = @IsActive WHERE ID = @ID",
                        new { Name = trimmedName, Owner = normalizedOwner, IsActive = normalizedIsActive, ID = instance.ID });
                }
                else
                {
                    result = dbConnector.ExecuteNonQuery(
                        "UPDATE capacityinstance SET Name = @Name, IsActive = @IsActive WHERE ID = @ID AND Owner = @Owner",
                        new { Name = trimmedName, IsActive = normalizedIsActive, ID = instance.ID, Owner = username });
                }

                if (result > 0)
                {
                    _logger.LogInformation("Updated CapacityInstance with ID {InstanceID}.", instance.ID);
                    return Ok("Instance updated successfully.");
                }

                _logger.LogWarning("Failed to update CapacityInstance with ID {InstanceID}.", instance.ID);
                return StatusCode(500, "Failed to update instance.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating CapacityInstance.");
                return StatusCode(500, "Internal server error while updating CapacityInstance.");
            }
        }

        [HttpDelete]
        public IActionResult DeleteInstance(string id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return BadRequest("Instance ID is required.");
                }

                var authResult = ValidateCapacityInstanceOwnershipOrFail(id);
                if (authResult != null)
                {
                    return authResult;
                }

                var username = User.Identity?.Name;
                var isAdmin = IsCurrentUserAdmin();
                var dbConnector = GetCapacityDbConnector();
                var result = isAdmin
                    ? dbConnector.ExecuteNonQuery("DELETE FROM capacityinstance WHERE ID = @id", new { id })
                    : dbConnector.ExecuteNonQuery(
                        "DELETE FROM capacityinstance WHERE ID = @id AND Owner = @username",
                        new { id, username });

                if (result > 0)
                {
                    _logger.LogInformation("Deleted CapacityInstance with ID {InstanceID}.", id);
                    return Ok("Instance deleted successfully.");
                }

                _logger.LogWarning("Failed to delete CapacityInstance with ID {InstanceID}.", id);
                return StatusCode(500, "Failed to delete instance.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting CapacityInstance.");
                return StatusCode(500, "Internal server error while deleting CapacityInstance.");
            }
        }

        public sealed class CreateCapacityInstanceRequest
        {
            public string Name { get; set; } = string.Empty;

            public string? Owner { get; set; }

            public int IsActive { get; set; } = 1;
        }

        public sealed class CopyCapacityInstanceRequest
        {
            public string SourceInstanceID { get; set; } = string.Empty;

            public string NewInstanceName { get; set; } = string.Empty;

            public string? Owner { get; set; }
        }

        private DBConnector GetCapacityDbConnector()
        {
            return DBConnector.GetDBConnector(DBConnector.CapacityDatabaseSectionName);
        }

        private CapacityInstance? GetCapacityInstanceById(DBConnector dbConnector, string instanceID)
        {
            return (dbConnector.Query<CapacityInstance>(
                "SELECT * FROM capacityinstance WHERE ID = @instanceID",
                new { instanceID }) ?? new List<CapacityInstance>()).FirstOrDefault();
        }

        private IActionResult? ValidateCapacityInstanceOwnershipOrFail(string instanceID)
        {
            if (string.IsNullOrWhiteSpace(instanceID))
            {
                return BadRequest("Instance ID is required.");
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

            var dbConnector = GetCapacityDbConnector();
            var instance = GetCapacityInstanceById(dbConnector, instanceID);
            if (instance == null)
            {
                return NotFound("Instance not found.");
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

        private string GenerateUniqueInstanceId(DBConnector dbConnector)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var candidate = _snowflakeIdGenerator.NextIdString();
                var exists = (dbConnector.Query<CapacityInstance>(
                    "SELECT * FROM capacityinstance WHERE ID = @id",
                    new { id = candidate }) ?? new List<CapacityInstance>()).Any();

                if (!exists)
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Failed to generate a unique instance ID.");
        }

        private static int NormalizeIsActive(int isActive)
        {
            return isActive == 0 ? 0 : 1;
        }
    }
}
