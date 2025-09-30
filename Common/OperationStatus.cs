using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [DataContract]
    public class OperationStatus
    {
        [DataMember] public bool Success { get; set; }
        [DataMember] public string Message { get; set; }
        [DataMember] public string Status { get; set; } // IN_PROGRESS / COMPLETED
    }
}
