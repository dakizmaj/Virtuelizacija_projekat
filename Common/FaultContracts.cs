using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [DataContract]
    public class DataFormatFault
    {
        [DataMember] public string Message { get; set; }
        public DataFormatFault() { }
        public DataFormatFault(string message)
        {
            Message = message;
        }
    }

    [DataContract]
    public class ValidationFault
    {
        [DataMember] public string Message { get; set; }
        public ValidationFault() { }
        public ValidationFault(string message)
        {
            Message = message;
        }
    }
}
