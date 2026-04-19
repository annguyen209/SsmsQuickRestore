using System;
using System.IO;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace SsmsRestoreDrop.Logging
{
    internal static class Logger
    {
        private static IVsOutputWindowPane? _outputPane;
        private static string?              _logFilePath;
        private static readonly object      _lock = new object();

        private static readonly Guid OutputPaneGuid =
            new Guid("E3B2C8A1-F4D5-4E6F-9A0B-1C2D3E4F5A6B");

        public static void Initialize(AsyncPackage package)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                // readonly fields can't be passed as ref; copy to local first
                var paneGuid = OutputPaneGuid;
                outputWindow?.CreatePane(ref paneGuid, "SSMS Quick Restore", 1, 1);
                outputWindow?.GetPane(ref paneGuid, out _outputPane);

                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var dir     = Path.Combine(appData, "SsmsQuickRestore", "Logs");
                Directory.CreateDirectory(dir);
                _logFilePath = Path.Combine(dir,
                    $"SsmsQuickRestore_{DateTime.Now:yyyyMMdd}.log");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SsmsQuickRestore] Logger init failed: {ex.Message}");
            }
        }

        public static void Info(string message)  => Write("INFO",  message);
        public static void Warn(string message)  => Write("WARN",  message);
        public static void Error(string message) => Write("ERROR", message);
        public static void Error(string message, Exception ex)
            => Write("ERROR", $"{message}{Environment.NewLine}{ex}");

        private static void Write(string level, string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";

            // Output window (must be on UI thread; if not, fall through silently)
            if (ThreadHelper.CheckAccess())
            {
#pragma warning disable VSTHRD010
                _outputPane?.OutputStringThreadSafe(line);
#pragma warning restore VSTHRD010
            }

            // Rolling log file
            lock (_lock)
            {
                if (_logFilePath != null)
                {
                    try { File.AppendAllText(_logFilePath, line); }
                    catch { /* best-effort */ }
                }
            }

            System.Diagnostics.Debug.Write($"[SsmsQuickRestore] {line}");
        }
    }
}
