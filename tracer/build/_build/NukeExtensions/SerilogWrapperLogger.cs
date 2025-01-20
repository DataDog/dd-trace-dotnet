using System;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using Serilog.Events;

namespace NukeExtensions;

// super basic logger for use with the test containers to just send to the log
public class SerilogWrapperLogger: ILogger
{
    public static readonly SerilogWrapperLogger Instance = new();
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        // lose the structuring, but meh for now
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        Serilog.Log.Write(Convert(logLevel), exception, formatter(state, exception));
    }

    public bool IsEnabled(LogLevel logLevel) => Serilog.Log.IsEnabled(Convert(logLevel));

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
        => LogContext.PushProperty("State", state);

    LogEventLevel Convert(LogLevel level) => level switch
    {
        LogLevel.Trace => LogEventLevel.Verbose,
        LogLevel.Debug => LogEventLevel.Debug,
        LogLevel.Information => LogEventLevel.Information,
        LogLevel.Warning => LogEventLevel.Warning,
        LogLevel.Error => LogEventLevel.Error,
        _ => LogEventLevel.Fatal
    };

}
