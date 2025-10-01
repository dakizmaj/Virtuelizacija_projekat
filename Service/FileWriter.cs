using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service
{
    public class FileWriter : IDisposable
    {
        private StreamWriter writer;
        private bool disposed = false;

        public FileWriter(string path)
        {
            // kreirani folder ako ne postoji
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            writer = new StreamWriter(path, append: true);
        }

        public void WriteLine(string line)
        {
            if (disposed)
                throw new ObjectDisposedException("FileWriter");
            writer.WriteLine(line);
            //writer.Flush();
        }

        public void Dispose()
        {
            if (!disposed)
            {
                writer?.Flush();
                writer?.Close();
                writer?.Dispose();
                disposed = true;
                Console.WriteLine("FileWriter resurs oslobodjen.");
            }
        }
    }
}
