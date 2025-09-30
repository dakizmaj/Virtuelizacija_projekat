using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.IO;

namespace Common
{
    public class EisSample
    {
        [DataContract]
        public double FrequencyHz { get; set; }
        [DataContract]
        public double R_Ohm { get; set; }
        [DataContract]
        public double X_Ohm { get; set; }
        [DataContract]
        public double V { get; set; }
        [DataContract]
        public double T_degC { get; set; }
        [DataContract]
        public double Range_Ohm { get; set; }
        [DataContract]
        public ulong RowIndex { get; set; }
    }

    public class EisMeta
    {
        [DataContract]
        public string BatteryId { get; private set; }
        [DataContract]
        public string TestId { get; private set; }
        [DataContract]
        public string StateOfChargePercentage { get; private set; }
        [DataContract]
        public string Filename { get; private set; }
        [DataContract]
        public ulong TotalRows { get; private set; }
    }
}
