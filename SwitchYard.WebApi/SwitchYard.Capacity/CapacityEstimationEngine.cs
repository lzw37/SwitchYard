using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SwitchYard.Capacity
{
    /// <summary>
    /// 车站能力计算任务
    /// </summary>
    public class StationCapacityEstimationTask
    {
        /// <summary>
        /// 实例ID
        /// </summary>
        public required string InstanceID { get; set; }

        /// <summary>
        /// 任务ID
        /// </summary>
        public required string ID { get; set; }

        /// <summary>
        /// 任务名称
        /// </summary>
        public string? TaskName { get; set; }

        /// <summary>
        /// 车站设计方案ID
        /// </summary>
        /// </summary>
        public string? StationSchemeID { get; set; }

        /// <summary>
        /// 车站设计方案
        /// </summary>
        [JsonIgnore]
        public required StationScheme StationScheme { get; set; }

        /// <summary>
        /// 列车运行方案ID
        /// </summary>
        public string? TrainOperationSchemeID { get; set; }

        /// <summary>
        /// 列车运行方案
        /// </summary>
        [JsonIgnore]
        public required TrainOperationScheme TrainOperationScheme { get; set; }

        /// <summary>
        /// 生成序列化的能力计算输入数据
        /// </summary>
        /// <returns></returns>
        public string BuildJson()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    /// <summary>
    /// 能力分析引擎
    /// </summary>
    public class CapacityEstimationEngine
    {

    }
}
