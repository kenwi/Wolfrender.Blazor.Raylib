using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Wolfrender.Highscores.Server.Logging;

public static class FileLoggerExtensions
{
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, IConfiguration configuration)
    {
        var filePath = configuration["Logging:File:Path"];
        if (string.IsNullOrWhiteSpace(filePath))
            return builder;

        var minLevel = LogLevel.Information;
        if (Enum.TryParse<LogLevel>(configuration["Logging:File:MinLevel"], ignoreCase: true, out var configured))
            minLevel = configured;

        builder.AddProvider(new FileLoggerProvider(filePath, minLevel));
        return builder;
    }
}

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly LogLevel _minLevel;
    private readonly object _writeLock = new();

    private static readonly CultureInfo NorwegianCulture = CultureInfo.GetCultureInfo("nb-NO");

    public FileLoggerProvider(string filePath, LogLevel minLevel)
    {
        _filePath = filePath;
        _minLevel = minLevel;

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    public ILogger CreateLogger(string categoryName) =>
        new FileLogger(categoryName, _filePath, _minLevel, _writeLock);

    public void Dispose() { }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly string _filePath;
        private readonly LogLevel _minLevel;
        private readonly object _writeLock;

        public FileLogger(string category, string filePath, LogLevel minLevel, object writeLock)
        {
            _category = category;
            _filePath = filePath;
            _minLevel = minLevel;
            _writeLock = writeLock;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", NorwegianCulture);
            var level = logLevel switch
            {
                LogLevel.Trace => "TRC",
                LogLevel.Debug => "DBG",
                LogLevel.Information => "INF",
                LogLevel.Warning => "WRN",
                LogLevel.Error => "ERR",
                LogLevel.Critical => "CRT",
                _ => "???",
            };

            var builder = new StringBuilder();
            builder.Append('[').Append(timestamp).Append("] ");
            builder.Append('[').Append(level).Append("] ");
            builder.Append(_category).Append(": ");
            builder.Append(formatter(state, exception));

            if (exception is not null)
            {
                builder.AppendLine();
                builder.Append(exception);
            }

            lock (_writeLock)
                File.AppendAllText(_filePath, builder.ToString() + Environment.NewLine);
        }
    }
}
