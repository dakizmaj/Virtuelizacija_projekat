using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Virtuelizacija_projekat.Common;

namespace Service
{
    [ServiceContract]
    public static class SessionManager
    {
        [OperationContract]
        public static void StartSession(EisMeta meta)
        {
            //
        }
        
        [OperationContract]
        public static void PushSample(EisSample sample)
        {
            //
        }
        
        [OperationContract]
        public static void EndSession()
        {
            //
        }
    }
}
