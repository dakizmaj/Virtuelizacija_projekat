using Common;
using System;
using System.Collections.Generic;
using System.IO;
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
            var binding = new NetTcpBinding();
            var endpoint = new EndpointAddress("net.tcp://localhost:9000/BatteryService");

            var factory = new ChannelFactory<IBatteryService>(binding, endpoint);
            var proxy = factory.CreateChannel();

            string basePath = @".\Dataset"; /// ovde staviti putanju do svog dataset foldera
            var logPath = Path.Combine(basePath, "log.txt");

            foreach(var file in Directory.GetFiles(basePath, "*.csv", SearchOption.AllDirectories))
            {
                try
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);

                    string[] parts = fileName.Split(new[] { "_SoC_" }, StringSplitOptions.None);
                    string batteryId = parts[0];

                    string[] socAndTest = parts[1].Split('_');
                    int soc = int.Parse(socAndTest[0]);

                    string testId = socAndTest.Length > 1 ? socAndTest[1] : "Test";

                    var samples = CsvReader.ReadSamples(file).ToList();

                    // Start session
                    var meta = new EisMeta
                    {
                        BatteryID = batteryId,
                        TestID = testId,
                        SoC = soc,
                        FileName = Path.GetFileName(file),
                        TotalRows = samples.Count
                    };

                    var start = proxy.StartSession(meta);
                    Console.WriteLine($"StartSession: {start.Message}");

                    // Slanje uzoraka
                    foreach (var sample in samples)
                    {
                        var push = proxy.PushSample(sample);
                        Console.WriteLine($"PushSample: {push.Message}");
                    }

                    // End session
                    var end = proxy.EndSession();
                    Console.WriteLine($"EndSession: {end.Message}");

                    // Ako broj redova nije 28 -> log
                    if (samples.Count != 28)
                    {
                        File.AppendAllText(logPath, $"{file}: ocekivano 28 redova, pronadjeno {samples.Count}\n");
                    }
                }
                catch (FaultException<DataFormatFault> ex)
                {
                    File.AppendAllText(logPath, $"{file}: DataFormatFault - {ex.Detail.Message}\n");
                }
                catch (FaultException<ValidationFault> ex)
                {
                    File.AppendAllText(logPath, $"{file}: ValidationFault - {ex.Detail.Message}\n");
                }
                catch (FaultException ex)
                {
                    File.AppendAllText(logPath, $"{file}: FaultException - {ex.Message}\n");
                }
                catch (Exception ex)
                {
                    File.AppendAllText(logPath, $"{file}: Exception - {ex.Message}\n");
                }
            }
            

            Console.WriteLine("GOTOVO, pritisni ENTER za kraj...");
            Console.ReadLine();

        }
    }
}
