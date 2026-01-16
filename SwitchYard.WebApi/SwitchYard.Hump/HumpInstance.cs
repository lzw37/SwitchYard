using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchYard.Hump
{
    /// <summary>
    /// 驼峰设计实例
    /// </summary>
    public class HumpInstance
    {
        /// <summary>
        /// 实例ID
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 实例名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 所有者用户ID
        /// </summary>
        public string OwnerID { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedDate { get; set; }

        /// <summary>
        /// 是否生效
        /// </summary>
        public int IsActive { get; set; }
    }
}
