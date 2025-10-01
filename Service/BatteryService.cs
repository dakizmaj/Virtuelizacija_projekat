using Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;


namespace Service
{
    public class BatteryService : IBatteryService
    {
        private EisMeta currentSession;
        private int recivedRows = 0;
        private int lastRowIndex = 0;
        public FileWriter fileWriter;
        private FileWriter rejectsWriter;
        private readonly string logFile = Path.Combine("Data", "server.log");

        public OperationStatus StartSession(EisMeta meta)
        {
            if (meta == null || string.IsNullOrEmpty(meta.BatteryID) || string.IsNullOrEmpty(meta.TestID))
                throw new FaultException<DataFormatFault>(
                    new DataFormatFault("Meta podaci nisu ispravni."),
                    new FaultReason("Invalid meta data."));

            currentSession = meta;
            recivedRows = 0;
            lastRowIndex = 0;

            string folderPath = Path.Combine("Data", meta.BatteryID, meta.TestID, $"{meta.SoC}");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            //session.csv
            string filePath = Path.Combine(folderPath, "session.csv");
            fileWriter = new FileWriter(filePath);

            //rejects.csv
            string rejectsPath = Path.Combine(folderPath, "rejects.csv");
            rejectsWriter = new FileWriter(rejectsPath);

            WriteLog($"Session STARTED for Battery={meta.BatteryID}, Test={meta.TestID}, SoC={meta.SoC}");

            return new OperationStatus
            {
                Success = true,
                Message = $"Session started for {meta.BatteryID}/{meta.TestID}, SoC={meta.SoC}%",
                Status = "IN_PROGRESS"
            };
        }

        public OperationStatus PushSample(EisSample sample)
        {
            if (sample == null)
            {
                WriteReject("Sample je null.");
                throw new FaultException<DataFormatFault>(
                    new DataFormatFault("Sample je null."),
                    new FaultReason("Invalid sample.")
                );
            }
                if (sample.FrequencyHz <= 0)
                {
                    WriteReject($"Nevalidna frekvencija: {sample.FrequencyHz}");
                    throw new FaultException<ValidationFault>(
                        new ValidationFault("FrequencyHz mora biti > 0."),
                        new FaultReason("Validation failed.")
                    );
                }
            bool isResistanceOk = double.IsNaN(sample.R_Ohm) || double.IsInfinity(sample.R_Ohm);
            bool isImpendanceOk = double.IsNaN(sample.X_Ohm) || double.IsInfinity(sample.X_Ohm);
            bool isVoltageOk = double.IsNaN(sample.V) || double.IsInfinity(sample.V);
            if (isResistanceOk || isImpendanceOk || isVoltageOk)
            {
                WriteReject($"Nevalidne vrednosti R:{sample.R_Ohm}, X:{sample.X_Ohm}, V:{sample.V}");
                throw new FaultException<ValidationFault>(
                    new ValidationFault("R, X ili V nisu validne vrednosti."),
                    new FaultReason("Validation failed.")
                );
            }

                if (sample.RowIndex <= lastRowIndex)
                {
                    WriteReject($"Nevalidan RowIndex: {sample.RowIndex}, poslednji: {lastRowIndex}");
                    throw new FaultException<ValidationFault>(
                        new ValidationFault("RowIndex mora monotono rasti."),
                        new FaultReason("Validation failed.")
                    );
                }
            lastRowIndex = sample.RowIndex;
            recivedRows++;

            string line = $"{sample.RowIndex};{sample.FrequencyHz};{sample.R_Ohm};{sample.X_Ohm};{sample.V};{sample.T_degC};{sample.Range_ohm}";
            fileWriter?.WriteLine(line);

            WriteLog($"Sample {sample.RowIndex} ACCEPTED (Battery={currentSession.BatteryID}, Test={currentSession.TestID})");

            return new OperationStatus
            {
                Success = true,
                Message = $"Sample {sample.RowIndex} recived.",
                Status = "IN_PROGRESS"
            };
        }
        public OperationStatus EndSession()
        {
            if (currentSession == null)
                throw new FaultException<DataFormatFault>(
                    new DataFormatFault { Message = "Session nije zapocet." },
                    new FaultReason("EndSession called before StartSession."));

            fileWriter?.Dispose();
            fileWriter = null;

            rejectsWriter?.Dispose();
            rejectsWriter = null;

            var msg = $"Session completed. Recived {recivedRows}/{currentSession.TotalRows} rows.";

            WriteLog($"Session ENDED for Battery={currentSession.BatteryID}, Test={currentSession.TestID}, SoC={currentSession.SoC}.");


            return new OperationStatus
            {
                Success = true,
                Message = msg,
                Status = "COMPLETED"
            };
        }
        private void WriteReject(string reason)
        {
            string line = $"[{DateTime.UtcNow}] {reason}";
            rejectsWriter?.WriteLine(line);
        }
        private void WriteLog(string message)
        {
            string line = $"[{DateTime.UtcNow}] {message}";
            File.AppendAllText(logFile, line + Environment.NewLine);
            Console.WriteLine(line);
        }
    }
}
