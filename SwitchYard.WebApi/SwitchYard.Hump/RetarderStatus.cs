using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SwitchYard.Hump
{
    /// <summary>
    /// 减速器工作状态
    /// </summary>
    public class RetarderStatus
    {
        [JsonIgnore]
        public string? HumpCalculationID { get; set; }
        /// <summary>
        /// 减速器ID
        /// </summary>
        public string RetarderID { get; set; }

        /// <summary>
        /// 是否工作
        /// </summary>
        public bool IsActivated { get; set; }

        /// <summary>
        /// 输出制动力比例（取值范围为0-1）
        /// </summary>
        public double Output { get; set; }

        /// <summary>
        /// 设计总制动能高度/(m)
        /// </summary>
        public double TotalEnergyHeight { get; set; }
    }
}
