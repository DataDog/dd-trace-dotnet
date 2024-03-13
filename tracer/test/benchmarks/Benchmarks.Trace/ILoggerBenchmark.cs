using System;
using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger;
using Datadog.Trace.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IExternalScopeProvider = Microsoft.Extensions.Logging.IExternalScopeProvider;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [BenchmarkAgent4]
    [BenchmarkCategory(Constants.TracerCategory)]
    public class ILoggerBenchmark
    {
        private static readonly Tracer LogInjectionTracer;
        private static readonly ILogger Logger;

        static ILoggerBenchmark()
        {
            var logInjectionSettings = new TracerSettings
            {
                StartupDiagnosticLogEnabled = false,
                LogsInjectionEnabled = true,
                Environment = "env",
                ServiceVersion = "version"
            };

            LogInjectionTracer = new Tracer(logInjectionSettings, new DummyAgentWriter(), null, null, null);
            Tracer.UnsafeSetTracerInstance(LogInjectionTracer);

            var services = new ServiceCollection();
            services.AddLogging();

            services.AddSingleton<ILoggerProvider, TestProvider>();

            var serviceProvider = services.BuildServiceProvider();

            Logger = serviceProvider.GetRequiredService<ILogger<ILoggerBenchmark>>();
        }

        [Benchmark]
        public void EnrichedLog()
        {
            using (LogInjectionTracer.StartActive("Test"))
            {
                using (LogInjectionTracer.StartActive("Child"))
                {
                    Logger.LogInformation("Hello");
                }
            }
        }

        internal class TestProvider : ILoggerProvider, ISupportExternalScope
        {
            private IExternalScopeProvider _scopeProvider;
            public static TestProvider Instance { get; } = new();

            public ILogger CreateLogger(string categoryName)
            {
                return new TestLogger(_scopeProvider, categoryName);
            }

            /// <inheritdoc />
            public void Dispose()
            {
            }

            public void SetScopeProvider(IExternalScopeProvider scopeProvider)
            {
                _scopeProvider = scopeProvider;
            }
        }

        internal class TestLogger : ILogger
        {
            private readonly IExternalScopeProvider _scopeProvider;
            private readonly string _category;
            private const string LoglevelPadding = ": ";

            public TestLogger(IExternalScopeProvider scopeProvider, string category)
            {
                _scopeProvider = scopeProvider;
                _category = category;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
#if DEBUG
                var textWriter = System.Console.Out;
#else
                var textWriter = TextWriter.Null;
#endif
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                textWriter.Write(LoglevelPadding);
                textWriter.Write(_category);
                textWriter.Write('[');

                textWriter.Write(eventId.ToString());

                textWriter.Write(']');

                // scope information
                WriteScopeInformation(textWriter, _scopeProvider);
                var message = formatter(state, exception);
                WriteMessage(textWriter, message);

                if (exception != null)
                {
                    // exception message
                    WriteMessage(textWriter, exception.ToString());
                }

                textWriter.Write(Environment.NewLine);
            }

            private static void WriteMessage(TextWriter writer, string text)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    writer.Write(' ');
                    writer.Write(text.Replace(Environment.NewLine, " "));
                }
            }

            private static void WriteScopeInformation(TextWriter textWriter, IExternalScopeProvider scopeProvider)
            {
                if (scopeProvider != null)
                {
                    Action<object,TextWriter> callback = (scope, state) =>
                    {
                        state.Write(" => ");
                        state.Write(scope);
                    };

                    // Logs injection emulation
                    LoggerIntegrationCommon.AddScope(Tracer.Instance, callback, textWriter);
                    scopeProvider.ForEachScope(callback, textWriter);
                }
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public IDisposable BeginScope<TState>(TState state)
                => _scopeProvider?.Push(state) ?? NullScope.Instance;
        }

        internal sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new NullScope();

            private NullScope()
            {
            }

            /// <inheritdoc />
            public void Dispose()
            {
            }
        }
    }
}
