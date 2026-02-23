using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MySqlX.XDevAPI;
using SwitchYard.Hump;
using SwitchYard.Service.Services;
using SwitchYard.Service.Utils;
using System.Data.Common;
using System.Net;
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

        public HumpController(ILogger<HumpController> logger, IConfiguration configuration, SnowflakeIdGenerator snowflakeIdGenerator, InstanceAuthorizationService authService)
        {
            _logger = logger;
            _config = configuration;
            _snowflakeIdGenerator = snowflakeIdGenerator;
            _authService = authService;
        }

        /// <summary>
        /// 验证实例所有权并返回相应的ActionResult
        /// </summary>
        private IActionResult? ValidateInstanceOwnershipOrFail(string instanceID)
        {
            var username = User.Identity?.Name;
            var result = _authService.ValidateInstanceOwnership(instanceID, username);

            if (!result.IsAuthorized)
            {
                if (result.IsNotFound)
                {
                    _logger.LogWarning(result.ErrorMessage);
                    return NotFound(result.ErrorMessage);
                }
                if (result.IsError)
                {
                    _logger.LogWarning(500, result.ErrorMessage);
                    return StatusCode(500, result.ErrorMessage);
                }
                _logger.LogWarning(result.ErrorMessage);
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

                DBConnector dbConnector = DBConnector.GetDBConnector();
                var instanceList = dbConnector.Query<HumpInstance>("SELECT * FROM humpinstance WHERE Owner = @username", new { username });
                _logger.LogInformation("Retrieved {InstanceCount} HumpInstances for user {Username}.", instanceList?.Count ?? 0, username);
                return Ok(instanceList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting HumpInstance.");
                return StatusCode(500, "Internal server error while getting HumpInstance.");
            }
        }

        [HttpPost(Name = "CreateInstance")]
        public IActionResult CreateInstance(HumpInstance instance)
        {
            try
            {
                var username = User.Identity.Name;
                instance.Owner = username;
                instance.CreatedDate = DateTime.Now;
                instance.IsActive = 1;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                instance.ID = _snowflakeIdGenerator.NextIdString();
                var result = dbConnector.ExecuteNonQuery("INSERT INTO humpinstance (ID, Name, Owner, CreatedDate, IsActive) VALUES (@ID, @Name, @Owner, @CreatedDate, @IsActive)",
                    instance);
                if (result > 0)
                {
                    _logger.LogInformation("Created HumpInstance with ID {InstanceID} for user {Username}.", instance.ID, username);
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
                _logger.LogError(ex, "Error creating HumpInstance.");
                return StatusCode(500, "Internal server error while creating HumpInstance.");
            }
        }

        [HttpPut(Name = "EditInstance")]
        public IActionResult EditInstance(HumpInstance instance)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(instance.ID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var result = dbConnector.ExecuteNonQuery("UPDATE humpinstance SET Name = @Name, IsActive = @IsActive WHERE ID = @ID AND Owner = @Owner",
                    new { instance.Name, instance.IsActive, instance.ID, Owner = username });
                if (result > 0)
                {
                    _logger.LogInformation("Updated HumpInstance with ID {InstanceID} for user {Username}.", instance.ID, username);
                    return Ok("Instance updated successfully.");
                }
                else
                {
                    _logger.LogWarning("Failed to update HumpInstance for user {Username}.", username);
                    return StatusCode(500, "Failed to update instance.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating HumpInstance.");
                return StatusCode(500, "Internal server error while updating HumpInstance.");
            }
        }

        [HttpDelete(Name = "DeleteInstance")]
        public IActionResult DeleteInstance(string id)
        {
            try
            {
                var authResult = ValidateInstanceOwnershipOrFail(id);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var result = dbConnector.ExecuteNonQuery("DELETE FROM humpinstance WHERE ID = @id AND Owner = @username", new { id, username });
                if (result > 0)
                {
                    _logger.LogInformation("Deleted HumpInstance with ID {InstanceID} for user {Username}.", id, username);
                    return Ok("Instance deleted successfully.");
                }
                else
                {
                    _logger.LogWarning("Failed to delete HumpInstance for user {Username}.", username);
                    return StatusCode(500, "Failed to delete instance.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting HumpInstance.");
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
                _logger.LogInformation("Retrieved {SlopeLineCount} SlopeLines for instance {InstanceID} by user {Username}.", slopeLines?.Count ?? 0, instanceID, username);
                return Ok(slopeLines);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SlopeLines for instance {InstanceID}.", instanceID);
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
                    _logger.LogInformation("Created SlopeLine with ID {SlopeLineID} for instance {InstanceID} by user {Username}.", slopeLine.ID, slopeLine.InstanceID, username);
                    return Ok(slopeLine);
                }
                else
                {
                    _logger.LogWarning("Failed to create SlopeLine for instance {InstanceID} by user {Username}.", slopeLine.InstanceID, username);
                    return StatusCode(500, "Failed to create slope line.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating SlopeLine.");
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
                    _logger.LogInformation("Updated SlopeLine with ID {SlopeLineID} for instance {InstanceID} by user {Username}.", slopeLine.ID, existing.InstanceID, username);
                    return Ok("SlopeLine updated successfully.");
                }
                else
                {
                    _logger.LogWarning("Failed to update SlopeLine for instance {InstanceID} by user {Username}.", existing.InstanceID, username);
                    return StatusCode(500, "Failed to update slope line.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating SlopeLine.");
                return StatusCode(500, "Internal server error while updating SlopeLine.");
            }
        }

        [HttpDelete(Name = "DeleteSlopeLine")]
        public IActionResult DeleteSlopeLine(string id)
        {
            try
            {
                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var slopeLine = dbConnector.Query<SlopeLine>("SELECT * FROM slopeline WHERE ID = @id", new { id }).FirstOrDefault();
                if (slopeLine == null)
                {
                    return NotFound("SlopeLine not found.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(slopeLine.InstanceID);
                if (authResult != null) return authResult;

                var result = dbConnector.ExecuteNonQuery("DELETE FROM slopeline WHERE ID = @id", new { id });
                if (result > 0)
                {
                    _logger.LogInformation("Deleted SlopeLine with ID {SlopeLineID} for instance {InstanceID} by user {Username}.", id, slopeLine.InstanceID, username);
                    return Ok("SlopeLine deleted successfully.");
                }
                else
                {
                    _logger.LogWarning("Failed to delete SlopeLine for instance {InstanceID} by user {Username}.", slopeLine.InstanceID, username);
                    return StatusCode(500, "Failed to delete slope line.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting SlopeLine.");
                return StatusCode(500, "Internal server error while deleting SlopeLine.");
            }
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
                _logger.LogError(ex, "Error retrieving FlatLayout.");
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

                _logger.LogInformation("FlatLayout retrieved with {PositionCount} positions, {SegmentCount} segments, {SwitchCount} switches, and {RetarderCount} retarders.",
                    flatLayout.PositionList?.Count ?? 0,
                    flatLayout.PositionSegmentList?.Count ?? 0,
                    flatLayout.SwitchList?.Count ?? 0,
                    flatLayout.RetarderList?.Count ?? 0);

                return flatLayout;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting FlatLayout.");
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
                _logger.LogInformation("Updated FlatLayout for instance {InstanceID}, slope line {SlopeLineID} by user {Username}.", flatLayout.InstanceID, flatLayout.SlopeLineID, username);
                return Ok("FlatLayout updated successfully.");
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                _logger.LogError(ex, "Error updating FlatLayout.");
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
                _logger.LogInformation("Deleted FlatLayout for instance {InstanceID}, slope line {SlopeLineID} by user {Username}.", flatLayout.InstanceID, flatLayout.SlopeLineID, username);
                return Ok("FlatLayout deleted successfully.");
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                _logger.LogError(ex, "Error deleting FlatLayout.");
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
                _logger.LogInformation("WagonConcept retrieved with {WagonConceptCount} entries.", wagonConceptList?.Count ?? 0);
                return Ok(wagonConceptList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving WagonConcept.");
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
                if (wagonConcept == null || string.IsNullOrEmpty(wagonConcept.InstanceID))
                {
                    return BadRequest("Invalid WagonConcept or missing InstanceID.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(wagonConcept.InstanceID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();

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
                    _logger.LogInformation("Created WagonConcept {TypeName} for instance {InstanceID} by user {Username}.", wagonConcept.TypeName, wagonConcept.InstanceID, username);
                    return Ok(wagonConcept);
                }
                else
                {
                    _logger.LogWarning("Failed to create WagonConcept for instance {InstanceID} by user {Username}.", wagonConcept.InstanceID, username);
                    return StatusCode(500, "Failed to create WagonConcept.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating WagonConcept.");
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
                if (wagonConcept == null || string.IsNullOrEmpty(wagonConcept.TypeName))
                {
                    return BadRequest("Invalid WagonConcept or missing TypeName.");
                }

                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var existing = dbConnector.Query<WagonConcept>("SELECT * FROM wagonconcept WHERE TypeName = @typeName", new { typeName = wagonConcept.TypeName }).FirstOrDefault();
                if (existing == null)
                {
                    return NotFound("WagonConcept not found.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(existing.InstanceID);
                if (authResult != null) return authResult;

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
                    _logger.LogInformation("Updated WagonConcept {TypeName} for instance {InstanceID} by user {Username}.", wagonConcept.TypeName, existing.InstanceID, username);
                    return Ok("WagonConcept updated successfully.");
                }
                else
                {
                    _logger.LogWarning("Failed to update WagonConcept {TypeName} for instance {InstanceID} by user {Username}.", wagonConcept.TypeName, existing.InstanceID, username);
                    return StatusCode(500, "Failed to update WagonConcept.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating WagonConcept.");
                return StatusCode(500, "Internal server error while updating WagonConcept.");
            }
        }

        /// <summary>
        /// 删除车辆概念
        /// </summary>
        [HttpDelete(Name = "DeleteWagonConcept")]
        public IActionResult DeleteWagonConcept(string typeName)
        {
            try
            {
                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var existing = dbConnector.Query<WagonConcept>("SELECT * FROM wagonconcept WHERE TypeName = @typeName", new { typeName }).FirstOrDefault();
                if (existing == null)
                {
                    return NotFound("WagonConcept not found.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(existing.InstanceID);
                if (authResult != null) return authResult;

                var result = dbConnector.ExecuteNonQuery("DELETE FROM wagonconcept WHERE TypeName = @typeName", new { typeName });
                if (result > 0)
                {
                    _logger.LogInformation("Deleted WagonConcept {TypeName} for instance {InstanceID} by user {Username}.", typeName, existing.InstanceID, username);
                    return Ok("WagonConcept deleted successfully.");
                }
                else
                {
                    _logger.LogWarning("Failed to delete WagonConcept {TypeName} for instance {InstanceID} by user {Username}.", typeName, existing.InstanceID, username);
                    return StatusCode(500, "Failed to delete WagonConcept.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting WagonConcept.");
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
                _logger.LogInformation("Retrieved {Count} OperationConditions for instance {InstanceID}.", list?.Count ?? 0, instanceID);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting OperationConditions for instance {InstanceID}.", instanceID);
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
                    _logger.LogInformation("Created OperationCondition {ID} for instance {InstanceID}.", condition.ID, condition.InstanceID);
                    return Ok(condition);
                }
                else
                {
                    _logger.LogWarning("Failed to create OperationCondition for instance {InstanceID}.", condition.InstanceID);
                    return StatusCode(500, "Failed to create OperationCondition.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating OperationCondition.");
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
                    _logger.LogInformation("Updated OperationCondition {ID} for instance {InstanceID}.", condition.ID, existing.InstanceID);
                    return Ok("OperationCondition updated successfully.");
                }
                else
                {
                    _logger.LogWarning("Failed to update OperationCondition {ID} for instance {InstanceID}.", condition.ID, existing.InstanceID);
                    return StatusCode(500, "Failed to update OperationCondition.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating OperationCondition.");
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
                    _logger.LogInformation("Deleted OperationCondition {ID} for instance {InstanceID}.", id, existing.InstanceID);
                    return Ok("OperationCondition deleted successfully.");
                }
                else
                {
                    _logger.LogWarning("Failed to delete OperationCondition {ID} for instance {InstanceID}.", id, existing.InstanceID);
                    return StatusCode(500, "Failed to delete OperationCondition.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting OperationCondition.");
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

                _logger.LogInformation("SlopeLayout retrieved with {PositionCount} positions and {SegmentCount} segments.",
                    slopeLayout.PositionList?.Count ?? 0,
                    slopeLayout.PositionSegmentList?.Count ?? 0);
                return Ok(slopeLayout);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving SlopeLayout.");
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
                _logger.LogInformation("Created SlopeLayout for instance {InstanceID}, hump scheme {HumpSchemeID} by user {Username}.", instanceID, humpSchemeID, username);
                return Ok(slopeLayout);
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                _logger.LogError(ex, "Error creating SlopeLayout.");
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
                _logger.LogInformation("Updated SlopeLayout for instance {InstanceID}, hump scheme {HumpSchemeID} by user {Username}.", instanceID, humpSchemeID, username);
                return Ok("SlopeLayout updated successfully.");
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                _logger.LogError(ex, "Error updating SlopeLayout.");
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
                _logger.LogInformation("Deleted SlopeLayout for instance {InstanceID}, hump scheme {HumpSchemeID} by user {Username}.", instanceID, humpSchemeID, username);
                return Ok("SlopeLayout deleted successfully.");
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                _logger.LogError(ex, "Error deleting SlopeLayout.");
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
                StringBuilder sqlStrBuilder = new StringBuilder();
                foreach (var data in humpCalculation.Data)
                {
                    sqlStrBuilder.Append($"INSERT INTO humpcalculationdata (InstanceID, HumpSchemeID, HumpCalculationID, X, GravityEnergyHeight, ResistanceEnergyHeight, KineticEnergyHeight, BreakingEnergyHeight, InitTotalEnergyHeight) VALUES ('{data.InstanceID}', '{data.HumpSchemeID}', '{data.HumpCalculationID}', {data.X}, {data.GravityEnergyHeight}, {data.ResistanceEnergyHeight}, {data.KineticEnergyHeight}, {data.BreakingEnergyHeight}, {data.InitTotalEnergyHeight});");
                }

                if(sqlStrBuilder.Length == 0)
                {
                    // 返回空
                    _logger.LogInformation("No HumpCalculationData to insert for instance {InstanceID} and hump scheme {HumpSchemeID}.", parameters.InstanceID, parameters.HumpSchemeID);
                    return NoContent();
                }

                dbConnector.BeginTransaction();
                dbConnector.ExecuteNonQuery($"DELETE FROM humpcalculationdata WHERE InstanceID = '{parameters.InstanceID}' AND HumpSchemeID = '{parameters.HumpSchemeID}' AND HumpCalculationID = '{parameters.ID}';");
                dbConnector.ExecuteNonQuery(sqlStrBuilder.ToString());
                dbConnector.Commit();
                _logger.LogInformation("Inserted {DataCount} HumpCalculationData records for instance {InstanceID} and hump scheme {HumpSchemeID}.", humpCalculation.Data?.Count ?? 0, parameters.InstanceID, parameters.HumpSchemeID);

                // 返回计算结果
                _logger.LogInformation("Energy height calculation executed for instance {InstanceID} with parameters: {Parameters}.", parameters.InstanceID, parameters);
                return Ok(humpCalculation);
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                _logger.LogError(ex, "Error calculating resistance.");
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
                _logger.LogInformation("Kinetic Energy Height calculated for {PositionCount} positions.", kineticEnergyHeightList?.Count ?? 0);
                return Ok(kineticEnergyHeightList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating Kinetic Energy Height.");
                return StatusCode(500, "Internal server error while calculating Kinetic Energy Height.");
            }
        }

        private SlopeLine LoadSlopeLine(string instanceID, string id)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            var slopeLine = dbConnector.Query<SlopeLine>("SELECT * FROM slopeline WHERE InstanceID = @instanceID AND ID = @id", new { instanceID, id }).FirstOrDefault();
            return slopeLine;
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
                    for (var i = slopeLayout.PositionList.First().X; i <= slopeLayout.PositionList.Last().X; i += 20)
                    {
                        var energyHeight = HumpEnergyHeightCalculator.CalculateResistanceEnergyHeight(flatLayout, i, parameters);
                        resistanceEnergyHeightList.Add(new { x = i, height = Math.Round(energyHeight, 3) });
                    }
                }
                _logger.LogInformation("Resistance Energy Height calculated for {PositionCount} positions.", resistanceEnergyHeightList?.Count ?? 0);
                return Ok(resistanceEnergyHeightList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating Resistance Energy Height.");
                return StatusCode(500, "Internal server error while calculating Resistance Energy Height.");
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

                parameters.Wagon = wagonConceptList.Find(w => w.TypeName == parameters.Wagon.TypeName);

                var breakingEnergyHeightDict = new Dictionary<double, double>();
                foreach (var p in slopeLayout.PositionList)
                {
                    var energyHeight = HumpEnergyHeightCalculator.CalculateBreakingEnergyHeight(flatLayout, p.X, parameters);
                    breakingEnergyHeightDict.Add(p.X, energyHeight);
                }
                _logger.LogInformation("Breaking Energy Height calculated for {PositionCount} positions.", breakingEnergyHeightDict?.Count ?? 0);
                return Ok(breakingEnergyHeightDict);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating Breaking Energy Height.");
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
                _logger.LogInformation("Velocity calculated for {PositionCount} positions.", velocityList?.Count ?? 0);
                return Ok(velocityList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating Velocity.");
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

                _logger.LogInformation("Time calculated for {PositionCount} positions.", velocityList?.Count ?? 0);
                return Ok(timeList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating Time.");
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
                _logger.LogInformation("Retrieved {HumpSchemeCount} HumpSchemes for instance {InstanceID} by user {Username}.", humpSchemes?.Count ?? 0, instanceID, username);
                return Ok(humpSchemes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting HumpSchemes for instance {InstanceID}.", instanceID);
                return StatusCode(500, "Internal server error while getting HumpSchemes.");
            }
        }

        /// <summary>
        /// 创建驼峰方案
        /// </summary>
        [HttpPost(Name = "CreateHumpScheme")]
        public IActionResult CreateHumpScheme(HumpScheme humpScheme)
        {
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
                DBConnector dbConnector = DBConnector.GetDBConnector();
                humpScheme.ID = _snowflakeIdGenerator.NextIdString();
                var result = dbConnector.ExecuteNonQuery("INSERT INTO humpscheme (InstanceID, ID, Name) VALUES (@InstanceID, @ID, @Name)",
                    new { humpScheme.InstanceID, humpScheme.ID, humpScheme.Name });
                if (result > 0)
                {
                    _logger.LogInformation("Created HumpScheme with ID {HumpSchemeID} for instance {InstanceID} by user {Username}.", humpScheme.ID, humpScheme.InstanceID, username);
                    return Ok(humpScheme);
                }
                else
                {
                    _logger.LogWarning("Failed to create HumpScheme for instance {InstanceID} by user {Username}.", humpScheme.InstanceID, username);
                    return StatusCode(500, "Failed to create HumpScheme.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating HumpScheme.");
                return StatusCode(500, "Internal server error while creating HumpScheme.");
            }
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
                    _logger.LogInformation("Updated HumpScheme with ID {HumpSchemeID} for instance {InstanceID} by user {Username}.", humpScheme.ID, existing.InstanceID, username);
                    return Ok("HumpScheme updated successfully.");
                }
                else
                {
                    _logger.LogWarning("Failed to update HumpScheme for instance {InstanceID} by user {Username}.", existing.InstanceID, username);
                    return StatusCode(500, "Failed to update HumpScheme.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating HumpScheme.");
                return StatusCode(500, "Internal server error while updating HumpScheme.");
            }
        }

        /// <summary>
        /// 删除驼峰方案
        /// </summary>
        [HttpDelete(Name = "DeleteHumpScheme")]
        public IActionResult DeleteHumpScheme(string id)
        {
            try
            {
                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var humpScheme = dbConnector.Query<HumpScheme>("SELECT * FROM humpscheme WHERE ID = @id", new { id }).FirstOrDefault();
                if (humpScheme == null)
                {
                    _logger.LogWarning("HumpScheme {HumpSchemeID} not found.", id);
                    return NotFound("HumpScheme not found.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(humpScheme.InstanceID);
                if (authResult != null) return authResult;

                var result = dbConnector.ExecuteNonQuery("DELETE FROM humpscheme WHERE ID = @id", new { id });
                if (result > 0)
                {
                    _logger.LogInformation("Deleted HumpScheme with ID {HumpSchemeID} for instance {InstanceID} by user {Username}.", id, humpScheme.InstanceID, username);
                    return Ok("HumpScheme deleted successfully.");
                }
                else
                {
                    _logger.LogWarning("Failed to delete HumpScheme for instance {InstanceID} by user {Username}.", humpScheme.InstanceID, username);
                    return StatusCode(500, "Failed to delete HumpScheme.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting HumpScheme.");
                return StatusCode(500, "Internal server error while deleting HumpScheme.");
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
                var humpCalculations = dbConnector.Query<HumpCalculation>("SELECT * FROM humpcalculation WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID", new { instanceID = instanceID, humpSchemeID = humpSchemeID });
                _logger.LogInformation("Retrieved {HumpCalculationCount} HumpCalculations for instance {InstanceID} by user {Username}.", humpCalculations?.Count ?? 0, instanceID, username);
                return Ok(humpCalculations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting HumpCalculations for instance {InstanceID}.", instanceID);
                return StatusCode(500, "Internal server error while getting HumpCalculations.");
            }
        }

        /// <summary>
        /// 创建驼峰计算
        /// </summary>
        [HttpPost(Name = "CreateHumpCalculation")]
        public IActionResult CreateHumpCalculation(HumpCalculation humpCalculation)
        {
            try
            {
                if (humpCalculation == null || string.IsNullOrEmpty(humpCalculation.InstanceID))
                {
                    return BadRequest("Invalid HumpCalculation or missing InstanceID.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(humpCalculation.InstanceID);
                if (authResult != null) return authResult;

                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                humpCalculation.ID = _snowflakeIdGenerator.NextIdString();
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
                if (result > 0)
                {
                    _logger.LogInformation("Created HumpCalculation with ID {HumpCalculationID} for instance {InstanceID} by user {Username}.", humpCalculation.ID, humpCalculation.InstanceID, username);
                    return Ok(humpCalculation);
                }
                else
                {
                    _logger.LogWarning("Failed to create HumpCalculation for instance {InstanceID} by user {Username}.", humpCalculation.InstanceID, username);
                    return StatusCode(500, "Failed to create HumpCalculation.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating HumpCalculation.");
                return StatusCode(500, "Internal server error while creating HumpCalculation.");
            }
        }

        /// <summary>
        /// 更新驼峰计算
        /// </summary>
        [HttpPut(Name = "EditHumpCalculation")]
        public IActionResult EditHumpCalculation(HumpCalculation humpCalculation)
        {
            try
            {
                if (humpCalculation == null || string.IsNullOrEmpty(humpCalculation.ID))
                {
                    return BadRequest("Invalid HumpCalculation or missing ID.");
                }

                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var existing = dbConnector.Query<HumpCalculation>("SELECT * FROM humpcalculation WHERE ID = @id", new { id = humpCalculation.ID }).FirstOrDefault();
                if (existing == null)
                {
                    return NotFound("HumpCalculation not found.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(existing.InstanceID);
                if (authResult != null) return authResult;

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
                if (result > 0)
                {
                    _logger.LogInformation("Updated HumpCalculation with ID {HumpCalculationID} for instance {InstanceID} by user {Username}.", humpCalculation.ID, existing.InstanceID, username);
                    return Ok("HumpCalculation updated successfully.");
                }
                else
                {
                    _logger.LogWarning("Failed to update HumpCalculation for instance {InstanceID} by user {Username}.", existing.InstanceID, username);
                    return StatusCode(500, "Failed to update HumpCalculation.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating HumpCalculation.");
                return StatusCode(500, "Internal server error while updating HumpCalculation.");
            }
        }

        /// <summary>
        /// 删除驼峰计算
        /// </summary>
        [HttpDelete(Name = "DeleteHumpCalculation")]
        public IActionResult DeleteHumpCalculation(string instanceID, string humpSchemeID, string id)
        {
            try
            {
                var username = User.Identity?.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var humpCalculation = dbConnector.Query<HumpCalculation>("SELECT * FROM humpcalculation WHERE InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID AND ID = @id", new { instanceID = instanceID, humpSchemeID = humpSchemeID, id = id }).FirstOrDefault();
                if (humpCalculation == null)
                {
                    return NotFound("HumpCalculation not found.");
                }

                var authResult = ValidateInstanceOwnershipOrFail(humpCalculation.InstanceID);
                if (authResult != null) return authResult;

                var result = dbConnector.ExecuteNonQuery("DELETE FROM humpcalculation WHERE ID = @id", new { id });
                if (result > 0)
                {
                    _logger.LogInformation("Deleted HumpCalculation with ID {HumpCalculationID} for instance {InstanceID} by user {Username}.", id, humpCalculation.InstanceID, username);
                    return Ok("HumpCalculation deleted successfully.");
                }
                else
                {
                    _logger.LogWarning("Failed to delete HumpCalculation for instance {InstanceID} by user {Username}.", humpCalculation.InstanceID, username);
                    return StatusCode(500, "Failed to delete HumpCalculation.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting HumpCalculation.");
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
                _logger.LogInformation("Retrieved HumpCalculation with ID {HumpCalculationID} for instance {InstanceID}.", id, humpCalculation.InstanceID);
                return Ok(humpCalculation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting HumpCalculation with ID {HumpCalculationID}.", id);
                return StatusCode(500, "Internal server error while getting HumpCalculation.");
            }
        }

        private HumpCalculation GetHumpCalculation(string instanceID, string humpSchemeID, string id)
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            var humpCalculation = dbConnector.Query<HumpCalculation>("SELECT * FROM humpcalculation WHERE ID = @id AND InstanceID = @instanceID AND HumpSchemeID = @humpSchemeID", new { id = id, instanceID = instanceID, humpSchemeID = humpSchemeID }).FirstOrDefault();
            return humpCalculation;
        }

        /// <summary>
        /// 获取追踪间隔检算方案列表
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
                
                _logger.LogInformation("Retrieved {SchemeCount} HeadwayCheckSchemes for instance {InstanceID} by user {Username}.", schemes?.Count ?? 0, instanceID, username);
                return Ok(schemes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting HeadwayCheckSchemes for instance {InstanceID}.", instanceID);
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
                    _logger.LogWarning("Failed to create HeadwayCheckScheme for instance {InstanceID} by user {Username}.", scheme.InstanceID, username);
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
                _logger.LogInformation("Created HeadwayCheckScheme with ID {SchemeID} and {WagonCount} wagons for instance {InstanceID} by user {Username}.", scheme.ID, scheme.WagonList?.Count ?? 0, scheme.InstanceID, username);
                return Ok(scheme);
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                _logger.LogError(ex, "Error creating HeadwayCheckScheme.");
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
                    _logger.LogWarning("Failed to update HeadwayCheckScheme for instance {InstanceID} by user {Username}.", existing.InstanceID, username);
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
                _logger.LogInformation("Updated HeadwayCheckScheme with ID {SchemeID} and {WagonCount} wagons for instance {InstanceID} by user {Username}.", scheme.ID, scheme.WagonList?.Count ?? 0, existing.InstanceID, username);
                return Ok("HeadwayCheckScheme updated successfully.");
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                _logger.LogError(ex, "Error updating HeadwayCheckScheme.");
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
                
                dbConnector.ExecuteNonQuery("DELETE FROM headwaycheckwagon WHERE InstanceID = @instanceID AND HeadwayCheckID = @headwayCheckID", 
                    new { instanceID = scheme.InstanceID, headwayCheckID = id });
                
                var result = dbConnector.ExecuteNonQuery("DELETE FROM headwaycheckscheme WHERE ID = @id", new { id });
                
                if (result > 0)
                {
                    dbConnector.Commit();
                    _logger.LogInformation("Deleted HeadwayCheckScheme with ID {SchemeID} for instance {InstanceID} by user {Username}.", id, scheme.InstanceID, username);
                    return Ok("HeadwayCheckScheme deleted successfully.");
                }
                else
                {
                    dbConnector.Rollback();
                    _logger.LogWarning("Failed to delete HeadwayCheckScheme for instance {InstanceID} by user {Username}.", scheme.InstanceID, username);
                    return StatusCode(500, "Failed to delete HeadwayCheckScheme.");
                }
            }
            catch (Exception ex)
            {
                dbConnector.Rollback();
                _logger.LogError(ex, "Error deleting HeadwayCheckScheme.");
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

            foreach(var hcWagon in scheme.WagonList)
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
                
                _logger.LogInformation("Retrieved HeadwayCheckScheme with ID {SchemeID} and {WagonCount} wagons for instance {InstanceID}.", id, scheme.WagonList?.Count ?? 0, instanceID);
                return Ok(scheme);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting HeadwayCheckScheme with ID {SchemeID}.", id);
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

                SpeedProfileGenerator.SpaceStepSize = spaceStepSize;

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
                        RetarderStatus = null // TODO: 如果需要减速器状态，需要从HumpCalculation中获取RetarderStatusID并加载
                    };

                    var speedProfile = SpeedProfileGenerator.Generate(hcWagon, flatLayout, slopeLayout);

                    speedProfileList.Add(speedProfile);
                }

                _logger.LogInformation("Calculated speed profile for HeadwayCheckScheme with ID {SchemeID} for instance {InstanceID}.", headwayCheckSchemeID, instanceID);
                return Ok(speedProfileList);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error calculating speed profile for instance {InstanceID} with HeadwayCheckScheme ID {SchemeID}.", instanceID, headwayCheckSchemeID);
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
                        RetarderStatus = null // TODO: 如果需要减速器状态，需要从HumpCalculation中获取RetarderStatusID并加载
                    };
                }

                var rtData = HeadwayChecker.CalculateRunningTime(scheme, flatLayout, slopeLayout);

                _logger.LogInformation("HeadwayCheck with ID {SchemeID} for instance {InstanceID} has been excuted.", headwayCheckSchemeID, instanceID);
                return Ok(rtData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing HeadwayCheck for instance {InstanceID} with ID {SchemeID}.", instanceID, instanceID);
                return StatusCode(500, "Internal server error while executing HeadwayCheck.");
            }
        }
    }
}
