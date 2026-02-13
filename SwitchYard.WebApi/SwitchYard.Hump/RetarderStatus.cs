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
        /// <summary>
        /// 减速器工作状态ID
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 减速器激活状态
        /// </summary>
        public Dictionary<string, bool> RetarderActivation { get; set; }

        /// <summary>
        /// 减速器输出制动力比例
        /// </summary>
        public Dictionary<string, double> RetarderOutput { get; set; }
    }
}
