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
    [BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
    public class ILoggerBenchmark
    {
        private ILogger _logger;
        private ServiceProvider _serviceProvider;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var config = TracerHelper.DefaultConfig;
            config[ConfigurationKeys.LogsInjectionEnabled] = true;
            config[ConfigurationKeys.Environment] = "env";
            config[ConfigurationKeys.ServiceVersion] = "version";
            TracerHelper.SetGlobalTracer(config);

            var services = new ServiceCollection();
            services.AddLogging();

            services.AddSingleton<ILoggerProvider, TestProvider>();

            _serviceProvider = services.BuildServiceProvider();

            _logger = _serviceProvider.GetRequiredService<ILogger<ILoggerBenchmark>>();

            // Warmup
            EnrichedLog();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            if (_logger is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _serviceProvider.Dispose();
            TracerHelper.CleanupGlobalTracer();
        }

        [Benchmark]
        public void EnrichedLog()
        {
            using (Tracer.Instance.StartActive("Test"))
            {
                using (Tracer.Instance.StartActive("Child"))
                {
                    _logger.LogInformation("Hello");
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
