using System.Text.Json.Serialization;
using System.Text.Json;

namespace SwitchYard.Capacity
{
    /// <summary>
    /// 车站设计方案
    /// </summary>
    public class StationScheme
    {
        /// <summary>
        /// 实例ID
        /// </summary>
        public string? InstanceID { get; set; }

        /// <summary>
        /// 车站设计方案ID
        /// </summary>
        public string? ID { get; set; }

        /// <summary>
        /// 车站设计方案名称
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// 信号机
        /// </summary>
        public List<Signal> Signals { get; set; } = new List<Signal>();

        /// <summary>
        /// 绝缘节
        /// </summary>
        public List<InsulationJoint> InsulationJoints { get; set; } = new List<InsulationJoint>();

        /// <summary>
        /// 站台
        /// </summary>
        public List<Platform> Platforms { get; set; } = new List<Platform>();

        /// <summary>
        /// 道岔
        /// </summary>
        public List<Switch> LayoutSwitches { get; set; } = new List<Switch>();

        /// <summary>
        /// 节点集合
        /// </summary>
        [JsonIgnore]
        public HashSet<Node>? Nodes { get; set; }

        /// <summary>
        /// 边集合
        /// </summary>
        [JsonIgnore]
        public HashSet<Link>? Links { get; set; }

        /// <summary>
        /// 轨道电路区段集合
        /// </summary>
        [JsonIgnore]
        public HashSet<Cell>? Cells { get; set; }

        /// <summary>
        /// 进路集合
        /// </summary>
        [JsonIgnore]
        public HashSet<Route>? Routes { get; set; }

        /// <summary>
        /// 毗邻的区间集合
        /// </summary>
        [JsonIgnore]
        public HashSet<Segment>? AdjecentSegments { get; set; }
    }

    /// <summary>
    /// 信号机
    /// </summary>
    public class Signal
    {
        /// <summary>
        /// 实例ID
        /// </summary>
        public string? InstanceID { get; set; }

        /// <summary>
        /// 车站ID
        /// </summary>
        public string? StationSchemeID { get; set; }

        public string? ID { get; set; }

        public string? Type { get; set; }

        public string? Direction { get; set; }

        public string? BindingNodeID { get; set; }
    }

    /// <summary>
    /// 绝缘节
    /// </summary>
    public class InsulationJoint
    {
        /// <summary>
        /// 实例ID
        /// </summary>
        public string? InstanceID { get; set; }

        /// <summary>
        /// 车站ID
        /// </summary>
        public string? StationSchemeID { get; set; }

        public string? ID { get; set; }

        public string? Type { get; set; }

        public string? BindingNodeID { get; set; }
    }

    /// <summary>
    /// 站台
    /// </summary>
    public class Platform
    {
        /// <summary>
        /// 实例ID
        /// </summary>
        public string? InstanceID { get; set; }

        /// <summary>
        /// 车站ID
        /// </summary>
        public string? StationSchemeID { get; set; }

        public string? ID { get; set; }

        public string? Name { get; set; }

        public double? X { get; set; }

        public double? Y { get; set; }

        public double? Width { get; set; }

        public double? Height { get; set; }
    }

    /// <summary>
    /// 道岔分支向量
    /// </summary>
    public class SwitchBranchVector
    {
        public double? X { get; set; }

        public double? Y { get; set; }

        public string? BindingLinkID { get; set; }

        [JsonIgnore]
        public Link? BindingLink { get; set; }
    }

    /// <summary>
    /// 道岔
    /// </summary>
    public class Switch
    {
        /// <summary>
        /// 实例ID
        /// </summary>
        public string? InstanceID { get; set; }

        /// <summary>
        /// 车站ID
        /// </summary>
        public string? StationSchemeID { get; set; }

        public string ID { get; set; }

        public string Type { get; set; }

        public string BindingNodeID { get; set; }

        public Node BindingNode { get; set; }

        public List<SwitchBranchVector> BranchVectorList { get; set; } = new List<SwitchBranchVector>();
    }

