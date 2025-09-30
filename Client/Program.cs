using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            var binding = new NetTcpBinding(SecurityMode.None);
            var endpoint = new EndpointAddress("net.tcp://localhost:9000/BatteryService");

            var factory = new ChannelFactory<IBatteryService>(binding, endpoint);
            var proxy = factory.CreateChannel();

            try
            {
                //Start session
                var meta = new EisMeta
                {
                    BatteryID = "B01",
                    TestID = "Test_1",
                    SoC = 20,
                    FileName = "20.csv",
                    TotalRows = 28
                };
                var start = proxy.StartSession(meta);
                Console.WriteLine($"StartSession: {start.Message}");

                var goodSample = new EisSample
                {
                    FrequencyHz = 1000,
                    R_Ohm = 0.01,
                    X_Ohm = 0.002,
                    V = 3.7,
                    T_degC = 25,
                    Range_ohm = 1,
                    RowIndex = 1
                };

                var push = proxy.PushSample(goodSample);
                Console.WriteLine($"PushSample: {push.Message}");

                // Nevalidan sample (namerno greska: FrequencyHz <0)
                var badSample = new EisSample
                {
                    FrequencyHz = -1,
                    R_Ohm = 0.01,
                    X_Ohm = 0.002,
                    V = 3.7,
                    T_degC = 25,
                    Range_ohm = 1,
                    RowIndex = 2
                };
                var pushBad = proxy.PushSample(badSample);
                Console.WriteLine($"PushSample: {pushBad.Message}");

                // End Session
                var end = proxy.EndSession();
                Console.WriteLine($"EndSession: {end.Message}");
            }
            catch (FaultException<DataFormatFault> ex)
            {
                Console.WriteLine($"DataFormatFault: {ex.Detail.Message}");
            }
            catch (FaultException<ValidationFault> ex)
            {
                Console.WriteLine($"ValidationFault: {ex.Detail.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Neocekivana greska: {ex.Message}");
            }

            Console.WriteLine("GOTOVO, pritisni ENTER za kraj...");
            Console.ReadLine();

        }
    }
}
