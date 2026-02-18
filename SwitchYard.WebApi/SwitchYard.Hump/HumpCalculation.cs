using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchYard.Hump
{
    /// <summary>
    /// 表示驼峰计算的主实体，包含计算相关的标识和数据集合
    /// </summary>
    public class HumpCalculation
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
        /// 车辆类型名称
        /// </summary>
        public string WagonType { get; set; }

        /// <summary>
        /// 作业条件ID
        /// </summary>
        public string OperationConditionID { get; set; }

        /// <summary>
        /// 溜放线ID
        /// </summary>
        public string SlopeLineID { get; set; }

        /// <summary>
        /// 计算数据集合
        /// </summary>
        public List<HumpCalculationData>? Data { get; set; }
    }

    /// <summary>
    /// 表示单条驼峰计算数据，包含能量高度等信息
    /// </summary>
    public class HumpCalculationData
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
        /// 驼峰计算ID
        /// </summary>
        public string HumpCalculationID { get; set; }

        /// <summary>
        /// X 坐标
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// 重力能高
        /// </summary>
        public double? GravityEnergyHeight { get;set; }

        /// <summary>
        /// 阻力能高
        /// </summary>
        public double? ResistanceEnergyHeight { get; set; }

        /// <summary>
        /// 动能高
        /// </summary>
        public double? KineticEnergyHeight { get; set; }

        /// <summary>
        /// 制动能高
        /// </summary>
        public double? BreakingEnergyHeight { get; set; }

        /// <summary>
        /// 初始总能高
        /// </summary>
        public double? InitTotalEnergyHeight { get; set; }
    }
}
