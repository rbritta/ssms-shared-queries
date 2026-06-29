using System;
using System.IO;

namespace SsmsSharedQueries.Diagnostics
{
    /// <summary>
    /// Minimal thread-safe file logger. Writes to
    /// %LocalAppData%\SsmsSharedQueries\log.txt. Never throws.
    /// </summary>
    internal static class Log
    {
        private static readonly object _lock = new object();

        public static string FilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SsmsSharedQueries", "log.txt");

        public static void Write(string message)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}");
                }
            }
            catch { /* logging must never break the plugin */ }
        }

        public static void Write(string message, Exception ex) => Write($"{message} :: {ex}");
    }
}
