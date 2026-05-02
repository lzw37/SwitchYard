using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace SwitchYard.Hump
{
    public static class SpeedProfileGenerator
    {
        /// <summary>
        /// 空间离散步长/m
        /// </summary>
        public static double SpaceStepSize { get; set; } = 1.0;

        /// <summary>
        /// 计算勾车溜放的速度曲线
        /// </summary>
        /// <param name="hcWagon"></param>
        /// <param name="flatLayout"></param>
        /// <param name="slopeLayout"></param>
        /// <returns></returns>
        public static HeadwayCheckWagonSpeedProfile Generate(
            HeadwayCheckWagon hcWagon,
            FlatLayout flatLayout,
            SlopeLayout slopeLayout,
            double? spaceStepSize = null)
        {
            var speedProfile = new HeadwayCheckWagonSpeedProfile() { Wagon = hcWagon};
            var flatXs = flatLayout?.PositionList?.Select(p => p?.X ?? 0).ToList() ?? new List<double>();
            var slopeXs = slopeLayout?.PositionList?.Select(p => p?.X ?? 0).ToList() ?? new List<double>();
            if (flatXs.Count == 0 || slopeXs.Count == 0)
            {
                return speedProfile;
            }

            double stepSize = spaceStepSize.HasValue && spaceStepSize.Value > 0
                ? spaceStepSize.Value
                : SpaceStepSize;
            double startX = Math.Max(flatXs.Min(), slopeXs.Min());
            double endX = Math.Min(flatXs.Max(), slopeXs.Max());

            if (endX < startX)
            {
                return speedProfile;
            }

            void AppendSample(double x)
            {
                if (speedProfile.PositionList.Count > 0 &&
                    Math.Abs(speedProfile.PositionList[^1] - x) <= 1e-9)
                {
                    return;
                }

                var kineticEnergyResult = HumpEnergyHeightCalculator.CalculateKineticEnergyHeight(flatLayout, slopeLayout, x, hcWagon.EnergyCalculationParams);
                var velocity = kineticEnergyResult.Velocity;
                speedProfile.PositionList.Add(x);
                speedProfile.SpeedList.Add(velocity);
            }

            for (double x = startX; x <= endX + 1e-9; x += stepSize)
            {
                AppendSample(Math.Min(x, endX));

                // 车辆已减速至 0，无法继续溜放，停止后续速度曲线计算
                if (speedProfile.SpeedList[^1] <= 0)
                {
                    break;
                }
            }

            if (speedProfile.PositionList.Count > 0 &&
                speedProfile.PositionList[^1] < endX - 1e-9 &&
                speedProfile.SpeedList[^1] > 0)
            {
                AppendSample(endX);
            }

            return speedProfile;
        }
    }

    public class HeadwayCheckWagonSpeedProfile
    {
        public HeadwayCheckWagon Wagon { get; set; }
        public List<double> PositionList { get; set; } = new List<double>();
        public List<double> SpeedList { get; set; } = new List<double>();
    }
}
