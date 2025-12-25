using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public static double CalculateGravitationPotentialEnergyHeight(FlatLayout flatLayout, double x, EnergyCalculationParams param)
        {
            var height = flatLayout.GetHeight(x);
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
            return 0.0;
        }

        /// <summary>
        /// 计算车辆在指定位置的动能高
        /// </summary>
        /// <param name="flatLayout">线路平面布置图</param>
        /// <param name="x">指定位置的x坐标</param>
        /// <param name="param">能高计算参数</param>
        /// <returns>动能高/m</returns>
        public static double CalculateKineticEnergyHeight(FlatLayout flatLayout, double x, EnergyCalculationParams param)
        {
            var orgKineticEnergyHeight = Math.Pow(param.WagonVelocityOnTop, 2) / (2 * param.g_);

            var gravitationHeight = CalculateGravitationPotentialEnergyHeight(flatLayout, x, param);
            var resistanceHeight = CalculateResistanceEnergyHeight(flatLayout, x, param);
            var breakingHeight = CalculateBreakingEnergyHeight(flatLayout, x, param);

            return orgKineticEnergyHeight - gravitationHeight - resistanceHeight - breakingHeight;
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
            var totalCurveResistancePower = HumpResistanceCalculator.CalculateCurveResistance(curveCorner);

            // 计算当前位置处车辆在溜放部分和调车场部分的风阻力和基本阻力
            var airResistanceOnYard = HumpResistanceCalculator.CalculateAirResistance(
                param.Wagon.GrossMass, param.AirDensity, param.Wagon.WindwardArea,
                param.WagonVelocityOnYard, param.WindVelocity, param.IsHeadWind);
            var airResistanceOnSlop = HumpResistanceCalculator.CalculateAirResistance(
                param.Wagon.GrossMass, param.AirDensity, param.Wagon.WindwardArea,
                param.WagonVelocityOnSlop, param.WindVelocity, param.IsHeadWind);

            var pureResistanceOnYard = HumpResistanceCalculator.CalculatePureResistance(
                param.Wagon.GrossMass, param.Temperature, 1,
                param.WagonVelocityOnYard, param.Wagon.CarTypeParam);
            var pureResistanceOnSlop = HumpResistanceCalculator.CalculatePureResistance(
                param.Wagon.GrossMass, param.Temperature, 0,
                param.WagonVelocityOnSlop, param.Wagon.CarTypeParam);

            // 计算总阻力能高
            var resistanceEnergyHeight = switchResistancePower
                + totalCurveResistancePower
                + (airResistanceOnYard + pureResistanceOnYard) * totalLengthOnYard
                + (airResistanceOnSlop + pureResistanceOnSlop) * totalLengthOnSlop;

            return resistanceEnergyHeight * 0.001;
        }
    }

    /// <summary>
    /// 能高计算参数
    /// </summary>
    public class EnergyCalculationParams
    {
        /// <summary>
        /// 车辆信息
        /// </summary>
        public WagonConcept Wagon { get; set; }

        /// <summary>
        /// 推峰速度/(m/s)
        /// </summary>
        public double WagonVelocityOnTop { get; set; }

        /// <summary>
        /// 车辆在溜放部分的溜放速度/(m/s)
        /// </summary>
        public double WagonVelocityOnSlop { get; set; }

        /// <summary>
        /// 车辆在调车场部分的溜放速度/(m/s)
        /// </summary>
        public double WagonVelocityOnYard { get; set; }

        /// <summary>
        /// 风速/(m/s)
        /// </summary>
        public double WindVelocity { get; set; }

        /// <summary>
        /// 是否逆风（1：逆风，0：顺风）
        /// </summary>
        public int IsHeadWind { get; set; }

        /// <summary>
        /// 空气密度/(kg·s²/m⁴)
        /// </summary>
        public double AirDensity { get; set; }

        /// <summary>
        /// 气温/°C
        /// </summary>
        public double Temperature { get; set; }

        /// <summary>
        /// 自由落体重力加速度/(m/s²)
        /// </summary>
        public double g { get; set; } = 9.8;

        /// <summary>
        /// 考虑了车轮转动惯量的重力加速度/(m/s²)
        /// </summary>
        public double g_ => (this.g / (1 + 0.42 * this.Wagon.AxleNumber / this.Wagon.GrossMass));

        /// <summary>
        /// 减速器激活状态
        /// </summary>
        public Dictionary<string, bool> RetarderActivation{ get; set; }

        /// <summary>
        /// 减速器输出制动力比例
        /// </summary>
        public Dictionary<string, double> RetarderOutput { get; set; }
    }
}
