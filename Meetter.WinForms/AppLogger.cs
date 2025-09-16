using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Meetter.WinForms;

internal static class AppLogger
{
    private static readonly object _sync = new object();
    private static string _logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Meetter", "logs");
    private static string _logFilePath = Path.Combine(_logDirectory, "app.log");
    private static long _maxBytes = 1 * 1024 * 1024; // 1 MB
    private static int _retainedFiles = 3;
    private static bool _initialized;

    public static string LogFilePath => _logFilePath;

    public static void Configure(string? directory = null, long? maxBytes = null, int? retainedFiles = null)
    {
        lock (_sync)
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                _logDirectory = directory!;
                _logFilePath = Path.Combine(_logDirectory, "app.log");
            }
            if (maxBytes.HasValue) _maxBytes = Math.Max(64 * 1024, maxBytes.Value);
            if (retainedFiles.HasValue) _retainedFiles = Math.Max(1, retainedFiles.Value);
            Directory.CreateDirectory(_logDirectory);
            _initialized = true;
        }
    }

    public static void Info(string message) => Write("INFO", message, null);
    public static void Warn(string message) => Write("WARN", message, null);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);
    public static void Debug(string message) => Write("DEBUG", message, null);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            lock (_sync)
            {
                if (!_initialized)
                {
                    Configure();
                }
                RotateIfNeeded();
                var sb = new StringBuilder();
                var now = DateTimeOffset.Now;
                sb.Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
                sb.Append(' ');
                sb.Append(level);
                sb.Append(" [");
                sb.Append(Environment.ProcessId);
                sb.Append('/');
                sb.Append(Thread.CurrentThread.ManagedThreadId);
                sb.Append("] ");
                sb.Append(message);
                if (ex != null)
                {
                    sb.Append(" | ");
                    sb.Append(ex.GetType().Name);
                    sb.Append(": ");
                    sb.Append(ex.Message);
                    sb.AppendLine();
                    sb.Append(ex.StackTrace);
                }
                sb.AppendLine();
                File.AppendAllText(_logFilePath, sb.ToString(), Encoding.UTF8);
            }
        }
        catch { /* logging must never throw */ }
    }

    private static void RotateIfNeeded()
    {
        try
        {
            var fi = new FileInfo(_logFilePath);
            if (!fi.Exists) return;
            if (fi.Length < _maxBytes) return;
            for (int i = _retainedFiles - 1; i >= 1; i--)
            {
                var src = Path.Combine(_logDirectory, $"app.log.{i}");
                var dst = Path.Combine(_logDirectory, $"app.log.{i + 1}");
                if (File.Exists(dst)) File.Delete(dst);
                if (File.Exists(src)) File.Move(src, dst);
            }
            var first = Path.Combine(_logDirectory, "app.log.1");
            if (File.Exists(first)) File.Delete(first);
            File.Move(_logFilePath, first);
        }
        catch { }
    }
}


