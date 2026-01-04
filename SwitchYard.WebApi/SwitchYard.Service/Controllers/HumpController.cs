using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SwitchYard.Hump;
using System.Data.Common;

namespace SwitchYard.Service.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class HumpController : Controller
    {
        IConfiguration _config;
        ILogger<HumpController> _logger;

        public HumpController(ILogger<HumpController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _config = configuration;
        }

        /// <summary>
        /// 获取驼峰溜放部分的平面布置图
        /// </summary>
        /// <returns></returns>
        [HttpGet(Name = "GetFlatLayout")]
        public IActionResult GetFlatLayout()
        {
            try
            {
                var flatLayout = LoadFlatLayout();

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
        private Hump.FlatLayout LoadFlatLayout()
        {
            var flatLayout = new SwitchYard.Hump.FlatLayout();

            DBConnector dbConnector = DBConnector.GetDBConnector();
            flatLayout.PositionList = dbConnector.Query<SwitchYard.Hump.HPosition>("SELECT * FROM position");
            flatLayout.PositionSegmentList = dbConnector.Query<SwitchYard.Hump.HPositionSegment>("SELECT * FROM positionsegment");
            flatLayout.SwitchList = dbConnector.Query<SwitchYard.Hump.Switch>("SELECT * FROM switch");
            flatLayout.RetarderList = dbConnector.Query<SwitchYard.Hump.Retarder>("SELECT * FROM retarder");

            foreach(var seg in flatLayout.PositionSegmentList)
            {
                seg.StartPosition = flatLayout.PositionList.Find(p => p.ID == seg.StartPositionID);
                seg.EndPosition = flatLayout.PositionList.Find(p => p.ID == seg.EndPositionID);
            }

            foreach(var sw in flatLayout.SwitchList)
            {
                sw.BindingPosition = flatLayout.PositionList.Find(p => p.ID == sw.BindingPositionID);
                sw.BindingPositionSegment = flatLayout.PositionSegmentList.Find(s => s.ID == sw.BindingPositionSegmentID);
            }

            foreach(var retarder in flatLayout.RetarderList)
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

        [HttpPost(Name = "SaveFlatLayout")]
        public IActionResult SaveFlatLayout(SwitchYard.Hump.FlatLayout flatLayout)
        {
            Console.WriteLine("FlatLayout saved: " + flatLayout.ToString());
            return Ok("FlatLayout saved successfully.");
        }

        [HttpGet(Name = "GetWagonConcept")]
        public IActionResult GetWagonConcept() 
        {
            try
            {
                DBConnector dbConnector = DBConnector.GetDBConnector();
                var wagonConceptList = dbConnector.Query<SwitchYard.Hump.WagonConcept>("SELECT * FROM wagonconcept");
                _logger.LogInformation("WagonConcept retrieved with {WagonConceptCount} entries.", wagonConceptList?.Count ?? 0);
                return Ok(wagonConceptList);
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, "Error retrieving WagonConcept.");
                return StatusCode(500, "Internal server error while retrieving WagonConcept.");
            }
        }

        [HttpGet(Name = "GetSlopeLayout")]
        public IActionResult GetSlopeLayout()
        {
            try
            {
                var slopeLayout = new SwitchYard.Hump.SlopeLayout();

                DBConnector dbConnector = DBConnector.GetDBConnector();
                slopeLayout.PositionList = dbConnector.Query<SwitchYard.Hump.VPosition>("SELECT * FROM vposition");
                slopeLayout.PositionSegmentList = dbConnector.Query<SwitchYard.Hump.VPositionSegment>("SELECT * FROM vpositionsegment");    

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
    }
}
