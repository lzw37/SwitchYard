using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchYard.Hump
{
    public class HumpCalculation
    {
        public string InstanceID { get; set; }

        public string HumpSchemeID { get; set; }

        public string ID { get; set; }

        public string WagonType { get; set; }

        public string OperationConditionID { get; set; }

        public string SlopeLineID { get; set; }

        public HumpCalculationData? Data { get; set; }
    }

    public class HumpCalculationData
    {

    }
}
