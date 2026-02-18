using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SwitchYard.Hump
{
    /// <summary>
    /// 溜放线
    /// </summary>
    public class SlopeLine
    {
        /// <summary>
        /// 溜放线ID
        /// </summary>
        public string ID { get; set; } = string.Empty;

        /// <summary>
        /// 所属实例ID
        /// </summary>
        public string InstanceID { get; set; }

        /// <summary>
        /// 溜放线名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 溜放线的平面展开图
        /// </summary>
        public FlatLayout? FlatLayout { get; set; }
    }

    /// <summary>
    /// 驼峰从峰顶至某调车线计算点的平面布置（展开）图
    /// </summary>
    public class FlatLayout
    {
        /// <summary>
        /// 实例ID
        /// </summary>
        public string InstanceID { get; set; }

        /// <summary>
        /// 溜放线ID
        /// </summary>
        public string SlopeLineID { get; set; }

        /// <summary>
        /// 位置点列表
        /// </summary>
        public List<HPosition> PositionList { get; set; }

        /// <summary>
        /// 位置区间列表
        /// </summary>
        public List<HPositionSegment> PositionSegmentList { get; set; }

        /// <summary>
        /// 道岔列表
        /// </summary>
        public List<Switch> SwitchList { get; set; }

        /// <summary>
        /// 减速器列表
        /// </summary>
        public List<Retarder> RetarderList { get; set; }

        /// <summary>
        /// 获取某位置范围内的各类道岔数量
        /// </summary>
        /// <param name="startX">开始位置x坐标</param>
        /// <param name="endX">结束位置x坐标</param>
        /// <returns>各类道岔数量</returns>
        public SwitchCount GetSwitchCount(double startX, double endX)
        {
            var switches = this.SwitchList.FindAll(s => s.BindingPosition?.X >= startX && s.BindingPosition?.X <= endX);
            var diamonds = this.SwitchList.FindAll(s => s.BindingPositionSegment?.StartPosition.X >= startX && s.BindingPositionSegment?.EndPosition.X <= endX);
            var count = new SwitchCount()
            {
                ForwardCound = switches.Sum(s =>
                {
                    return (s.Type == SwitchTypes.Single && s.Direction == SwitchDirections.Forward) ? 1 : 0;
                }),

                ReverseCount = switches.Sum(s =>
                {
                    return (s.Type == SwitchTypes.Single && s.Direction == SwitchDirections.Reverse) ? 1 : 0;
                }),

                SlipCount = switches.Sum(s =>
                {
                    return (s.Type == SwitchTypes.Slip) ? 1 : 0;
                }),

                DiamondCount = diamonds.Sum(s =>
                {
                    return s.Type == SwitchTypes.Diamond ? 1 : 0;
                }),

                TotalCurveDegree = switches.Sum(s =>
                {
                    return s.CurveDegree;
                })
            };
            return count;
        }

        /// <summary>
        /// 获取某位置范围内的曲线总转角
        /// </summary>
        /// <param name="startX">开始位置x坐标</param>
        /// <param name="endX">结束位置x坐标</param>
        /// <returns>总转角/°</returns>
        public double GetCurveCornerCount(double startX, double endX)
        {
            var curveCorners = this.PositionSegmentList.FindAll(segment => segment.EndPosition.X >= startX && segment.StartPosition.X <= endX);

            var sum = curveCorners.Sum(segment => segment.CurveDegree);
            var firstSegment = curveCorners.First();
            var lastSegment = curveCorners.Last();

            var startLengthError = startX - firstSegment.StartPosition.X;
            var endLengthError = lastSegment.EndPosition.X - endX;

            var startCornerError = firstSegment.CurveDegree * (startLengthError / firstSegment.Length);
            var endCornerError = lastSegment.CurveDegree * (endLengthError / lastSegment.Length);

            return sum - startCornerError - endCornerError;
        }

        /// <summary>
        /// 获取某x坐标处的高度
        /// </summary>
        /// <param name="x">位置x坐标</param>
        /// <returns>某x坐标处的高度/m</returns>
        public double GetHeight(double x)
        {
            var seg = PositionSegmentList.Find(s => s.StartPosition.X <= x && s.EndPosition.X >= x);
            if (seg == null)
            {
                throw new ApplicationException("x坐标错误，找不到区间");
            }
            var rate = (x - seg.StartPosition.X) / seg.Length;
            var height = seg.StartPosition.Height + rate * (seg.EndPosition.Height - seg.StartPosition.Height);
            return height;
        }
    }

    /// <summary>
    /// 驼峰从峰顶至某调车线计算点的纵断面图
    /// </summary>
    public class SlopeLayout
    {
        public List<VPosition> PositionList { get; set; }

        public List<VPositionSegment> PositionSegmentList { get; set; }

        /// <summary>
        /// 获取某x坐标处的高度
        /// </summary>
        /// <param name="x">位置x坐标</param>
        /// <returns>某x坐标处的高度/m</returns>
        public double GetHeight(double x)
        {
            var seg = PositionSegmentList.Find(s => s.StartPosition.X <= x && s.EndPosition.X >= x);
            if (seg == null)
            {
                throw new ApplicationException("x坐标错误，找不到区间");
            }
            var rate = (x - seg.StartPosition.X) / seg.Length;
            var height = seg.StartPosition.Height + rate * (seg.EndPosition.Height - seg.StartPosition.Height);
            return height;
        }
    }

    /// <summary>
    /// 位置点
    /// </summary>
    public abstract class Position
    {
        /// <summary>
        /// 实例ID
        /// </summary>
        public string InstanceID { get; set; }

        /// <summary>
        /// 位置点ID
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 该位置点的x坐标/m
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// 该位置点的高度/m
        /// </summary>
        public double Height { get; set; }
    }

    /// <summary>
    /// 水平位置点
    /// </summary>
    public class HPosition : Position
    {

    }

    /// <summary>
    /// 垂直位置点
    /// </summary>
    public class VPosition : Position
    {
        /// <summary>
        /// 驼峰方案ID
        /// </summary>
        public string HumpSchemeID { get; set; }

    }

    /// <summary>
    /// 位置区间
    /// </summary>
    public abstract class PositionSegment
    {
        /// <summary>
        /// 实例ID
        /// </summary>
        public string InstanceID { get; set; }

        /// <summary>
        /// 位置区间ID
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 起始位置点
        /// </summary>
        [JsonIgnore]
        public Position? StartPosition { get; set; }

        /// <summary>
        /// 终止位置点
        /// </summary>
        [JsonIgnore]
        public Position? EndPosition { get; set; }

        /// <summary>
        /// 起始位置点ID
        /// </summary>
        public string StartPositionID { get; set; }

        /// <summary>
        /// 终止位置点ID
        /// </summary>
        public string EndPositionID { get; set; }

        /// <summary>
        /// 长度/m
        /// </summary>
        public double Length { get; set; }
    }

    /// <summary>
    /// 水平位置区间
    /// </summary>
    public class HPositionSegment : PositionSegment
    {
        /// <summary>
        /// 转角总度数/°
        /// </summary>
        public double CurveDegree { get; set; }

        /// <summary>
        /// 转角方向
        /// </summary>
        public CurveDirections CurveDirection { get; set; }

        /// <summary>
        /// 位置参数（1:调车场,0:溜放部分）
        /// </summary>
        public int LocationParam { get; set; }
    }

    /// <summary>
    /// 垂直位置区间
    /// </summary>
    public class VPositionSegment : PositionSegment
    {
        /// <summary>
        /// 驼峰方案ID
        /// </summary>
        public string HumpSchemeID{ get; set; }

        /// <summary>
        /// 坡度/‰
        /// </summary>
        public double Gradient { get; set; }

        /// <summary>
        /// 高度/m
        /// </summary>
        public double Height { get; set; }
    }

    /// <summary>
    /// 道岔
    /// </summary>
    public class Switch
    {
        /// <summary>
        /// 道岔ID
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 道岔所在位置点
        /// </summary>
        [JsonIgnore]
        public HPosition? BindingPosition { get; set; }

        public string? BindingPositionID { get; set; }

        /// <summary>
        /// 道岔所在位置区间（仅对菱形交叉生效）
        /// </summary>
        [JsonIgnore]
        public HPositionSegment? BindingPositionSegment { get; set; }

        public string? BindingPositionSegmentID { get; set; }

        /// <summary>
        /// 曲线转角（车辆溜放方向）/°
        /// </summary>
        public double CurveDegree { get; set; }

        /// <summary>
        /// 道岔种类
        /// </summary>
        public SwitchTypes Type { get; set; }

        /// <summary>
        /// 道岔方向（顺向或逆向）
        /// </summary>
        public SwitchDirections Direction { get; set; }

        /// <summary>
        /// 道岔开向（左开或右开）
        /// </summary>
        public SwitchSides Side { get; set; }
    }

    /// <summary>
    /// 减速器
    /// </summary>
    public class Retarder
    {
        /// <summary>
        /// 减速器ID
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 减速器所在的位置区间
        /// </summary>
        [JsonIgnore]
        public HPositionSegment? BindingPositionSegment { get; set; }

        public string BindingPositionSegmentID { get; set; }

        /// <summary>
        /// 减速器数量配置
        /// </summary>
        public int[] NumberArray { get; set; }

        [JsonIgnore]
        public string Numbers
        {
            get
            {
                return NumberArray != null ? string.Join("+", NumberArray) : string.Empty;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    NumberArray = Array.Empty<int>();
                }
                else
                {
                    var strArray = value.Split('+');
                    NumberArray = strArray.Select(s => int.Parse(s)).ToArray();
                }
            }
        }

        /// <summary>
        /// 最大制动力/kN
        /// </summary>
        /// <returns></returns>
        public double GetMaxBreakingForce()
        {
            return 0.0;
        }
    }

    /// <summary>
    /// 道岔数量统计
    /// </summary>
    public class SwitchCount
    {
        /// <summary>
        /// 逆向道岔数量
        /// </summary>
        public int ReverseCount { get; set; }

        /// <summary>
        /// 顺向道岔数量
        /// </summary>
        public int ForwardCound { get; set; }

        /// <summary>
        /// 交分道岔数量
        /// </summary>
        public int SlipCount { get; set; }

        /// <summary>
        /// 菱形交叉数量
        /// </summary>
        public int DiamondCount { get; set; }

        /// <summary>
        /// 车辆溜放方向的曲线转角角度总和/°
        /// </summary>
        public double TotalCurveDegree { get; set; }
    }

    /// <summary>
    /// 道岔种类
    /// </summary>
    public enum SwitchTypes
    {
        /// <summary>
        /// 单开
        /// </summary>
        Single,

        /// <summary>
        /// 交分
        /// </summary>
        Slip,

        /// <summary>
        /// 菱形
        /// </summary>
        Diamond,
        None
    }

    /// <summary>
    /// 道岔方向
    /// </summary>
    public enum SwitchDirections
    {
        /// <summary>
        /// 逆向
        /// </summary>
        Reverse,

        /// <summary>
        /// 顺向
        /// </summary>
        Forward,
        None
    }

    /// <summary>
    /// 道岔开向
    /// </summary>
    public enum SwitchSides
    {
        /// <summary>
        /// 左开
        /// </summary>
        Left,

        /// <summary>
        /// 右开
        /// </summary>
        Right,
        None
    }

    /// <summary>
    /// 曲线方向
    /// </summary>
    public enum CurveDirections
    {
        /// <summary>
        /// 左转
        /// </summary>
        Left,

        /// <summary>
        /// 右转
        /// </summary>
        Right,
        None
    }
}
