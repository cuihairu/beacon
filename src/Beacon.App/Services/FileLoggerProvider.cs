using Microsoft.Extensions.Logging;

namespace Beacon.App.Services;

/// <summary>滚动文件日志：%AppData%\Beacon\logs\beacon-YYYYMMDD.log，默认保留 7 天（B-105）。
/// Secrets 一律不进日志（RFC §13 脱敏）——Provider 侧不记录 token。</summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const string FilePrefix = "beacon-";

    private readonly string _logDirectory;
    private readonly int _retainDays;
    private readonly object _gate = new();
    private DateTime _lastPruneDate = DateTime.MinValue;

    public FileLoggerProvider(string? logDirectory = null, int retainDays = 7)
    {
        _logDirectory = logDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Beacon", "logs");
        _retainDays = retainDays;
        Directory.CreateDirectory(_logDirectory);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    private void Write(DateTime timestamp, LogLevel level, string category, Exception? exception, string message)
    {
        try
        {
            var line = $"{timestamp:HH:mm:ss.fff} [{level}] {category}: {message}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }
            lock (_gate)
            {
                File.AppendAllText(Path.Combine(_logDirectory, $"{FilePrefix}{timestamp:yyyyMMdd}.log"), line + Environment.NewLine);
                PruneIfNeeded(timestamp);
            }
        }
        catch (IOException)
        {
            // 日志写失败不吞掉应用本身
        }
    }

    private void PruneIfNeeded(DateTime now)
    {
        if (_lastPruneDate.Date == now.Date)
        {
            return;
        }
        _lastPruneDate = now.Date;
        var cutoff = now.Date.AddDays(-_retainDays);
        foreach (var file in Directory.EnumerateFiles(_logDirectory, $"{FilePrefix}*.log"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.Length > FilePrefix.Length
                && DateTime.TryParseExact(name[FilePrefix.Length..], "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date)
                && date < cutoff)
            {
                try { File.Delete(file); } catch (IOException) { }
            }
        }
    }

    private sealed class FileLogger(FileLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => owner.Write(DateTime.Now, logLevel, category, exception, formatter(state, exception));
    }

    public void Dispose()
    {
    }
}
