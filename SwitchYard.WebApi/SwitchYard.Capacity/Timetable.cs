using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchYard.Capacity
{
    /// <summary>
    /// 列车运行方案
    /// </summary>
    public class TrainOperationScheme
    {
        /// <summary>
        /// 实例ID
        /// </summary>
        public string? InstanceID { get; set; }

        /// <summary>
        /// 车站ID
        /// </summary>
        public string? StationSchemeID { get; set; }

        /// <summary>
        /// 列车列表
        /// </summary>
        public List<Train> Trains { get; set; } = new List<Train>();
    }

    /// <summary>
    /// 列车运行时刻表条目
    /// </summary>
    public class Timetable
    {
        /// <summary>
        /// 实例ID
        /// </summary>
        public string? InstanceID { get; set; }

        /// <summary>
        /// 车站ID
        /// </summary>
        public string? StationSchemeID { get; set; }

        /// <summary>
        /// 条目ID
        /// </summary>
        public int? ID { get; set; }

        /// <summary>
        /// 列车ID
        /// </summary>
        public string? TrainID { get; set; }

        /// <summary>
        /// 运行类型（如到达、出发等）
        /// </summary>
        public string? MovementType { get; set; }

        /// <summary>
        /// 序号
        /// </summary>
        public int? Sequence { get; set; }

        /// <summary>
        /// 必要标签（只能选择包含所有标签的进路）
        /// </summary>
        public HashSet<string>? RequiredRouteTags { get; set; }

        /// <summary>
        /// 禁止标签（不能选择包含任一标签的进路）
        /// </summary>
        public HashSet<string>? ForbidRouteTags { get; set;  }

        /// <summary>
        /// 开始时间
        /// </summary>
        public TimeSpan? StartTime { get; set; }

        /// <summary>
        /// 结束时间
        /// </summary>
        public TimeSpan? EndTime { get; set; }

        /// <summary>
        /// 最小持续时间（秒）
        /// </summary>
        public int? MinDuration { get; set; }

        /// <summary>
        /// 最大持续时间（秒）
        /// </summary>
        public int? MaxDuration { get; set; }
    }

    /// <summary>
    /// 列车
    /// </summary>
    public class Train
    {
        /// <summary>
        /// 实例ID
        /// </summary>
        public string? InstanceID { get; set; }

        /// <summary>
        /// 列车ID
        /// </summary>
        public string? ID { get; set; }

        /// <summary>
        /// 列车类型
        /// </summary>
        public string? Type { get; set; }

        public List<Timetable> Timetables { get; set; } = new List<Timetable>();
    }
}
