using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchYard.Hump
{
    /// <summary>
    /// 驼峰从峰顶至某调车线计算点的平面布置（展开）图
    /// </summary>
    public class FlatLayout
    {
        /// <summary>
        /// 位置点列表
        /// </summary>
        public List<Position> PositionList { get; set; }

        /// <summary>
        /// 位置区间列表
        /// </summary>
        public List<PositionSegment> PositionSegmentList { get; set; }

        /// <summary>
        /// 道岔列表
        /// </summary>
        public List<Switch> SwitchList { get; set; }

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
                ForwardCound = switches.Sum(s => { 
                    return (s.Type == SwitchTypes.Single && s.Direction == SwitchDirections.Forward) ? 1 : 0; }),
                
                ReverseCount = switches.Sum(s => { 
                    return (s.Type == SwitchTypes.Single && s.Direction == SwitchDirections.Reverse) ? 1 : 0; }),
                
                SlipCount = switches.Sum(s => { 
                    return (s.Type == SwitchTypes.Slip) ? 1 : 0; }),
                
                DiamondCount = diamonds.Sum(s => { 
                    return s.Type == SwitchTypes.Diamond ? 1 : 0; })
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

            var sum = curveCorners.Sum(segment => segment.CurveCorner);
            var firstSegment = curveCorners.First();
            var lastSegment = curveCorners.Last();

            var startLengthError = startX - firstSegment.StartPosition.X;
            var endLengthError = lastSegment.EndPosition.X - endX;

            var startCornerError = firstSegment.CurveCorner * (startLengthError / firstSegment.Length);
            var endCornerError = lastSegment.CurveCorner * (endLengthError / lastSegment.Length);

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
    /// 位置点
    /// </summary>
    public class Position
    {
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
    /// 位置区间
    /// </summary>
    public class PositionSegment
    {
        /// <summary>
        /// 起始位置点
        /// </summary>
        public Position StartPosition { get; set; }

        /// <summary>
        /// 终止位置点
        /// </summary>
        public Position EndPosition { get; set; }

        /// <summary>
        /// 长度/m
        /// </summary>
        public double Length { get; set; }

        /// <summary>
        /// 转角总度数/°
        /// </summary>
        public double CurveCorner { get; set; }

        /// <summary>
        /// 位置参数（1:调车场,0:溜放部分）
        /// </summary>
        public int LocationParam { get; set; }
    }

    /// <summary>
    /// 道岔
    /// </summary>
    public class Switch
    {
        /// <summary>
        /// 道岔所在位置点
        /// </summary>
        public Position BindingPosition { get; set; }

        /// <summary>
        /// 道岔所在位置区间（仅对菱形交叉生效）
        /// </summary>
        public PositionSegment BindingPositionSegment { get; set; }

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
        /// 减速器所在的位置区间
        /// </summary>
        public PositionSegment BindingPositionSegment { get; set; }

        /// <summary>
        /// 减速器数量配置
        /// </summary>
        public int[] Numbers { get; set; }

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
        Diamond
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
        Forward
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
        Right
    }
}
