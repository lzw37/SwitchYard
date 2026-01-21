using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SwitchYard.Hump;
using System.Data.Common;
using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using SwitchYard.Service.Utils;

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

        public HumpController(ILogger<HumpController> logger, IConfiguration configuration, SnowflakeIdGenerator snowflakeIdGenerator)
        {
            _logger = logger;
            _config = configuration;
            _snowflakeIdGenerator = snowflakeIdGenerator;
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
                var username = User.Identity.Name;
                if (instance.Owner != username)
                {
                    return Unauthorized("Cannot edit instance owned by another user.");
                }
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var result = dbConnector.ExecuteNonQuery("UPDATE humpinstance SET Name = @Name, IsActive = @IsActive WHERE ID = @ID AND Owner = @Owner",
                    new { instance.Name, instance.IsActive, instance.ID, instance.Owner });
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
                var username = User.Identity.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var instances = dbConnector.Query<HumpInstance>("SELECT * FROM humpinstance WHERE ID = @id AND Owner = @username", new { id, username });
                if (instances == null || instances.Count == 0)
                {
                    return NotFound("Instance not found or not owned by user.");
                }
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
                var username = User.Identity.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var instance = dbConnector.Query<HumpInstance>("SELECT * FROM humpinstance WHERE ID = @instanceID", new { instanceID }).FirstOrDefault();
                if (instance == null || instance.Owner != username)
                {
                    return Unauthorized("Instance not found or not owned by user.");
                }
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
                var username = User.Identity.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var instance = dbConnector.Query<HumpInstance>("SELECT * FROM humpinstance WHERE ID = @instanceID", new { instanceID = slopeLine.InstanceID }).FirstOrDefault();
                if (instance == null || instance.Owner != username)
                {
                    return Unauthorized("Instance not found or not owned by user.");
                }
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
                var username = User.Identity.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var existing = dbConnector.Query<SlopeLine>("SELECT * FROM slopeline WHERE ID = @id", new { id = slopeLine.ID }).FirstOrDefault();
                if (existing == null)
                {
                    return NotFound("SlopeLine not found.");
                }
                var instance = dbConnector.Query<HumpInstance>("SELECT * FROM humpinstance WHERE ID = @instanceID", new { instanceID = existing.InstanceID }).FirstOrDefault();
                if (instance == null || instance.Owner != username)
                {
                    return Unauthorized("Instance not found or not owned by user.");
                }
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
                var username = User.Identity.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var slopeLine = dbConnector.Query<SlopeLine>("SELECT * FROM slopeline WHERE ID = @id", new { id }).FirstOrDefault();
                if (slopeLine == null)
                {
                    return NotFound("SlopeLine not found.");
                }
                var instance = dbConnector.Query<HumpInstance>("SELECT * FROM humpinstance WHERE ID = @instanceID", new { instanceID = slopeLine.InstanceID }).FirstOrDefault();
                if (instance == null || instance.Owner != username)
                {
                    return Unauthorized("Instance not found or not owned by user.");
                }
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
                var username = User.Identity.Name;
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var instance = dbConnector.Query<HumpInstance>("SELECT * FROM humpinstance WHERE ID = @instanceID", new { instanceID }).FirstOrDefault();
                if (instance == null || instance.Owner != username)
                {
                    return Unauthorized("Instance not found or not owned by user.");
                }

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
                var username = User.Identity.Name;
                var instance = dbConnector.Query<HumpInstance>("SELECT * FROM humpinstance WHERE ID = @instanceID", new { instanceID = flatLayout.InstanceID }).FirstOrDefault();
                if (instance == null || instance.Owner != username)
                {
                    return Unauthorized("Instance not found or not owned by user.");
                }

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
                foreach (var sw in flatLayout.SwitchList)
                {
                    var id = _snowflakeIdGenerator.NextIdString();
                    dbConnector.ExecuteNonQuery("INSERT INTO switch (ID, InstanceID, SlopeLineID, BindingPositionID, BindingPositionSegmentID, CurveDegree, Type, Direction, Side) VALUES (@ID, @InstanceID, @SlopeLineID, @BindingPositionID, @BindingPositionSegmentID, @CurveDegree, @Type, @Direction, @Side)",
                        new { ID = id, InstanceID = flatLayout.InstanceID, SlopeLineID = flatLayout.SlopeLineID, BindingPositionID = sw.BindingPositionID, BindingPositionSegmentID = sw.BindingPositionSegmentID, CurveDegree = sw.CurveDegree, Type = sw.Type, Direction = sw.Direction, Side = sw.Side });
                }

                // Insert retarders
                foreach (var retarder in flatLayout.RetarderList)
                {
                    var id = _snowflakeIdGenerator.NextIdString();
                    dbConnector.ExecuteNonQuery("INSERT INTO retarder (ID, InstanceID, SlopeLineID, BindingPositionSegmentID, Numbers) VALUES (@ID, @InstanceID, @SlopeLineID, @BindingPositionSegmentID, @Numbers)",
                        new { ID = id, InstanceID = flatLayout.InstanceID, SlopeLineID = flatLayout.SlopeLineID, BindingPositionSegmentID = retarder.BindingPositionSegmentID, Numbers = retarder.Numbers });
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
                var username = User.Identity.Name;
                var instance = dbConnector.Query<HumpInstance>("SELECT * FROM humpinstance WHERE ID = @instanceID", new { instanceID = flatLayout.InstanceID }).FirstOrDefault();
                if (instance == null || instance.Owner != username)
                {
                    return Unauthorized("Instance not found or not owned by user.");
                }

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
        public IActionResult GetWagonConcept()
        {
            try
            {
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var wagonConceptList = LoadWagonConcept();
                _logger.LogInformation("WagonConcept retrieved with {WagonConceptCount} entries.", wagonConceptList?.Count ?? 0);
                return Ok(wagonConceptList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving WagonConcept.");
                return StatusCode(500, "Internal server error while retrieving WagonConcept.");
            }
        }

        private List<WagonConcept> LoadWagonConcept()
        {
            DBConnector dbConnector = DBConnector.GetDBConnector();
            var wagonConceptList = dbConnector.Query<SwitchYard.Hump.WagonConcept>("SELECT * FROM wagonconcept");
            return wagonConceptList;
        }

        /// <summary>
        /// 获取纵断面
        /// </summary>
        /// <returns></returns>
        [HttpGet(Name = "GetSlopeLayout")]
        public IActionResult GetSlopeLayout()
        {
            try
            {
                var slopeLayout = LoadSlopeLayout();

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

        private SlopeLayout LoadSlopeLayout()
        {
            var slopeLayout = new SwitchYard.Hump.SlopeLayout();
            DBConnector dbConnector = DBConnector.GetDBConnector();
            slopeLayout.PositionList = dbConnector.Query<SwitchYard.Hump.VPosition>("SELECT * FROM vposition");
            slopeLayout.PositionSegmentList = dbConnector.Query<SwitchYard.Hump.VPositionSegment>("SELECT * FROM vpositionsegment");
            foreach (var seg in slopeLayout.PositionSegmentList)
            {
                seg.StartPosition = slopeLayout.PositionList.Find(p => p.ID == seg.StartPositionID);
                seg.EndPosition = slopeLayout.PositionList.Find(p => p.ID == seg.EndPositionID);
            }
            return slopeLayout;
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
                var flatLayout = LoadFlatLayout("","");
                var slopeLayout = LoadSlopeLayout();
                var wagonConceptList = LoadWagonConcept();

                parameters.Wagon = wagonConceptList.Find(w => w.TypeName == parameters.WagonTypeName);

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

        /// <summary>
        /// 计算阻力能高
        /// </summary>
        /// <param name="parameters">能高计算参数</param>
        /// <returns></returns>
        [HttpPost(Name = "GetResistanceEnergyHeight")]
        public IActionResult GetResistanceEnergyHeight(EnergyCalculationParams parameters, double? currentX =null)
        {
            try
            {
                var flatLayout = LoadFlatLayout("","");
                var slopeLayout = LoadSlopeLayout();
                var wagonConceptList = LoadWagonConcept();

                parameters.Wagon = wagonConceptList.Find(w => w.TypeName == parameters.WagonTypeName);

                var resistanceEnergyHeightList = new List<object>();

                if (currentX != null)
                {
                    var energyHeight = HumpEnergyHeightCalculator.CalculateResistanceEnergyHeight(flatLayout, Convert.ToDouble(currentX), parameters);
                    resistanceEnergyHeightList.Add(new { x = currentX, height = Math.Round(energyHeight, 3) });
                }
                else
                {
                    //foreach (var p in slopeLayout.PositionList)
                    for (var i = slopeLayout.PositionList.First().X; i <= slopeLayout.PositionList.Last().X; i += 20)
                    {
                        //var energyHeight = HumpEnergyHeightCalculator.CalculateResistanceEnergyHeight(flatLayout, p.X, parameters);
                        //resistanceEnergyHeightList.Add(new { x = p.X, height = Math.Round(energyHeight,3) });
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
                var flatLayout = LoadFlatLayout("","");
                var slopeLayout = LoadSlopeLayout();
                var wagonConceptList = LoadWagonConcept();

                parameters.Wagon = wagonConceptList.Find(w => w.TypeName == parameters.WagonTypeName);

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

            var flatLayout = LoadFlatLayout("","");
            var slopeLayout = LoadSlopeLayout();
            var wagonConceptList = LoadWagonConcept();

            parameters.Wagon = wagonConceptList.Find(w => w.TypeName == parameters.WagonTypeName);

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
                var timeList = new List<object>();
                var velocityList = GetVelocityList(parameters);

                double startX = ((dynamic)velocityList[0]).x;
                double cumulativeTime = 0.0;

                timeList.Add(new { x = startX, time = cumulativeTime });

                for (var i = 1; i < velocityList.Count; i++)
                {
                    var item_0 = velocityList[i-1];
                    var item_t = velocityList[i];

                    var v0 = ((dynamic)item_0).velocity;
                    var vt = ((dynamic)item_t).velocity;

                    var x0 = ((dynamic)item_0).x;
                    var xt = ((dynamic)item_t).x;

                    double duration = 2*(xt-x0)/(v0 + vt);
                    cumulativeTime = cumulativeTime + duration;

                    timeList.Add(new { x = xt, time = Math.Round(cumulativeTime,2) });
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
    }
}
