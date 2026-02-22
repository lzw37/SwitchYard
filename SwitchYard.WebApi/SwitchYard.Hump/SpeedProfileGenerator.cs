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
        public static HeadwayCheckWagonSpeedProfile Generate(HeadwayCheckWagon hcWagon, FlatLayout flatLayout, SlopeLayout slopeLayout)
        {
            var speedProfile = new HeadwayCheckWagonSpeedProfile() { Wagon = hcWagon};

            double startX = Math.Max(flatLayout.PositionList.First().X,slopeLayout.PositionList.First().X);
            double endX = Math.Min(flatLayout.PositionList.Last().X, slopeLayout.PositionList.Last().X);

            for (double x = startX; x <= endX; x += SpaceStepSize)
            {
                var kineticEnergyResult = HumpEnergyHeightCalculator.CalculateKineticEnergyHeight(flatLayout, slopeLayout, x, hcWagon.EnergyCalculationParams);
                var velocity = kineticEnergyResult.Velocity;
                speedProfile.PositionList.Add(x);
                speedProfile.SpeedList.Add(velocity);
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
