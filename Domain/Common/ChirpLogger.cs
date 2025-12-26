using Chirp.Application.Common;

namespace Chirp.Domain.Common;

public class ChirpLogger(ChirpOptions chirpOptions) : ILogger
{
    private readonly ChirpOptions _chirpOptions = chirpOptions ?? throw new ArgumentNullException(nameof(chirpOptions));
    
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _chirpOptions.LoggingEnabled;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.Information && _chirpOptions.LoggingEnabled)
        {
            Console.WriteLine(formatter(state, exception));
        }
    }
}