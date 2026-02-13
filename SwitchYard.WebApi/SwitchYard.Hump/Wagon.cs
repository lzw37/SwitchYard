using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SwitchYard.Hump
{
    /// <summary>
    /// 车辆类型
    /// </summary>
    public class WagonConcept
    {
        /// <summary>
        /// 车型
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// 长度
        /// </summary>
        public double Length { get; set; }

        /// <summary>
        /// 车辆自重/t
        /// </summary>
        public double NetMass { get; set; }

        /// <summary>
        /// 车辆载重/t
        /// </summary>
        public double LoadingMass { get; set; }

        /// <summary>
        /// 车辆计算总重量/t
        /// </summary>
        public double GrossMass =>  NetMass + LoadingMass;

        /// <summary>
        /// 车辆正面迎风面积/m²
        /// </summary>
        public double WindwardArea { get; set; }

        /// <summary>
        /// 车辆轴数
        /// </summary>
        public int AxleNumber { get; set; }

        /// <summary>
        /// 计算车型标签（难行车、中行车、易行车）
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// 车型计算参数
        /// </summary>
        public int CarTypeParam 
        { 
            get
            { 
                switch (Label)
                {
                    case "难行车":
                        return 1;
                    case "中行车":
                        return 0;
                    case "易行车":
                        return -1;
                    default:
                        throw new ApplicationException("车辆标签有误：" + Label);
                }
            } 
        }

        /// <summary>
        /// 自由落体重力加速度/(m/s²)
        /// </summary>
        public double g { get; set; } = 9.8;

        /// <summary>
        /// 考虑了车轮转动惯量的重力加速度/(m/s²)
        /// </summary>
        [JsonIgnore]
        public double g_
        {
            get
            {
                if (this == null)
                {
                    return this.g;
                }
                else
                {
                    return (this.g / (1 + 0.42 * this.AxleNumber / this.GrossMass));
                }
            }
        }
    }
}
