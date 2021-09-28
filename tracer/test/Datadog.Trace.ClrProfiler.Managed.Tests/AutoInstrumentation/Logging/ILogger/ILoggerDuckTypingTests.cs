// <copyright file="ILoggerDuckTypingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System;
using System.Collections.Generic;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission;
using Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.Serilog;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Xunit;
using IExternalScopeProvider = Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission.IExternalScopeProvider;
using ILoggerFactory = Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission.ILoggerFactory;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.ILogger
{
    public class ILoggerDuckTypingTests
    {
        private readonly NullDatadogSink _sink;
        private readonly LogFormatter _formatter;

        public ILoggerDuckTypingTests()
        {
            _sink = new NullDatadogSink();
            _formatter = new LogFormatter(SettingsHelper.GetValidSettings());
        }

        [Fact]
        public void CanDuckTypeILoggerFactory()
        {
            var loggerFactory = new LoggerFactory();
            var testProvider = new ConsoleLoggerProvider(new DummyOptionsMonitor());

            var proxy = loggerFactory.DuckCast<ILoggerFactory>();
            proxy.Should().NotBeNull();
            proxy.AddProvider(testProvider);
        }

        [Fact]
        public void CanReverseDuckTypeILogger()
        {
            var logger = new DirectSubmissionLogger("Test logger", null, _sink, _formatter, DirectSubmissionLogLevel.Debug);
            var proxy = logger.DuckCast<Microsoft.Extensions.Logging.ILogger>();

            proxy.BeginScope(123).Should().NotBeNull();
            proxy.IsEnabled(LogLevel.Error).Should().BeTrue();
            proxy.Log(LogLevel.Error, "This is my message with a {Parameter}", 123);
        }

        [Fact]
        public void CanReverseDuckTypeILogger2()
        {
            var logger = new TestLogger();
            var proxy = logger.DuckCast<Microsoft.Extensions.Logging.ILogger>();

            proxy.BeginScope(123).Should().NotBeNull();
            proxy.IsEnabled(LogLevel.Error).Should().BeTrue();
            proxy.Log(LogLevel.Error, "This is my message with a {Parameter}", 123);

            logger.Logs.Should().ContainSingle("This is my message with a 123");
        }

        [Fact]
        public void CanReverseDuckTypeILoggerProvider()
        {
            var loggerProvider = new DirectSubmissionLoggerProvider(_sink, _formatter, DirectSubmissionLogLevel.Debug);
            var proxyProvider = loggerProvider.DuckCast<ILoggerProvider>();

            var logger = proxyProvider.CreateLogger("Some category");

            logger.BeginScope(123).Should().NotBeNull();
            logger.IsEnabled(LogLevel.Error).Should().BeTrue();
            logger.Log(LogLevel.Error, "This is my message with a {Parameter}", 123);
        }

        [Fact]
        public void CanAddLoggerProvider()
        {
            var loggerFactory = new LoggerFactory();
            var instance = loggerFactory.DuckCast<ILoggerFactory>();

            var providerType = instance.Type
                                    .GetMethod("CreateLogger")
                                    .ReturnType
                                    .Assembly.GetType("Microsoft.Extensions.Logging.ILoggerProvider");
            var provider = new TestLoggerProvider();
            var proxy = provider.DuckCast(providerType);

            instance.AddProvider(proxy);

            var logger = loggerFactory.CreateLogger("This is a test");

            logger.BeginScope(123).Should().NotBeNull();
            logger.BeginScope("some string").Should().NotBeNull();
            logger.IsEnabled(LogLevel.Error).Should().BeTrue();
            logger.Log(LogLevel.Error, "This is my message with a {Parameter}", 123);

            provider.Logger.Logs.Should().ContainSingle("This is my message with a 123");
        }

        [Fact]
        public void CanDuckTypeExternalScopeProvider()
        {
            var scopeProvider = new LoggerExternalScopeProvider();
            var proxy = scopeProvider.DuckCast<IExternalScopeProvider>();

            proxy.Should().NotBeNull();
            using var scope = proxy.Push(123);
            scope.Should().NotBeNull();
        }

        [Fact]
        public void CanDuckTypeExternalScopeProviderAndUseWithProxyProvider()
        {
            var scopeProvider = new LoggerExternalScopeProvider();
            var loggerProvider = new DirectSubmissionLoggerProvider(new NullDatadogSink(), new LogFormatter(SettingsHelper.GetValidSettings()), DirectSubmissionLogLevel.Debug);
            var proxyProvider = loggerProvider.DuckCast<ISupportExternalScope>();
            proxyProvider.SetScopeProvider(scopeProvider);

            var logger = loggerProvider.CreateLogger("Test logger name");

            using var scope = logger.BeginScope(123);
            scope.Should().NotBeNull();
            logger.IsEnabled(3).Should().BeTrue();
            logger.Log(logLevel: 3, 12, state: 123, null, (state, ex) => $"This is my message with a {state}");
        }

        [Fact]
        public void CanSetProviderUsingHelper()
        {
            var factory = new LoggerFactory();
            var loggerProvider = new DirectSubmissionLoggerProvider(new NullDatadogSink(), new LogFormatter(SettingsHelper.GetValidSettings()), DirectSubmissionLogLevel.Debug);
            LoggerFactoryIntegrationCommon<LoggerFactory>.AddDirectSubmissionLoggerProvider(factory, loggerProvider);
        }

        public sealed class DummyOptionsMonitor : IOptionsMonitor<ConsoleLoggerOptions>
        {
            public ConsoleLoggerOptions CurrentValue { get; } = new ConsoleLoggerOptions();

            public IDisposable OnChange(Action<ConsoleLoggerOptions, string> listener) => null;

            public ConsoleLoggerOptions Get(string name) => CurrentValue;
        }

        /// <summary>
        /// Duck type for ILoggerProvider
        /// </summary>
        public class TestLoggerProvider
        {
            public TestLogger Logger { get; } = new();

            public IExternalScopeProvider ScopeProvider { get; private set; }

            /// <summary>
            /// Creates a new <see cref="ILogger"/> instance.
            /// </summary>
            /// <param name="categoryName">The category name for messages produced by the logger.</param>
            /// <returns>The instance of <see cref="ILogger"/> that was created.</returns>
            [DuckReverseMethod(ClrNames.String)]
            public TestLogger CreateLogger(string categoryName)
            {
                return Logger;
            }

            /// <inheritdoc cref="IDisposable.Dispose"/>
            [DuckReverseMethod]
            public void Dispose()
            {
            }

            /// <summary>
            /// Method for ISupportExternalScope
            /// </summary>
            /// <param name="scopeProvider">The provider of scope data</param>
            [DuckReverseMethod("Microsoft.Extensions.Logging.IExternalScopeProvider")]
            public void SetScopeProvider(IExternalScopeProvider scopeProvider)
            {
                ScopeProvider = scopeProvider;
            }
        }

        public class TestLogger
        {
            public List<string> Logs { get; } = new();

            [DuckReverseMethod("Microsoft.Extensions.Logging.LogLevel", "Microsoft.Extensions.Logging.EventId", "TState", "System.Exception", "Func`3")]
            public void Log<TState>(int logLevel, object eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                Logs.Add(formatter(state, exception));
            }

            /// <summary>
            /// Checks if the given <paramref name="logLevel"/> is enabled.
            /// </summary>
            /// <param name="logLevel">Level to be checked.</param>
            /// <returns><c>true</c> if enabled.</returns>
            [DuckReverseMethod("Microsoft.Extensions.Logging.LogLevel")]
            public bool IsEnabled(int logLevel) => true;

            /// <summary>
            /// Begins a logical operation scope.
            /// </summary>
            /// <param name="state">The identifier for the scope.</param>
            /// <typeparam name="TState">The type of the state to begin scope for.</typeparam>
            /// <returns>An <see cref="IDisposable"/> that ends the logical operation scope on dispose.</returns>
            [DuckReverseMethod("Microsoft.Extensions.Logging.LogLevel")]
            public IDisposable BeginScope<TState>(TState state) => NullDisposable.Instance;

            private class NullDisposable : IDisposable
            {
                public static readonly NullDisposable Instance = new();

                public void Dispose()
                {
                }
            }
        }
    }
}
#endif
