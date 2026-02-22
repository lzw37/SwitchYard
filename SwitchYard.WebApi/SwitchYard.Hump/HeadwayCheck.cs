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
        /// <summary>
        /// 生成驼峰检算数据
        /// </summary>
        /// <param name="scheme"></param>
        /// <returns></returns>
        public static HeadwayCheckData GetHeadwayCheckData(HeadwayCheckScheme scheme, FlatLayout flatLayout, SlopeLayout slopeLayout)
        {
            var checkPointList = new List<HeadwayCheckPoint>();
            // 生成检算点
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

            var data = new HeadwayCheckData()
            {
                InstanceID = scheme.InstanceID,
                HeadwayCheckSchemeID = scheme.ID,
                HeadwayCheckPoints = checkPointList
            };

            data.CheckPointDatas = new List<HeadwayCheckPointData>();
            foreach (var cp in checkPointList)
            {
                foreach (var hcWagon in scheme.WagonList)
                {
                    
                }
            }

            return data;
        }

        /// <summary>
        /// 返回驼峰检算结果
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static HeadwayCheckResult GetHeadwayCheckResult(HeadwayCheckData data, FlatLayout flatLayout)
        {
            return new HeadwayCheckResult();
        }
    }

    public class HeadwayCheckData
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
        public List<HeadwayCheckPointData> CheckPointDatas { get; set; }

        /// <summary>
        /// 检算点列表
        /// </summary>
        [JsonIgnore]
        public List<HeadwayCheckPoint> HeadwayCheckPoints { get; set; }
    }

    public class HeadwayCheckPointData
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

    public abstract class HeadwayCheckPoint
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
        Retarder
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
