using System;
using System.IO;
using System.Windows.Threading;

namespace BinanceAlgorithmLight.Errors
{
    public class ErrorText
    {
        public string patch = "error-log.txt";
        public void Add(string error)
        {
            string json = DateTime.Now.ToString() + " - " + error;
            File.AppendAllLines(@FullPatch(), json.Split('\n'));
        }
        public string Patch()
        {
            return patch;
        }
        public string Directory()
        {
            string directory = System.IO.Path.Combine(Environment.CurrentDirectory, "log");
            if (!System.IO.Directory.Exists(directory)) System.IO.Directory.CreateDirectory(directory);
            return directory;
        }
        public string FullPatch()
        {
            return Directory() + "/" + Patch();
        }
    }
}
