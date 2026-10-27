using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SwitchYard.Hump
{
    /// <summary>
    /// 车辆溜放条件
    /// </summary>
    public class OperationCondition
    {
        /// <summary>
        /// 实例ID
        /// </summary>
        public string InstanceID { get; set; }

        /// <summary>
        /// 车辆溜放条件ID
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 车辆溜放条件名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 推峰速度/(m/s)
        /// </summary>
        public double WagonVelocityOnTop { get; set; }

        /// <summary>
        /// 车辆在溜放部分的溜放速度/(m/s)
        /// </summary>
        public double WagonVelocityOnSlope { get; set; }

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
        /// 气流密度/(kg·s^2/m^4)
        /// </summary>
        public double AirDensity { get; set; }

        /// <summary>
        /// 气温/°C
        /// </summary>
        public double Temperature { get; set; }
    }
}
