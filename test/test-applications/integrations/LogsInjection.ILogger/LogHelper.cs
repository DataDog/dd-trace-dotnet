using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace LogsInjection.ILogger
{
    public static class LogHelper
    {
        public const string ExcludeMessagePrefix = "[ExcludeMessage]";

        public static void UninjectedLog(this Microsoft.Extensions.Logging.ILogger logger, string message)
        {
            logger.LogInformation($"{ExcludeMessagePrefix}{message}");
        }

        public static void ConditionalLog(this Microsoft.Extensions.Logging.ILogger logger, string message)
        {
#if NETCOREAPP
                logger.LogInformation(message);
#else
                // We don't instrument on .NET Framework, so we don't expect this to be log injected
                logger.UninjectedLog(message);
#endif
        }

        public static void ConfigureCustomLogging(WebHostBuilderContext ctx, ILoggingBuilder logging)
        {
            // delete existing log file
            var logFile = Path.Combine(ctx.HostingEnvironment.ContentRootPath, "simple.log");
            if (File.Exists(logFile))
            {
                try
                {
                    File.Delete(logFile);
                }
                catch
                {
                    // Don't throw if something's amiss
                }
            }

            logging.AddProvider(new SimpleLogProvider(logFile));
        }

        public class SimpleLogProvider : ILoggerProvider, ISupportExternalScope
        {
            private readonly string _logPath;

            public SimpleLogProvider(string logPath)
            {
                _logPath = logPath;
            }

            internal IExternalScopeProvider ScopeProvider { get; private set; }

            public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new SimpleLogger(this, categoryName, _logPath);

            public void Dispose()
            {
            }

            void ISupportExternalScope.SetScopeProvider(IExternalScopeProvider scopeProvider)
            {
                ScopeProvider = scopeProvider;
            }

            public class SimpleLogger : Microsoft.Extensions.Logging.ILogger
            {
                private readonly SimpleLogProvider _provider;
                private readonly string _logPath;
                private readonly string _category;

                public SimpleLogger(SimpleLogProvider loggerProvider, string categoryName, string logPath)
                {
                    _provider = loggerProvider;
                    _category = categoryName;
                    _logPath = logPath;
                }

                public IDisposable BeginScope<TState>(TState state)
                {
                    return _provider.ScopeProvider?.Push(state);
                }

                public bool IsEnabled(LogLevel logLevel)
                {
                    return true;
                }

                public void Log<TState>(DateTimeOffset timestamp, LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                {
                    if (!IsEnabled(logLevel))
                    {
                        return;
                    }

                    var scopes = new Dictionary<string, object>();
                    var scopeProvider = _provider.ScopeProvider;
                    if (scopeProvider != null)
                    {
                        scopeProvider.ForEachScope(
                            (scope, builder) =>
                            {
                                if (scope is IEnumerable<KeyValuePair<string, object>> pairs)
                                {
                                    foreach (var kvp in pairs)
                                    {
                                        builder[kvp.Key] = kvp.Value;
                                    }
                                }
                                else
                                {
                                    var temp = builder.TryGetValue("scopes", out var rawScope)
                                                   ? (List<object>)rawScope
                                                   : new List<object>();
                                    temp.Add(scope);
                                    builder["scopes"] = temp;
                                }
                            },
                            scopes);
                    }
#if NETCOREAPP2_1 || !NETCOREAPP
                    var log = Newtonsoft.Json.JsonConvert.SerializeObject(
                        new
                        {
                            timestamp = timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"),
                            category = _category,
                            scopes = scopes,
                            message = formatter(state, exception),
                            exception = exception?.Message
                        }, Newtonsoft.Json.Formatting.None);
#else
                    var log = System.Text.Json.JsonSerializer.Serialize(
                        new
                        {
                            timestamp = timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"),
                            category = _category,
                            scopes = scopes,
                            message = formatter(state, exception),
                            exception = exception?.Message
                        }, new System.Text.Json.JsonSerializerOptions()
                        {
                            WriteIndented = false,
                            IgnoreNullValues = true,
                        });
#endif


                    File.AppendAllText(_logPath, log + Environment.NewLine);
                }

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                {
                    Log(DateTimeOffset.UtcNow, logLevel, eventId, state, exception, formatter);
                }
            }
        }
    }
}
