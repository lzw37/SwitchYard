using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SwitchYard.Hump
{
    public static class HumpEnergyHeightCalculator
    {
        /// <summary>
        /// 计算车辆在指定位置的重力势能高
        /// </summary>
        /// <param name="flatLayout">线路平面布置图</param>
        /// <param name="x">指定位置的x坐标</param>
        /// <param name="param">能高计算参数</param>
        /// <returns>重力势能高/m</returns>
        public static double CalculateGravitationPotentialEnergyHeight(SlopeLayout slopeLayout, double x, EnergyCalculationParams param)
        {
            var height = slopeLayout.GetHeight(x);
            return height;
        }

        /// <summary>
        /// 计算车辆在指定位置的制动能高
        /// </summary>
        /// <param name="flatLayout">线路平面布置图</param>
        /// <param name="x">指定位置的x坐标</param>
        /// <param name="param">能高计算参数</param>
        /// <returns>制动能高/m</returns>
        public static double CalculateBreakingEnergyHeight(FlatLayout flatLayout, double x, EnergyCalculationParams param)
        {
            double totalEffectiveEnergyHeight = 0.0;

            if(param.RetarderStatusList == null || param.RetarderStatusList.Count == 0)
            {
                return totalEffectiveEnergyHeight;
            }

            foreach (var rs in param.RetarderStatusList)
            {
                if (!rs.IsActivated)
                    continue;

                var retarder = flatLayout.RetarderList.FirstOrDefault(r => r.ID == rs.RetarderID);
                if (retarder != null && retarder.BindingPositionSegment.StartPosition.X <= x)
                {
                    var lengthRate = Math.Min(1, Math.Max(0, (x - retarder.BindingPositionSegment.StartPosition.X) / retarder.BindingPositionSegment.Length));
                    var retarderEnergyHeight = rs.TotalEnergyHeight*rs.Output*lengthRate;

                    totalEffectiveEnergyHeight += retarderEnergyHeight;
                }
            }

            return totalEffectiveEnergyHeight;
        }

        /// <summary>
        /// 计算车辆在指定位置的动能高
        /// </summary>
        /// <param name="flatLayout">线路平面布置图</param>
        /// <param name="x">指定位置的x坐标</param>
        /// <param name="param">能高计算参数</param>
        /// <returns>动能高/m</returns>
        public static KineticEnergyHeightResult CalculateKineticEnergyHeight(FlatLayout flatLayout, SlopeLayout slopeLayout,
            double x, EnergyCalculationParams param, string? positionID=null)
        {
            var orgKineticEnergyHeight = Math.Pow(param.OperationCondition.WagonVelocityOnTop, 2) / (2 * param.Wagon.g_);
            var humpHeight = CalculateGravitationPotentialEnergyHeight(slopeLayout, 0, param);

            var gravitationHeight = CalculateGravitationPotentialEnergyHeight(slopeLayout, x, param);
            var resistanceHeight = CalculateResistanceEnergyHeight(flatLayout, x, param);
            var breakingHeight = CalculateBreakingEnergyHeight(flatLayout, x, param);
            var kineticEnergyHeight = Math.Max(0, orgKineticEnergyHeight + (humpHeight- gravitationHeight) - resistanceHeight - breakingHeight);
            var velocity = Math.Max(0, Math.Sqrt(2 * param.Wagon.g_ * Math.Max(0,kineticEnergyHeight)));

            KineticEnergyHeightResult result = new KineticEnergyHeightResult()
            {
                PositionID = positionID,
                OrgKineticEnergyHeight = orgKineticEnergyHeight,
                GravitationHeight = Math.Round(gravitationHeight,3),
                ResistanceHeight = Math.Round(resistanceHeight,3),
                BreakingHeight = Math.Round(breakingHeight,3),
                KineticEnergyHeight = Math.Round(kineticEnergyHeight,3),
                Velocity = Math.Round(velocity,2)
            };

            return result;
        }

        /// <summary>
        /// 计算车辆在指定区间内的阻力能高
        /// </summary>
        /// <param name="flatLayout">场区平面布置</param>
        /// <param name="startX">起始位置X坐标</param>
        /// <param name="endX">结束位置X坐标</param>
        /// <returns>阻力能高/m</returns>
        public static double CalculateResistanceEnergyHeight(FlatLayout flatLayout, double x, EnergyCalculationParams param)
        {
            var orgPosition = flatLayout.PositionList.OrderBy(p => p.X).First();

            var positionSegments = flatLayout.PositionSegmentList.FindAll(s => s.StartPosition.X < x)
                .OrderBy(s => s.StartPosition.X).ToList();

            if(positionSegments.Count == 0)
            {
                return 0.0;
            }

            // 分别计算当前位置处，溜放部分和调车场部分的总溜放长度
            var totalLengthOnSlop = positionSegments.FindAll(x => x.LocationParam == 0).Sum(x => x.Length);
            var totalLengthOnYard = positionSegments.FindAll(x => x.LocationParam == 1).Sum(x => x.Length);

            var lastSegmentError = positionSegments.Last().EndPosition.X - x;  // 最后一段存在长度误差，需要扣除
            if (totalLengthOnYard == 0)
            {
                totalLengthOnSlop -= lastSegmentError;
            }
            else
            {
                totalLengthOnYard -= lastSegmentError;
            }

            // 计算当前位置处的各类道岔数量及曲线总转角
            var sc = flatLayout.GetSwitchCount(orgPosition.X, x);
            var switchResistancePower = HumpResistanceCalculator.SwitchResistance(sc.ReverseCount, sc.ForwardCound, sc.DiamondCount, sc.SlipCount);

            var curveCorner = flatLayout.GetCurveCornerCount(orgPosition.X, x);
            var totalCurveResistancePower = HumpResistanceCalculator.CalculateCurveResistance(curveCorner + sc.TotalCurveDegree);  
                // 在计算曲线阻力时，除了考虑纯曲线的转角以外，还需加上道岔的曲线转角

            // 计算当前位置处车辆在溜放部分和调车场部分的风阻力和基本阻力
            var airResistanceOnYard = HumpResistanceCalculator.CalculateAirResistance(
                param.Wagon.GrossMass, param.OperationCondition.AirDensity, param.Wagon.WindwardArea,
                param.OperationCondition.WagonVelocityOnYard, param.OperationCondition.WindVelocity, param.OperationCondition.IsHeadWind);
            var airResistanceOnSlop = HumpResistanceCalculator.CalculateAirResistance(
                param.Wagon.GrossMass, param.OperationCondition.AirDensity, param.Wagon.WindwardArea,
                param.OperationCondition.WagonVelocityOnSlope, param.OperationCondition.WindVelocity, param.OperationCondition.IsHeadWind);

            var pureResistanceOnYard = HumpResistanceCalculator.CalculatePureResistance(
                param.Wagon.GrossMass, param.OperationCondition.Temperature, 1,
                param.OperationCondition.WagonVelocityOnYard, param.Wagon.CarTypeParam);
            var pureResistanceOnSlop = HumpResistanceCalculator.CalculatePureResistance(
                param.Wagon.GrossMass, param.OperationCondition.Temperature, 0,
                param.OperationCondition.WagonVelocityOnSlope, param.Wagon.CarTypeParam);

            // 计算总阻力能高
            var resistanceEnergyHeight = switchResistancePower
                + totalCurveResistancePower
                + (airResistanceOnYard + pureResistanceOnYard) * totalLengthOnYard
                + (airResistanceOnSlop + pureResistanceOnSlop) * totalLengthOnSlop;

            return resistanceEnergyHeight * 0.001;
        }

        /// <summary>
        /// 计算指定位置阻力能高的明细分解（基本阻力、风阻力、道岔阻力、曲线阻力），
        /// 返回总能高及计算各项时使用的所有原始参数，便于前端浮窗中显示数值与公式。
        /// </summary>
        public static ResistanceEnergyHeightDetail CalculateResistanceEnergyHeightDetail(
            FlatLayout flatLayout, double x, EnergyCalculationParams param)
        {
            var orgPosition = flatLayout.PositionList.OrderBy(p => p.X).First();

            var positionSegments = flatLayout.PositionSegmentList.FindAll(s => s.StartPosition.X < x)
                .OrderBy(s => s.StartPosition.X).ToList();

            double totalLengthOnSlop = 0.0;
            double totalLengthOnYard = 0.0;
            if (positionSegments.Count > 0)
            {
                totalLengthOnSlop = positionSegments.FindAll(s => s.LocationParam == 0).Sum(s => s.Length);
                totalLengthOnYard = positionSegments.FindAll(s => s.LocationParam == 1).Sum(s => s.Length);
                var lastSegmentError = positionSegments.Last().EndPosition.X - x;
                if (totalLengthOnYard == 0)
                {
                    totalLengthOnSlop -= lastSegmentError;
                }
                else
                {
                    totalLengthOnYard -= lastSegmentError;
                }
            }

            var sc = flatLayout.GetSwitchCount(orgPosition.X, x);
            var switchResistancePower = HumpResistanceCalculator.SwitchResistance(
                sc.ReverseCount, sc.ForwardCound, sc.DiamondCount, sc.SlipCount);

            var pureCurveCorner = positionSegments.Count > 0 ? flatLayout.GetCurveCornerCount(orgPosition.X, x) : 0.0;
            var totalCurveDegree = pureCurveCorner + sc.TotalCurveDegree;
            var curveResistancePower = HumpResistanceCalculator.CalculateCurveResistance(totalCurveDegree);

            var airResistanceOnYard = HumpResistanceCalculator.CalculateAirResistance(
                param.Wagon.GrossMass, param.OperationCondition.AirDensity, param.Wagon.WindwardArea,
                param.OperationCondition.WagonVelocityOnYard, param.OperationCondition.WindVelocity, param.OperationCondition.IsHeadWind);
            var airResistanceOnSlop = HumpResistanceCalculator.CalculateAirResistance(
                param.Wagon.GrossMass, param.OperationCondition.AirDensity, param.Wagon.WindwardArea,
                param.OperationCondition.WagonVelocityOnSlope, param.OperationCondition.WindVelocity, param.OperationCondition.IsHeadWind);

            var pureResistanceOnYard = HumpResistanceCalculator.CalculatePureResistance(
                param.Wagon.GrossMass, param.OperationCondition.Temperature, 1,
                param.OperationCondition.WagonVelocityOnYard, param.Wagon.CarTypeParam);
            var pureResistanceOnSlop = HumpResistanceCalculator.CalculatePureResistance(
                param.Wagon.GrossMass, param.OperationCondition.Temperature, 0,
                param.OperationCondition.WagonVelocityOnSlope, param.Wagon.CarTypeParam);

            // 各分项阻力能高（单位：m）。原始公式得到 N·m/kN，再乘 0.001 转为 m。
            var pureHeight = (pureResistanceOnYard * totalLengthOnYard + pureResistanceOnSlop * totalLengthOnSlop) * 0.001;
            var airHeight = (airResistanceOnYard * totalLengthOnYard + airResistanceOnSlop * totalLengthOnSlop) * 0.001;
            var switchHeight = switchResistancePower * 0.001;
            var curveHeight = curveResistancePower * 0.001;
            var totalHeight = pureHeight + airHeight + switchHeight + curveHeight;

            return new ResistanceEnergyHeightDetail
            {
                X = Math.Round(x, 3),
                TotalHeight = Math.Round(totalHeight, 4),
                PureResistance = new PureResistanceDetail
                {
                    EnergyHeight = Math.Round(pureHeight, 4),
                    UnitResistanceOnSlope = Math.Round(pureResistanceOnSlop, 4),
                    UnitResistanceOnYard = Math.Round(pureResistanceOnYard, 4),
                    LengthOnSlope = Math.Round(totalLengthOnSlop, 3),
                    LengthOnYard = Math.Round(totalLengthOnYard, 3),
                    WagonMass = param.Wagon.GrossMass,
                    Temperature = param.OperationCondition.Temperature,
                    WagonVelocityOnSlope = param.OperationCondition.WagonVelocityOnSlope,
                    WagonVelocityOnYard = param.OperationCondition.WagonVelocityOnYard,
                    CarTypeParam = param.Wagon.CarTypeParam
                },
                AirResistance = new AirResistanceDetail
                {
                    EnergyHeight = Math.Round(airHeight, 4),
                    UnitResistanceOnSlope = Math.Round(airResistanceOnSlop, 4),
                    UnitResistanceOnYard = Math.Round(airResistanceOnYard, 4),
                    LengthOnSlope = Math.Round(totalLengthOnSlop, 3),
                    LengthOnYard = Math.Round(totalLengthOnYard, 3),
                    WagonMass = param.Wagon.GrossMass,
                    AirDensity = param.OperationCondition.AirDensity,
                    WindwardArea = param.Wagon.WindwardArea,
                    WagonVelocityOnSlope = param.OperationCondition.WagonVelocityOnSlope,
                    WagonVelocityOnYard = param.OperationCondition.WagonVelocityOnYard,
                    WindVelocity = param.OperationCondition.WindVelocity,
                    IsHeadWind = param.OperationCondition.IsHeadWind
                },
                SwitchResistance = new SwitchResistanceDetail
                {
                    EnergyHeight = Math.Round(switchHeight, 4),
                    Power = Math.Round(switchResistancePower, 4),
                    ReverseCount = sc.ReverseCount,
                    ForwardCount = sc.ForwardCound,
                    DiamondCount = sc.DiamondCount,
                    SlipCount = sc.SlipCount
                },
                CurveResistance = new CurveResistanceDetail
                {
                    EnergyHeight = Math.Round(curveHeight, 4),
                    Power = Math.Round(curveResistancePower, 4),
                    PureCurveCorner = Math.Round(pureCurveCorner, 4),
                    SwitchCurveDegree = Math.Round(sc.TotalCurveDegree, 4),
                    TotalCurveDegree = Math.Round(totalCurveDegree, 4)
                }
            };
        }
    }

    /// <summary>
    /// 阻力能高分项明细
    /// </summary>
    public class ResistanceEnergyHeightDetail
    {
        public double X { get; set; }
        public double TotalHeight { get; set; }
        public PureResistanceDetail PureResistance { get; set; } = new PureResistanceDetail();
        public AirResistanceDetail AirResistance { get; set; } = new AirResistanceDetail();
        public SwitchResistanceDetail SwitchResistance { get; set; } = new SwitchResistanceDetail();
        public CurveResistanceDetail CurveResistance { get; set; } = new CurveResistanceDetail();
    }

    public class PureResistanceDetail
    {
        public double EnergyHeight { get; set; }
        public double UnitResistanceOnSlope { get; set; }
        public double UnitResistanceOnYard { get; set; }
        public double LengthOnSlope { get; set; }
        public double LengthOnYard { get; set; }
        public double WagonMass { get; set; }
        public double Temperature { get; set; }
        public double WagonVelocityOnSlope { get; set; }
        public double WagonVelocityOnYard { get; set; }
        public int CarTypeParam { get; set; }
    }

    public class AirResistanceDetail
    {
        public double EnergyHeight { get; set; }
        public double UnitResistanceOnSlope { get; set; }
        public double UnitResistanceOnYard { get; set; }
        public double LengthOnSlope { get; set; }
        public double LengthOnYard { get; set; }
        public double WagonMass { get; set; }
        public double AirDensity { get; set; }
        public double WindwardArea { get; set; }
        public double WagonVelocityOnSlope { get; set; }
        public double WagonVelocityOnYard { get; set; }
        public double WindVelocity { get; set; }
        public int IsHeadWind { get; set; }
    }

    public class SwitchResistanceDetail
    {
        public double EnergyHeight { get; set; }
        public double Power { get; set; }
        public int ReverseCount { get; set; }
        public int ForwardCount { get; set; }
        public int DiamondCount { get; set; }
        public int SlipCount { get; set; }
    }

    public class CurveResistanceDetail
    {
        public double EnergyHeight { get; set; }
        public double Power { get; set; }
        public double PureCurveCorner { get; set; }
        public double SwitchCurveDegree { get; set; }
        public double TotalCurveDegree { get; set; }
    }

    /// <summary>
    /// 动能高计算（系列）结果
    /// </summary>
    public class KineticEnergyHeightResult
    {
        /// <summary>
        /// 位置ID
        /// </summary>
        public string? PositionID { get; set; }

        /// <summary>
        /// 初始动能高
        /// </summary>
        public double OrgKineticEnergyHeight { get; set; }

        /// <summary>
        /// 重力势能高/m
        /// </summary>
        public double GravitationHeight { get; set; }

        /// <summary>
        /// 阻力能高/m
        /// </summary>
        public double ResistanceHeight { get; set; }

        /// <summary>
        /// 制动能高/m
        /// </summary>
        public double BreakingHeight { get; set; }

        /// <summary>
        /// 动能高/m
        /// </summary>
        public double KineticEnergyHeight { get; set; }

        /// <summary>
        /// 溜放瞬时速度/(m/s)
        /// </summary>
        public double Velocity { get; set; }
    }

    /// <summary>
    /// 能高计算参数
    /// </summary>
    public class EnergyCalculationParams
    {
        /// <summary>
        /// 实例ID
        /// </summary>
        public string InstanceID { get; set; }

        /// <summary>
        /// 驼峰方案ID
        /// </summary>
        public string HumpSchemeID { get; set; }

        /// <summary>
        /// 计算ID
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 溜放线ID
        /// </summary>
        public string? SlopeLineID { get; set; }

        /// <summary>
        /// 车辆类型
        /// </summary>
        public string? WagonTypeName { get; set; }

        /// <summary>
        /// 溜车条件ID
        /// </summary>
        public string? OperationConditionID { get; set; }

        /// <summary>
        /// 减速器工作状态ID
        /// </summary>
        public string? RetarderStatusID { get; set; }

        /// <summary>
        /// 溜放线
        /// </summary>
        [JsonIgnore]
        public SlopeLine? SlopeLine { get; set; }

        /// <summary>
        /// 车辆类型
        /// </summary>
        [JsonIgnore]
        public WagonConcept? Wagon { get; set; }

        /// <summary>
        /// 车辆溜放条件
        /// </summary>
        [JsonIgnore]
        public OperationCondition? OperationCondition { get; set; }

        /// <summary>
        /// 减速器工作状态
        /// </summary>
        public List<RetarderStatus>? RetarderStatusList { get; set; }
    }
}
