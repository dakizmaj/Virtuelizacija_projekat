using Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;


namespace Service
{
    public class TransferEventArgs : EventArgs
    {
        public EisMeta Meta { get; set; }
    }

    public class SampleEventArgs : EventArgs
    {
        public EisSample Sample { get; set; }
        public int RecivedRows { get; set; }
    }
    public class WarningEventArgs : EventArgs
    {
        public string WarningType { get; set; }//VoltageSpike, ImpendanceJump,OutOfBand,Reject
        public string Message { get; set; }
        public EisSample Sample { get; set; }
    }
    public class BatteryService : IBatteryService
    {
        //Eventi
        public event EventHandler<TransferEventArgs> OnTransferStarted;
        public event EventHandler<SampleEventArgs> OnSampleRecived;
        public event EventHandler<TransferEventArgs> OnTransferCompleted;
        public event EventHandler<WarningEventArgs> OnWarningRaised;

        //Session state
        private EisMeta currentSession;
        private int recivedRows = 0;
        private int lastRowIndex = 0;
        public FileWriter fileWriter;
        private FileWriter rejectsWriter;

        //running values for analytic checks
        private double lastV = double.NaN;
        private double lastZ = double.NaN;
        private double runningSumZ = 0.0;
        private int runningCountZ = 0;

        //thresholds (iz app.config)
        private readonly double V_threshold;
        private readonly double Z_threshold;
        private readonly double PercentThreshold; //0.25 = 25%

        // global server log
        private readonly string logFile = Path.Combine("Data", "server.log");

        public BatteryService()
        {
            //Read thresholds from config Defaults provided
            double vthr = 0.05, zthr = 0.01, pct = 0.25;
            double.TryParse(ConfigurationManager.AppSettings["V_Threshold"], out vthr);
            double.TryParse(ConfigurationManager.AppSettings["Z_Threshold"], out zthr);
            double.TryParse(ConfigurationManager.AppSettings["PercentThreshold"], out pct);

            V_threshold = vthr;
            Z_threshold = zthr;
            PercentThreshold = pct;

            // Internal default subscriptions for logging + console notifications
            // Subscribe once (safe to call multiple times for seperate service instances)
            OnTransferStarted += (s,e) => WriteLog($"EVENT: Transfer STARTED for {e.Meta.BatteryID}/{e.Meta.TestID}, SoC={e.Meta.SoC}");
            OnSampleRecived += (s,e) => WriteLog($"EVENT: Sample recived (#{e.RecivedRows}) for {currentSession?.BatteryID}/{currentSession?.TestID}");
            OnTransferCompleted += (s,e) => WriteLog($"EVENT: Transfer COMPLETED for {e.Meta.BatteryID}/{e.Meta.TestID}, SoC={e.Meta.SoC}");
            OnWarningRaised += (s,e) => WriteLog($"EVENT WARNING [{e.WarningType}]: {e.Message}");
        }
        public OperationStatus StartSession(EisMeta meta)
        {
            if (meta == null || string.IsNullOrEmpty(meta.BatteryID) || string.IsNullOrEmpty(meta.TestID))
                throw new FaultException<DataFormatFault>(
                    new DataFormatFault("Meta podaci nisu ispravni."),
                    new FaultReason("Invalid meta data."));

            currentSession = meta;
            recivedRows = 0;
            lastRowIndex = 0;
            lastV = double.NaN;
            lastZ = double.NaN;
            runningSumZ = 0.0;
            runningCountZ = 0;

            //folders + files
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
            //Podigni dogadjaj
            OnTransferStarted?.Invoke(this, new TransferEventArgs { Meta = meta });

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
                RejectAndThrow("Sample je null.", isDataFormat: true, sampleForEvent:  null);
            }
            //osnovna validacija formata
                if (sample.FrequencyHz <= 0)
                {
                RejectAndThrow($"Nevalidna frekvencija: {sample.FrequencyHz}", sampleForEvent: sample);
                }
            bool isResistanceOk = double.IsNaN(sample.R_Ohm) || double.IsInfinity(sample.R_Ohm);
            bool isImpendanceOk = double.IsNaN(sample.X_Ohm) || double.IsInfinity(sample.X_Ohm);
            bool isVoltageOk = double.IsNaN(sample.V) || double.IsInfinity(sample.V);
            if (isResistanceOk || isImpendanceOk || isVoltageOk)
            {
                RejectAndThrow($"Nevalidne vrednosti R={sample.R_Ohm}, X={sample.X_Ohm}, V={sample.V}", sampleForEvent: sample);
            }

