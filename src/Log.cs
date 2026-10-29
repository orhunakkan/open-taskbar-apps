using System;
using System.IO;

namespace OpenTaskbar
{
    /// <summary>Records what the last run did in %APPDATA%\OpenTaskbar\last-run.log.</summary>
    internal static class Log
    {
        private static string FilePath
        {
            get { return Path.Combine(AppSettings.DataFolder, "last-run.log"); }
        }

        public static void Start()
        {
            try
            {
                Directory.CreateDirectory(AppSettings.DataFolder);
                File.WriteAllText(FilePath, "");
            }
            catch (Exception) { }
            Write("Open Taskbar run started.");
        }

        public static void Write(string message)
        {
            try
            {
                File.AppendAllText(FilePath, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine);
            }
            catch (Exception) { }
        }
    }
}
