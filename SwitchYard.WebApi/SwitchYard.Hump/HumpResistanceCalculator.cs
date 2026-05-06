namespace SwitchYard.Hump
{
    public static class HumpResistanceCalculator
    {
        /// <summary>
        /// 计算车辆溜放的单位基本阻力
        /// </summary>
        /// <param name="wagonMass">计算车辆总重量/t</param>
        /// <param name="temperature">环境气温/°C</param>
        /// <param name="locationParam">地点参数（1:调车场，0:溜放部分）</param>
        /// <param name="velocity">速度/(m/s)</param>
        /// <param name="carTypeParam">车辆类型参数（1:难行车，0:中行车，-1:易行车）</param>
        /// <returns>返回单位基本阻力值/(N/kN)</returns>

        public static double CalculatePureResistance(double wagonMass, double temperature, int locationParam, double velocity, int carTypeParam)
        {
            double Q = wagonMass;  // 车辆总重量/t
            double t = temperature;  // 环境气温/°C
            double v = velocity; // 速度/(m/s)
            int k = locationParam; // 地点参数（1:调车场，0:驼峰溜放部分）

            double sigma_pure = 0.602 - (0.0012 + 0.00002 * Q) * t
                - 0.003 * Q + 0.00002 * Math.Pow(t, 2);  // 计算车辆溜放基本阻力离散程度的均方差

            double r = 2.439 + (0.00008 * Q - 0.01743) * t - 0.015 * Q + 0.00017 * Math.Pow(t, 2)
                + 0.1 * v + carTypeParam * 1.65 * sigma_pure + (1 - k) * 0.39;  // 计算车辆溜放的基本阻力/(N/kN)

            return r;
        }

        /// <summary>
        /// 计算单位风阻力
        /// </summary>
        /// <param name="wagonMass">车辆总重/t</param>
        /// <param name="airDensity">气流密度/(kg·s^2/m^4)</param>
        /// <param name="windwardArea">迎风面积/m^2</param>
        /// <param name="wagonVelocity">车辆速度/(m/s)</param>
        /// <param name="windVelocity">风速/(m/s)</param>
        /// <param name="isHeadWind">是否为逆风（1:逆风，0:顺风）</param>
        /// <returns>返回单位风阻力/(N/kN)</returns>
        public static double CalculateAirResistance(double wagonMass, double airDensity, double windwardArea,
            double wagonVelocity, double windVelocity, int isHeadWind)
        {
            double rho = airDensity; // 气流密度/(kg·s^2/m^4)
            double f= windwardArea; // 迎风面积/m^2
            double v_wagon = wagonVelocity; // 车辆速度/(m/s)
            double v_wind = windVelocity; // 风速/(m/s)
            double Q = wagonMass; // 车辆总重/t

            double beta = 0; // 风向与车辆纵轴方向的夹角/°

            if(v_wagon + v_wind * isHeadWind * Math.Cos(beta)==0) // 车与风合成速度为0时，相对静止，风阻力为0
            {
                return 0.0;
            }

            double alpha = Math.Atan((v_wind * Math.Sin(beta)) / (v_wagon + v_wind * isHeadWind* Math.Cos(beta))); // 风速与车速的合成速度/°

            double cx1 = 1;
            double cx0 = 1;
            // 根据《铁路驼峰及调车场设计规范》（TB 10062-2018）规定，计算风阻力时，beta取0，alpha取0，cx1/cx0取1

            double r = (rho * f * (cx1 / cx0)) / (2 * Q * Math.Pow(Math.Cos(alpha), 2)) 
                * Math.Pow(v_wagon + isHeadWind * v_wind * Math.Cos(beta), 2);

            return r; // 返回空气阻力/(N/kN)
        }

        /// <summary>
        /// 计算曲线基本阻力功
        /// </summary>
        /// <param name="curveCorner">曲线转角角度/°</param>
        /// <param name="length">曲线长度/m</param>
        /// <returns>返回曲线基本阻力/(N·m/kN)</returns>
        public static double CalculateCurveResistance(double curveCorner)
        {
            double e = curveCorner * 8;  // 每 1° 曲线转角的单位阻力功采用8 N·m/kN
            return e; // 返回曲线基本阻力功/(N·m/kN)
        }

        /// <summary>
        /// 计算道岔基本阻力
        /// </summary>
        /// <param name="reverseCount">逆向道岔数量</param>
        /// <param name="forwardCount">顺向道岔数量</param>
        /// <param name="diamondCount">菱形交叉数量</param>
        /// <param name="slipCount">交分/三开道岔数量</param>
        /// <returns>道岔基本阻力功/（N·m/kN）</returns>
        public static double SwitchResistance(int reverseCount, int forwardCount, int diamondCount, int slipCount)
        {
            double e = reverseCount * 24 + (forwardCount + diamondCount) * 12 + slipCount * 48; 
            return e;
        }
    }
}
