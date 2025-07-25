using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;

namespace PatientTrackerWPF.Services
{
    public class SimpleFileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _logDirectory;
        private readonly LogLevel _minLogLevel;
        private static readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public SimpleFileLogger(string categoryName, string logDirectory, LogLevel minLogLevel = LogLevel.Information)
        {
            _categoryName = categoryName;
            _logDirectory = logDirectory;
            _minLogLevel = minLogLevel;
            Directory.CreateDirectory(_logDirectory);
        }

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLogLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logEntry = $"[{timestamp}] [{logLevel}] [{_categoryName}] {message}";

            if (exception != null)
            {
                logEntry += Environment.NewLine + exception.ToString();
            }

            var logFile = Path.Combine(_logDirectory, $"PatientTracker-{DateTime.Now:yyyyMMdd}.log");

            _lock.EnterWriteLock();
            try
            {
                File.AppendAllText(logFile, logEntry + Environment.NewLine);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }

    public class SimpleFileLoggerProvider : ILoggerProvider
    {
        private readonly string _logDirectory;
        private readonly LogLevel _minLogLevel;

        public SimpleFileLoggerProvider(string logDirectory, LogLevel minLogLevel = LogLevel.Information)
        {
            _logDirectory = logDirectory;
            _minLogLevel = minLogLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new SimpleFileLogger(categoryName, _logDirectory, _minLogLevel);
        }

        public void Dispose() { }
    }

    // Extension method to add to ILoggingBuilder
    public static class SimpleFileLoggerExtensions
    {
        public static ILoggingBuilder AddSimpleFile(this ILoggingBuilder builder, string logDirectory, LogLevel minLogLevel = LogLevel.Information)
        {
            builder.AddProvider(new SimpleFileLoggerProvider(logDirectory, minLogLevel));
            return builder;
        }
    }
}