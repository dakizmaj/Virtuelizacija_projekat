using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common;
using System.IO;
using System.Globalization;

namespace Client
{
    public static class CsvReader
    {
        public static IEnumerable<EisSample> ReadSamples(string filePath)
        {
            var samples = new List<EisSample>();

            using (var reader = new StreamReader(filePath))
            {
                int rowIndex = 0;
                string line;

                line = reader.ReadLine(); // preskoci header
                while ((line = reader.ReadLine()) != null)
                {
                    rowIndex++;
                    // Predpostavljamo CSV format: FrequencyHz,R_Ohm,X_Ohm,V,T_degC,Range_ohm
                    var parts = line.Split(',');

                    if (parts.Length < 6)
                        continue; // preskoci los red

                    samples.Add(new EisSample
                    {
                        RowIndex = rowIndex,
                        FrequencyHz = double.Parse(parts[0], CultureInfo.InvariantCulture),
                        R_Ohm = double.Parse(parts[1], CultureInfo.InvariantCulture),
                        X_Ohm = double.Parse(parts[2], CultureInfo.InvariantCulture),
                        V = double.Parse(parts[3], CultureInfo.InvariantCulture),
                        T_degC = double.Parse(parts[4], CultureInfo.InvariantCulture),
                        Range_ohm = double.Parse(parts[5], CultureInfo.InvariantCulture)
                    });
                }
            }
            return samples;
        }
    }
}
