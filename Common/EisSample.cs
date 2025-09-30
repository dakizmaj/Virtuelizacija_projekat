using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [DataContract]
    public class EisSample
    {
        [DataMember] public double FrequencyHz { get; set; }
        [DataMember] public double R_Ohm { get; set; }
        [DataMember] public double X_Ohm { get; set; }
        [DataMember] public double V { get; set; }
        [DataMember] public double T_degC { get; set; }
        [DataMember] public double Range_ohm { get; set; }
        [DataMember] public int RowIndex { get; set; }
    }
}
