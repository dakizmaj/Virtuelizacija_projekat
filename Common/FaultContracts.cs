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
        string message;
        [DataMember] public string Message
        {
            get => message;
            set => message = value;
        }
        public DataFormatFault()
        {
            Message = string.Empty;
        }
        public DataFormatFault(string message)
        {
            Message = message;
        }
    }

    [DataContract]
    public class ValidationFault
    {
        string message;
        [DataMember] public string Message
        {
            get => message;
            set => message = value;
        }
        public ValidationFault()
        {
            Message = string.Empty;
        }
        public ValidationFault(string message)
        {
            Message = message;
        }
    }
}