            if (sample.RowIndex <= lastRowIndex)
            {
                RejectAndThrow($"Nevalidan RowIndex={sample.RowIndex}, last={lastRowIndex}", sampleForEvent: sample);
            }
            
            //compute impedancde
            double Z = Math.Sqrt(sample.R_Ohm  * sample.R_Ohm + sample.X_Ohm * sample.X_Ohm);

            // DETEKCIJA VOLTAGE SPIKE
            if (!double.IsNaN(lastV))
            {
                double dV = sample.V - lastV;
                if (Math.Abs(dV) > V_threshold)
                {
                    string dir = dV > 0 ? "iznad ocekivanog" : "ispod ocekivanog";
                    string msg = $"VoltageSpike deltaV = {dV:F6} (smer: {dir}), threshold={V_threshold}";
                    //podigni warning dogadjaj, ali nemoj automatski odbaciti sample
                    OnWarningRaised?.Invoke(this, new WarningEventArgs { WarningType = "VoltageSpike", Message = msg, Sample = sample });
                    WriteLog(msg);
                }
            }

            //Detekcija IMPEDANCE JUMP
            if (!double.IsNaN(lastZ))
            {
                double dZ = Z - lastZ;
                if (Math.Abs (dZ) > Z_threshold)
                {
                    string dir = dZ > 0 ? "iznad ocekivanog" : "ispod ocekivanog";
                    string msg = $"ImpendanceJump: deltaZ = {dZ:F6} (smer: {dir}), threshold={Z_threshold}";
                    OnWarningRaised?.Invoke(this, new WarningEventArgs { WarningType = "ImpedanceJump", Message = msg, Sample = sample });
                    WriteLog(msg);
                }
            }

            //RUNNING MEAN za Z: pre nego sto ukljucimi uzorak racunamo meanBefore
            double meanBeforeZ = runningCountZ > 0 ? runningSumZ / runningCountZ : double.NaN;
            //Provera OutOfBand u odnosu na meanBefore ( ako imamo prethodnu srednju vrednost)
            if (!double.IsNaN(meanBeforeZ))
            {
                double lower = (1.0 - PercentThreshold) * meanBeforeZ;
                double upper = (1.0 + PercentThreshold) * meanBeforeZ;
                if (Z < lower || Z > upper)
                {
                    string dir = Z < lower ? "ispod ocekivanog" : "iznad ocekivanog";
                    string msg = $"OutOfBandWarning Z={Z:F6} {dir} (meanBefore={meanBeforeZ:F6}, +-{PercentThreshold * 100.0}% )";
                    OnWarningRaised?.Invoke(this, new WarningEventArgs { WarningType = "OutOfBand", Message= msg, Sample = sample });
                    WriteLog(msg);
                }
            }

            //Fizicki opsezi (ako ispada van, upisi u rejects i baci)
            if (sample.V<2.5 || sample.V > 4.2)
            {
                RejectAndThrow($"Napon {sample.V} V van opsega (2.5 - 4.2 V)", sampleForEvent: sample);
            }

            if (sample.T_degC < 0 || sample.T_degC > 60)
            {
                RejectAndThrow($"Temperatura {sample.T_degC} C van opsega (0-60 C", sampleForEvent: sample);
            }

            if (sample.R_Ohm < 0)
            {
                RejectAndThrow($"Otpornost R={sample.R_Ohm}, X={sample.X_Ohm} mora biti >= 0", sampleForEvent: sample);
            }

            if (sample.Range_ohm <= 0)
            {
                RejectAndThrow($"Range={sample.Range_ohm} mora biti > 0", sampleForEvent: sample);
            }

            //Ako smo stigli dovde sample je prihvacen
            lastRowIndex = sample.RowIndex;
            recivedRows++;

