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

        public OperationStatus StartSession(EisMeta meta)
        {
            if (meta == null || string.IsNullOrEmpty(meta.BatteryID) || string.IsNullOrEmpty(meta.TestID))
                throw new FaultException<DataFormatFault>(
                    new DataFormatFault { Message = "Meta podaci nisu ispravni."},
                    new FaultReason("Invalid meta data."));

            currentSession = meta;
            recivedRows = 0;
            lastRowIndex = 0;

            string folderPath = Path.Combine("Data", meta.BatteryID, meta.TestID, $"{meta.SoC}");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filePath = Path.Combine(folderPath, "session.csv");
            fileWriter = new FileWriter(filePath);

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
                throw new FaultException<DataFormatFault>(
                    new DataFormatFault { Message = "Sample je null."},
                    new FaultReason("Invalid sample."));
            if (sample.FrequencyHz <= 0)
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Message = "FrequencyHz mora biti > 0." },
                    new FaultReason("Validation failed."));

            if (double.IsNaN(sample.R_Ohm)|| double.IsInfinity(sample.R_Ohm) ||
                double.IsNaN(sample.X_Ohm)|| double.IsInfinity(sample.X_Ohm) ||
                double.IsNaN(sample.V) || double.IsInfinity(sample.V))
            {
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Message = "R, X ili V nisu validne vrednosti."},
                    new FaultReason("Validation failed."));
            }

            if (sample.RowIndex <= lastRowIndex)
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Message = "RowIndex mora monotono rasti."},
                    new FaultReason("Validation failed."));

            lastRowIndex = sample.RowIndex;
            recivedRows++;

            string line = $"{sample.RowIndex},{sample.FrequencyHz},{sample.R_Ohm},{sample.X_Ohm},{sample.V},{sample.T_degC},{sample.Range_ohm}";
            fileWriter?.WriteLine(line);

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

            var msg = $"Session completed. Recived {recivedRows}/{currentSession.TotalRows} rows.";

            return new OperationStatus
            {
                Success = true,
                Message = msg,
                Status = "COMPLETED"
            };
        }
    }
}
