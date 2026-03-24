using System;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell;

namespace Kilo.VisualStudio.Extension.Logging
{
    public interface IKiloLogger
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message, Exception? ex = null);
    }

    public class KiloLogger : IKiloLogger
    {
        private static readonly string LogFolder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kilo.VisualStudio", "Logs");

        private readonly string _logFile;

        public KiloLogger()
        {
            if (!System.IO.Directory.Exists(LogFolder))
            {
                System.IO.Directory.CreateDirectory(LogFolder);
            }

            _logFile = System.IO.Path.Combine(LogFolder, $"kilo_{DateTime.Now:yyyyMMdd}.log");
        }

        public void Info(string message) => Log("INFO", message);
        public void Warning(string message) => Log("WARN", message);

        public void Error(string message, Exception? ex = null)
        {
            var fullMessage = ex != null ? $"{message}\n{ex}" : message;
            Log("ERROR", fullMessage);
        }

        private void Log(string level, string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logLine = $"[{timestamp}] [{level}] {message}";

            try
            {
                System.IO.File.AppendAllText(_logFile, logLine + Environment.NewLine);
                Debug.WriteLine($"[Kilo] {logLine}");
            }
            catch
            {
            }
        }
    }
}