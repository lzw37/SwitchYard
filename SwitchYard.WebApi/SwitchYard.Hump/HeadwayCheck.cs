using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchYard.Hump
{
    /// <summary>
    /// 驼峰检算方案
    /// </summary>
    public class HeadwayCheckScheme
    {
        /// <summary>
        /// 驼峰实例ID
        /// </summary>
        public string InstanceID { get; set; }

        /// <summary>
        /// 驼峰检算方案ID
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 驼峰方案ID
        /// </summary>
        public string HumpSchemeID { get; set; }

        /// <summary>
        /// 驼峰检算方案名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 溜放车辆列表
        /// </summary>
        public List<HeadwayCheckWagon> WagonList { get; set; }

        /// <summary>
        /// 推峰速度
        /// </summary>
        public double WagonVelocityOnTop { get; set; }

        /// <summary>
        /// 溜放线ID
        /// </summary>
        public string SlopeLineID { get; set; }
    }

    /// <summary>
    /// 驼峰检算车辆信息
    /// </summary>
    public class HeadwayCheckWagon
    {
        /// <summary>
        /// 溜放顺序
        /// </summary>
        public int Sequence { get; set; }

        /// <summary>
        /// 驼峰计算ID
        /// </summary>
        public string HumpCalculationID { get; set; }
    }
}
