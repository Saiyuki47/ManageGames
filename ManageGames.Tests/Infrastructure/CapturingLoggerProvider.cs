using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ManageGames.Tests.Infrastructure;

/// <summary>Collects the app's warnings and errors so tests can inspect what was logged.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _messages = new();

    public IEnumerable<string> Messages => _messages;

    public ILogger CreateLogger(string categoryName)
    {
        return new CapturingLogger(_messages);
    }

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Warning;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                messages.Enqueue(formatter(state, exception));
            }
        }
    }
}