    public class Cell
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
        /// 轨道电路区段ID
        /// </summary>
        public string? ID { get; set; }

        /// <summary>
        /// 边ID列表（逗号分隔）
        /// </summary>
        public string? LinkIDList { get; set; }

        /// <summary>
        /// 边对象集合
        /// </summary>
        [JsonIgnore]
        public HashSet<Link> Links { get; set; } = new HashSet<Link>();
    }

    /// <summary>
    /// 线路图中的连接边
    /// </summary>
    public class Link
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
        /// 边ID
        /// </summary>
        public int? ID { get; set; }

        /// <summary>
        /// 起始节点ID
        /// </summary>
        public int? FromNodeID { get; set; }

        /// <summary>
        /// 终止节点ID
        /// </summary>
        public int? ToNodeID { get; set; }

        /// <summary>
        /// 起始节点对象
        /// </summary>
        [JsonIgnore]
        public Node? FromNode { get; set; }

        /// <summary>
        /// 终止节点对象
        /// </summary>
        public Node? ToNode { get; set; }
    }

    /// <summary>
    /// 线路图中的节点
    /// </summary>
    public class Node
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
        /// 节点ID
        /// </summary>
        public int? ID { get; set; }

        /// <summary>
        /// X坐标
        /// </summary>
        public double? X { get; set; }

        /// <summary>
        /// Y坐标
        /// </summary>
        public double? Y { get; set; }

        /// <summary>
        /// 入边集合
        /// </summary>
        [JsonIgnore]
        public HashSet<Link> InLinks { get; set; } = new HashSet<Link>();
        
        /// <summary>
        /// 出边集合
        /// </summary>
        [JsonIgnore]
        public HashSet<Link> OutLinks { get; set; } = new HashSet<Link>();
    }

    /// <summary>
    /// 进路
    /// </summary>
    public class Route
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
        /// 进路ID
        /// </summary>
        public string? ID { get; set; }

        /// <summary>
        /// 股道ID列表（逗号分隔）
        /// </summary>
        public string? LinkIDList { get; set; }

        /// <summary>
        /// 边对象集合
        /// </summary>
        [JsonIgnore]
        public HashSet<Link> Links { get; set; } = new HashSet<Link>();

        /// <summary>
        /// 单元ID列表（逗号分隔）
        /// </summary>
        public string? CellIDList { get; set; }

        /// <summary>
        /// 单元对象集合
        /// </summary>
        [JsonIgnore]
        public HashSet<Cell> Cells { get; set; } = new HashSet<Cell>();

        /// <summary>
        /// 进路标签
        /// </summary>
        public HashSet<string>? Tags { get; set; }
    }

    /// <summary>
    /// 区间（两站之间的线路段）
    /// </summary>
    public class Segment
    {
        /// <summary>
        /// 实例ID
        /// </summary>
        public string? InstanceID { get; set; }

        /// <summary>
        /// 区间ID
        /// </summary>
        public string? ID { get; set; }

        /// <summary>
        /// 起始车站ID
        /// </summary>
        public string? FromStationSchemeID { get; set; }

        /// <summary>
        /// 终止车站ID
        /// </summary>
        public string? ToStationSchemeID { get; set; }

        /// <summary>
        /// 区间名称
        /// </summary>
        public string? Name { get; set; }

        public string? AttachingNodeID { get; set; }
    }

    /// <summary>
    /// 车站进路占用时间
    /// </summary>
    public class StationRouteTime
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
        /// 进路ID
        /// </summary>
        public string? RouteID { get; set; }

        /// <summary>
        /// 单元ID
        /// </summary>
        public string? CellID { get; set; }

        /// <summary>
        /// 开始占用偏移量（秒）
        /// </summary>
        public int? StartOccupationShift { get; set; }

        /// <summary>
        /// 结束占用偏移量（秒）
        /// </summary>
        public int? EndOccupationShift { get; set; }
    }
}
