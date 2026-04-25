using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SwitchYard.Hump
{
    /// <summary>
    /// 驼峰检算方案
    /// </summary>
    public class HeadwayCheckScheme
    {
        /// <summary>
        /// 驼峰实例ID
        /// </summary>
        public string InstanceID { get; set; }

        /// <summary>
        /// 驼峰检算方案ID
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 驼峰方案ID
        /// </summary>
        public string HumpSchemeID { get; set; }

        /// <summary>
        /// 驼峰检算方案名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 溜放车辆列表
        /// </summary>
        public List<HeadwayCheckWagon> WagonList { get; set; }

        /// <summary>
        /// 推峰速度
        /// </summary>
        public double WagonVelocityOnTop { get; set; }

        /// <summary>
        /// 溜放线ID
        /// </summary>
        public string SlopeLineID { get; set; }
    }

    /// <summary>
    /// 驼峰检算车辆信息
    /// </summary>
    public class HeadwayCheckWagon
    {
        /// <summary>
        /// 溜放顺序
        /// </summary>
        public int Sequence { get; set; }

        /// <summary>
        /// 驼峰计算ID
        /// </summary>
        public string HumpCalculationID { get; set; }

        /// <summary>
        /// 驼峰计算实例
        /// </summary>
        [JsonIgnore]
        public HumpCalculation? HumpCalculation { get; set; }

        /// <summary>
        /// 能高计算参数
        /// </summary>
        [JsonIgnore]
        public EnergyCalculationParams? EnergyCalculationParams { get; set; }
    }

    /// <summary>
    /// 驼峰检算器
    /// </summary>
    public static class HeadwayChecker
    {
        private static List<HeadwayCheckPoint> GenerateCheckPointList(FlatLayout flatLayout)
        {
            var checkPointList = new List<HeadwayCheckPoint>();
            // 生成检算点

            // 峰顶
            HeadwayCheckPoint humpTop = new HeadwayCheckPoint
            {
                StartPosition = new HPosition() { X = flatLayout.PositionList.FirstOrDefault()?.X??0 },
                EndPosition = new HPosition() { X = flatLayout.PositionList.FirstOrDefault()?.X ?? 0 },
                Type = CheckPointTypes.Top,
                EquipmentID = "Top",
            };
            checkPointList.Add(humpTop);

            foreach (var sw in flatLayout.SwitchList)
            {
                // 生成道岔检算点
                var p = sw.BindingPosition;

                SwitchCheckPoint switchCheckPoint = new SwitchCheckPoint
                {
                    StartPosition = new HPosition() { X = p.X - 10 },
                    EndPosition = new HPosition() { X = p.X + 10 },
                    Type = CheckPointTypes.Switch,
                    EquipmentID = sw.ID,
                    Switch = sw
                };
                checkPointList.Add(switchCheckPoint);
            }

            foreach (var retarder in flatLayout.RetarderList)
            {
                // 生成减速器检算点
                var p1 = (HPosition)retarder.BindingPositionSegment.StartPosition;
                var p2 = (HPosition)retarder.BindingPositionSegment.EndPosition;

                RetarderCheckPoint retarderCheckPoint = new RetarderCheckPoint
                {
                    StartPosition = p1,
                    EndPosition = p2,
                    Type = CheckPointTypes.Retarder,
                    EquipmentID = retarder.ID,
                    Retarder = retarder
                };
                checkPointList.Add(retarderCheckPoint);
            }

            // 计算点
            HeadwayCheckPoint humpEnd = new HeadwayCheckPoint
            {
                StartPosition = new HPosition() { X = flatLayout.PositionList.LastOrDefault()?.X ?? 0 },
                EndPosition = new HPosition() { X = flatLayout.PositionList.LastOrDefault()?.X ?? 0 },
                Type = CheckPointTypes.End,
                EquipmentID = "End",
            };
            checkPointList.Add(humpEnd);

            return checkPointList;
        }

        /// <summary>
        /// 生成驼峰检算运行时间数据
        /// </summary>
        /// <param name="scheme"></param>
        /// <returns></returns>
        public static HeadwayCheckRunningTime CalculateRunningTime(HeadwayCheckScheme scheme, FlatLayout flatLayout, SlopeLayout slopeLayout)
        {
            // 生成检算点列表
            var checkPointList = GenerateCheckPointList(flatLayout);

            var data = new HeadwayCheckRunningTime()
            {
                InstanceID = scheme.InstanceID,
                HeadwayCheckSchemeID = scheme.ID,
                HeadwayCheckPoints = checkPointList
            };

            data.CheckPointDatas = new List<HeadwayCheckRunningTimeData>();

            var distanceToFisrtWagon = 0.0;
            foreach (var hcWagon in scheme.WagonList)
            {   
                distanceToFisrtWagon += hcWagon.EnergyCalculationParams.Wagon?.Length / 2 ?? 0;
                var rollingStartTime = distanceToFisrtWagon / scheme.WagonVelocityOnTop;
                distanceToFisrtWagon += hcWagon.EnergyCalculationParams.Wagon?.Length / 2 ?? 0;

                foreach (var cp in checkPointList)
                {
                    // 计算速度曲线
                    var speedProfile = SpeedProfileGenerator.Generate(hcWagon, flatLayout, slopeLayout);

                    // 计算至检算点的时间
                    var enterTime = CalculateCumulativeTime(speedProfile, cp.StartX);
                    var exitTime = CalculateCumulativeTime(speedProfile, cp.EndX);

                    // 若车辆在到达检算点之前已停止（速度为 0），则跳过该检算点，
                    // 后续位置-时间曲线不再绘制。
                    if (double.IsNaN(enterTime) || double.IsInfinity(enterTime) ||
                        double.IsNaN(exitTime) || double.IsInfinity(exitTime))
                    {
                        break;
                    }

                    enterTime += rollingStartTime;
                    exitTime += rollingStartTime;

                    HeadwayCheckRunningTimeData cpData = new HeadwayCheckRunningTimeData()
                    {
                        HeadwayCheckWagon = hcWagon,
                        HeadwayCheckPoint = cp,
                        EnterTime = enterTime,
                        ExitTime = exitTime
                    };
                    data.CheckPointDatas.Add(cpData);
                }
            }

            return data;
        }

        private static double CalculateCumulativeTime(HeadwayCheckWagonSpeedProfile speedProfile, double x)
        {
            double cumulativeTime = 0;
            int count = speedProfile.PositionList.Count;
            if (count == 0)
            {
                return double.NaN;
            }

            // 若目标位置超出速度曲线终点（速度为 0 后曲线会被截断），表示车辆无法到达该位置
            if (x > speedProfile.PositionList[count - 1] + 1e-9)
            {
                return double.NaN;
            }

            for (int i = 0; i < count - 1; i++)
            {
                var pos0 = speedProfile.PositionList[i];
                var v0 = speedProfile.SpeedList[i];

                var pos1 = speedProfile.PositionList[i + 1];
                var v1 = speedProfile.SpeedList[i + 1];

                if (pos0 > x)
                    break;

                var sumV = v0 + v1;
                if (sumV <= 0)
                {
                    // 车辆已停止，无法继续向前推进
                    return double.NaN;
                }

                var duration = 2 * (pos1 - pos0) / sumV;  // 平均速度法计算时间
                if (x > pos0 && x < pos1)
                {
                    duration = (x - pos0) / (pos1 - pos0) * duration;
                }
                cumulativeTime += duration;
            }
            return cumulativeTime;
        }

        /// <summary>
        /// 返回驼峰检算结果
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static HeadwayCheckResult GetHeadwayCheckResult(HeadwayCheckRunningTime data, FlatLayout flatLayout)
        {
            return new HeadwayCheckResult();
        }
    }

    public class HeadwayCheckRunningTime
    {
        /// <summary>
        /// 驼峰实例ID
        /// </summary>
        public string InstanceID { get; set; }

        /// <summary>
        /// 驼峰检算方案ID
        /// </summary>
        public string HeadwayCheckSchemeID { get; set; }

        /// <summary>
        /// 检算数据的字典，键为HeadwayCheckPoint，值为检算点数据列表
        /// </summary>
        [JsonIgnore]
        public List<HeadwayCheckRunningTimeData> CheckPointDatas { get; set; }

        [JsonIgnore]
        public Dictionary<HeadwayCheckWagon, List<HeadwayCheckRunningTimeData>> CheckPointDataDict
        {
            get
            {
                return CheckPointDatas.GroupBy(d => d.HeadwayCheckWagon)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
        }

        public List<HeadwayCheckWagonRunningTime> RunningTimes
        {
            get
            {
                return CheckPointDatas
                    .GroupBy(d => d.HeadwayCheckWagon.Sequence)
                    .Select(g =>
                    {
                        var wagon = g.First().HeadwayCheckWagon;
                        var sortedData = g.OrderBy(d => d.HeadwayCheckPoint.StartX).ToList();

                        var positionList = new List<double>();
                        var runningTimeList = new List<double>();

                        foreach (var data in sortedData)
                        {
                            positionList.Add(data.HeadwayCheckPoint.StartX);
                            positionList.Add(data.HeadwayCheckPoint.EndX);
                            runningTimeList.Add(data.EnterTime);
                            runningTimeList.Add(data.ExitTime);
                        }

                        return new HeadwayCheckWagonRunningTime
                        {
                            Wagon = wagon,
                            PositionList = positionList,
                            RunningTimeList = runningTimeList
                        };
                    })
                    .OrderBy(rt => rt.Wagon.Sequence)
                    .ToList();
            }
        }

        /// <summary>
        /// 检算点列表
        /// </summary>
        [JsonIgnore]
        public List<HeadwayCheckPoint>? HeadwayCheckPoints { get; set; }
    }

    public class HeadwayCheckWagonRunningTime
    {
        public HeadwayCheckWagon Wagon { get; set; }
        public List<double> PositionList { get; set; } = new List<double>();
        public List<double> RunningTimeList { get; set; } = new List<double>();
    }

    public class HeadwayCheckRunningTimeData
    {
        /// <summary>
        /// 检算车辆
        /// </summary>
        public HeadwayCheckWagon HeadwayCheckWagon { get; set; }

        /// <summary>
        /// 检算点
        /// </summary>
        public HeadwayCheckPoint HeadwayCheckPoint { get; set; }

        /// <summary>
        /// 入口时间/s
        /// </summary>
        public double EnterTime { get; set; }

        /// <summary>
        /// 出口时间/s
        /// </summary>
        public double ExitTime { get; set; }
    }

    public class HeadwayCheckPoint
    {
        /// <summary>
        /// 检算点起点
        /// </summary>
        [JsonIgnore]
        public HPosition StartPosition { get; set; }

        /// <summary>
        /// 检算点终点
        /// </summary>
        [JsonIgnore]
        public HPosition EndPosition { get; set; }

        public double StartX
        {
            get
            {
                return StartPosition.X;
            }
        }

        public double EndX
        {
            get
            {
                return EndPosition.X;
            }
        }

        /// <summary>
        /// 检算点种类
        /// </summary>
        public CheckPointTypes Type { get; set; }

        /// <summary>
        /// 设备ID（道岔ID或减速器ID）
        /// </summary>
        public virtual string EquipmentID { get; set; }
    }

    public class RetarderCheckPoint : HeadwayCheckPoint
    {
        /// <summary>
        /// 减速器ID
        /// </summary>
        public override string EquipmentID
        {
            get => retarderID;
            set => retarderID = value;
        }

        private string retarderID { get; set; }

        internal Retarder Retarder { get; set; }
    }

    public class SwitchCheckPoint : HeadwayCheckPoint
    {
        /// <summary>
        /// 道岔ID
        /// </summary>
        public override string EquipmentID
        {
            get => switchID;
            set => switchID = value;
        }

        private string switchID { get; set; }

        internal Switch Switch { get; set; }
    }

    /// <summary>
    /// 检算点种类
    /// </summary>
    public enum CheckPointTypes
    {
        /// <summary>
        /// 道岔
        /// </summary>
        Switch,

        /// <summary>
        /// 减速器
        /// </summary>
        Retarder,

        /// <summary>
        /// 峰顶
        /// </summary>
        Top,

        /// <summary>
        /// 难行线计算点
        /// </summary>
        End
    }

    public class HeadwayCheckResult
    {
        /// <summary>
        /// 溜放顺序
        /// </summary>
        public int Sequence { get; set; }
        /// <summary>
        /// 车辆ID
        /// </summary>
        public string WagonID { get; set; }
        /// <summary>
        /// 车辆溜放的单位基本阻力值/(N/kN)
        /// </summary>
        public double PureResistance { get; set; }
        /// <summary>
        /// 车辆溜放的单位风阻力值/(N/kN)
        /// </summary>
        public double AirResistance { get; set; }
        /// <summary>
        /// 车辆溜放的总阻力值/(N/kN)
        /// </summary>
        public double TotalResistance { get; set; }
    }
}
