using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Text.Json;


namespace SwitchYard.Capacity
{
    /// <summary>
    /// 能力计算实例
    /// </summary>
    public class CapacityInstance
    {
        /// <summary>
        /// 实例ID
        /// </summary>
        public string? ID { get; set; }

        /// <summary>
        /// 实例名称
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// 所有者
        /// </summary>
        public string? Owner { get; set; }

        /// <summary>
        /// 创建日期
        /// </summary>
        public DateTime? CreatedDate { get; set; }

        /// <summary>
        /// 是否激活
        /// </summary>
        public bool? IsActive { get; set; }
    }
}
