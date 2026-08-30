using System.Text;
using Microsoft.Extensions.Logging;

namespace WaveDL.Helpers;

/// <summary>
/// Minimal thread-safe rolling file logger. One file per day under
/// <c>%LOCALAPPDATA%\WaveDL\logs</c>. Failures to write are swallowed so logging never
/// takes the app down.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly LogLevel _minLevel;
    private readonly object _gate = new();

    public FileLoggerProvider(string directory, LogLevel minLevel)
    {
        _directory = directory;
        _minLevel = minLevel;
        try
        {
            Directory.CreateDirectory(_directory);
            PruneOldFiles();
        }
        catch (IOException)
        {
            // Best effort — the console/debug logger still works.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void Write(LogLevel level, string category, string message, Exception? exception)
    {
        if (level < _minLevel)
        {
            return;
        }

        var shortCategory = category.Split('.').LastOrDefault() ?? category;
        var line = new StringBuilder()
            .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append(" [").Append(level.ToString().ToUpperInvariant()).Append("] ")
            .Append(shortCategory).Append(" - ").Append(message);

        if (exception is not null)
        {
            line.AppendLine().Append(exception);
        }

        line.AppendLine();

        try
        {
            var path = Path.Combine(_directory, $"wavedl-{DateTime.Now:yyyyMMdd}.log");
            lock (_gate)
            {
                File.AppendAllText(path, line.ToString(), Encoding.UTF8);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void PruneOldFiles()
    {
        var cutoff = DateTime.Now.AddDays(-14);
        foreach (var file in Directory.EnumerateFiles(_directory, "wavedl-*.log"))
        {
            if (File.GetLastWriteTime(file) < cutoff)
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                provider.Write(logLevel, category, formatter(state, exception), exception);
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
