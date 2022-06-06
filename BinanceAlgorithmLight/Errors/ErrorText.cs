using System;
using System.IO;

namespace BinanceAlgorithmLight.Errors
{
    public static class ErrorText
    {
        const string patch = "error-log.txt";
        public static void Add(string error)
        {
            string json = DateTime.Now.ToString() + " - " + error;
            File.AppendAllLines(@Patch(), json.Split('\n'));
        }
        public static string Patch()
        {
            return patch;
        }
        public static string Directory()
        {
            return System.IO.Path.Combine(Environment.CurrentDirectory, "");
        }
        public static string FullPatch()
        {
            return Directory() + "/" + Patch();
        }
    }
}
