using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SwitchYard.Hump;
using System.Data.Common;
using System.Net;
using System.Runtime.CompilerServices;

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

        /// <summary>
        /// 保存修改后的平面布置图
        /// </summary>
        /// <param name="flatLayout"></param>
        /// <returns></returns>
        [HttpPost(Name = "SaveFlatLayout")]
        public IActionResult SaveFlatLayout(SwitchYard.Hump.FlatLayout flatLayout)
        {
            Console.WriteLine("FlatLayout saved: " + flatLayout.ToString());
            return Ok("FlatLayout saved successfully.");
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
                var flatLayout = LoadFlatLayout();
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
        public IActionResult GetResistanceEnergyHeight(EnergyCalculationParams parameters)
        {
            try
            {
                var flatLayout = LoadFlatLayout();
                var slopeLayout = LoadSlopeLayout();
                var wagonConceptList = LoadWagonConcept();

                parameters.Wagon = wagonConceptList.Find(w => w.TypeName == parameters.WagonTypeName);

                var resistanceEnergyHeightList = new List<object>();
                //foreach (var p in slopeLayout.PositionList)
                for (var i = slopeLayout.PositionList.First().X; i <= slopeLayout.PositionList.Last().X; i += 20)
                {
                    //var energyHeight = HumpEnergyHeightCalculator.CalculateResistanceEnergyHeight(flatLayout, p.X, parameters);
                    //resistanceEnergyHeightList.Add(new { x = p.X, height = Math.Round(energyHeight,3) });
                    var energyHeight = HumpEnergyHeightCalculator.CalculateResistanceEnergyHeight(flatLayout, i, parameters);
                    resistanceEnergyHeightList.Add(new { x = i, height = Math.Round(energyHeight, 3) });
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
                var flatLayout = LoadFlatLayout();
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

            var flatLayout = LoadFlatLayout();
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
