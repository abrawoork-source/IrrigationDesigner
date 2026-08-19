// File: src/IrrigationApp/Services/FileLoggerProvider.cs
using Microsoft.Extensions.Logging;

namespace IrrigationApp.Services;

/// <summary>Simple rolling file logger writing to %AppData%\IrrigationDesigner\logs.</summary>
public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logPath;

    public FileLoggerProvider()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IrrigationDesigner", "logs");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, $"app_{DateTime.Now:yyyyMMdd}.log");
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(_logPath, categoryName);

    public void Dispose() { }
}

public class FileLogger : ILogger
{
    private readonly string _path;
    private readonly string _category;
    private static readonly object _lock = new();

    public FileLogger(string path, string category)
    {
        _path     = path;
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;

    public void Log<TState>(LogLevel level, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(level)) return;
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {_category}: {formatter(state, exception)}";
        if (exception != null) line += $"\n{exception}";
        lock (_lock)
        {
            try { File.AppendAllText(_path, line + Environment.NewLine); }
            catch { /* swallow log errors */ }
        }
    }
}
