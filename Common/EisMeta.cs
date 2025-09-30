using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [DataContract]
    public class EisMeta
    {
        [DataMember] public string BatteryID { get; set; }
        [DataMember] public string TestID { get; set; }
        [DataMember] public int SoC { get; set; }//procenat iz naziva fajla
        [DataMember] public string FileName { get; set; }
        [DataMember] public int TotalRows { get; set; }
    }
}