            //string line = $"{sample.RowIndex},{sample.FrequencyHz},{sample.R_Ohm},{sample.X_Ohm},{sample.V},{sample.T_degC},{sample.Range_ohm}";
            string line = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5},{6}",
                sample.RowIndex,
                sample.FrequencyHz,
                sample.R_Ohm,
                sample.X_Ohm,
                sample.V,
                sample.T_degC,
                sample.Range_ohm
            );
            fileWriter?.WriteLine(line);

            runningSumZ += Z;
            runningCountZ++;

            OnSampleRecived?.Invoke(this, new SampleEventArgs { Sample = sample, RecivedRows = recivedRows });

            WriteLog($"Sample {sample.RowIndex} ACCEPTED (Battery={currentSession.BatteryID}, Test={currentSession.TestID})");

            lastV = sample.V;
            lastZ = Z;

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
                    new DataFormatFault("Session nije zapocet."),
                    new FaultReason("EndSession called before StartSession.")
                );

            fileWriter?.Dispose();
            fileWriter = null;

            rejectsWriter?.Dispose();
            rejectsWriter = null;

            string msg;
            bool success = true;

            if (recivedRows != currentSession.TotalRows)
            {
                msg = $"Session completed with mismatch: primljeno {recivedRows}, ocekivano {currentSession.TotalRows}.";
                WriteLog($"MISMATCH u sesiji (Battery={currentSession.BatteryID}, Test={currentSession.TestID}, SoC={currentSession.SoC}): {recivedRows}/{currentSession.TotalRows}");
                success = false;
            }
            else
            {
                msg = $"Session completed successfully. Received {recivedRows}/{currentSession.TotalRows} rows.";
                WriteLog($"Session ENDED OK for Battery={currentSession.BatteryID}, Test={currentSession.TestID}, SoC={currentSession.SoC}");
            }

            //podigni dogadjaj
            OnTransferCompleted?.Invoke(this, new TransferEventArgs { Meta = currentSession });

            // GENERISANJE REPORT (isti kod koji smo ranije dodali) - optional, možeš zadržati svoj kod
            try
            {
                string folderPath = Path.Combine("Data", currentSession.BatteryID, currentSession.TestID, $"{currentSession.SoC}");
                string sessionPath = Path.Combine(folderPath, "session.csv");
                string rejectsPath = Path.Combine(folderPath, "rejects.csv");
                string reportPath = Path.Combine(folderPath, "report.csv");

                int rejectedCount = 0;
                if (File.Exists(rejectsPath))
                    rejectedCount = File.ReadAllLines(rejectsPath).Length;

                double avgR = 0, avgX = 0, avgV = 0, avgT = 0;
                int validCount = 0;

                if (File.Exists(sessionPath))
                {
                    var lines = File.ReadAllLines(sessionPath);
                    foreach (var l in lines)
                    {
                        var parts = l.Split(',');
                        if (parts.Length >= 7)
                        {
                            avgR += double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                            avgX += double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
                            avgV += double.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture);
                            avgT += double.Parse(parts[5], System.Globalization.CultureInfo.InvariantCulture);
                            validCount++;
                        }
                    }
                }

                if (validCount > 0)
                {
                    avgR /= validCount;
                    avgX /= validCount;
                    avgV /= validCount;
                    avgT /= validCount;
                }

                using (var sw = new StreamWriter(reportPath, false))
                {
                    sw.WriteLine("BatteryID,TestID,SoC,TotalRows,ReceivedRows,RejectedRows,AvgR,AvgX,AvgV,AvgT");
                    sw.WriteLine($"{currentSession.BatteryID},{currentSession.TestID},{currentSession.SoC},{currentSession.TotalRows},{recivedRows},{rejectedCount},{avgR:F6},{avgX:F6},{avgV:F6},{avgT:F2}");
                }

                WriteLog($"Report generated: {reportPath}");
            }
            catch (Exception ex)
            {
                WriteLog($"Greška pri generisanju izveštaja: {ex.Message}");
            }


            return new OperationStatus
            {
                Success = true,
                Message = msg,
                Status = success? "COMPLETED" : "FAILED"
            };
        }
        // Helper: reject, log, write to rejects.csv, podiže warning event i baca FaultException
        private void RejectAndThrow(string reason, bool isDataFormat = false, EisSample sampleForEvent = null)
        {
            WriteReject(reason);
            WriteLog($"Sample REJECTED: {reason}");

            // podigni warning event za odbacivanje
            OnWarningRaised?.Invoke(this, new WarningEventArgs { WarningType = "Reject", Message = reason, Sample = sampleForEvent });

            if (isDataFormat)
            {
                throw new FaultException<DataFormatFault>(
                    new DataFormatFault { Message = reason },
                    new FaultReason("Invalid data format."));
            }
            else
            {
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Message = reason },
                    new FaultReason("Validation failed."));
            }
        }
        private void WriteReject(string reason)
        {
            string line = $"[{DateTime.UtcNow}] {reason}";
            rejectsWriter?.WriteLine(line);
        }
        private void WriteLog(string message)
        {
            string line = $"[{DateTime.UtcNow}] {message}";
            Directory.CreateDirectory(Path.GetDirectoryName(logFile) ?? "Data");
            File.AppendAllText(logFile, line + Environment.NewLine);
            Console.WriteLine(line);
        }
    }
}
