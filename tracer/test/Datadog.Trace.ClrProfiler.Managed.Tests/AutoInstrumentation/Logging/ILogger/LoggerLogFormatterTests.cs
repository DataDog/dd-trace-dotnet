// <copyright file="LoggerLogFormatterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission.Formatting;
using Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.Serilog;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Logging.ILogger
{
    public class LoggerLogFormatterTests
    {
        [Fact]
        public void SerializesEventCorrectly()
        {
            var formatter = new LogFormatter(SettingsHelper.GetValidSettings());
            var scopeProvider = new LoggerExternalScopeProvider();
            var logger = new TestLogger(scopeProvider, formatter, new DateTime(2021, 09, 13, 10, 40, 57));

            scopeProvider.Push(new Dictionary<string, object> { { "OtherProperty", 62 } });

            logger.Log(
                LogLevel.Debug,
                new InvalidOperationException("Oops, just a test!"),
                "This is a test with a {Value}",
                123);

            var expected = @"{""@t"":""2021-09-13T10:40:57.0000000Z"",""@m"":""This is a test with a 123"",""@l"":""Debug"",""@x"":""System.InvalidOperationException: Oops, just a test!"",""Value"":123,""OtherProperty"":62,""@i"":""a9a87aee"",""ddsource"":""csharp"",""ddservice"":""MyTestService"",""host"":""some_host""}";

            logger.Logs.Should().ContainSingle(expected);
        }

        [Fact]
        public void DoesntAddPropertiesThatAreAlreadyAddedTwice()
        {
            var formatter = new LogFormatter(SettingsHelper.GetValidSettings());
            var scopeProvider = new LoggerExternalScopeProvider();
            var logger = new TestLogger(scopeProvider, formatter, new DateTime(2021, 09, 13, 10, 40, 57));
            var properties  = new Dictionary<string, object>
            {
                { "Host", nameof(DoesntAddPropertiesThatAreAlreadyAddedTwice) + "host" },
                { "dd_service", nameof(DoesntAddPropertiesThatAreAlreadyAddedTwice) + "service" },
                { "ddsource", nameof(DoesntAddPropertiesThatAreAlreadyAddedTwice) + "source" },
                { "ddtags", nameof(DoesntAddPropertiesThatAreAlreadyAddedTwice) + ":tag" },
            };

            using (var s = scopeProvider.Push(properties))
            {
                logger.Log(
                    LogLevel.Debug,
                    new InvalidOperationException("Oops, just a test!"),
                    "This is a test with a {Value}",
                    123,
                    "some host");
            }

            var log = logger.Logs.Should().ContainSingle().Subject;

            var json = JObject.Parse(log);

            // should have all the added properties
            foreach (var property in properties)
            {
                json.Properties()
                    .Where(x => string.Equals(property.Key, x.Name, StringComparison.OrdinalIgnoreCase))
                    .Should()
                    .ContainSingle()
                    .Which.Value.ToString()
                    .Should()
                    .Be(property.Value.ToString());
            }
        }

        internal class TestLogger : Microsoft.Extensions.Logging.ILogger
        {
            private readonly IExternalScopeProvider _provider;
            private readonly LogFormatter _formatter;
            private readonly DateTime _timestamp;

            public TestLogger(IExternalScopeProvider provider, LogFormatter formatter, DateTime timestamp)
            {
                _provider = provider;
                _formatter = formatter;
                _timestamp = timestamp;
            }

            public List<string> Logs { get; } = new();

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                var provider = _provider.DuckCast<Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission.IExternalScopeProvider>();
                var logEntry = new LogEntry<TState>(_timestamp, (int)logLevel, "some_cat", eventId.GetHashCode(), state, exception, formatter, provider);
                var log = LoggerLogFormatter.FormatLogEvent(_formatter, in logEntry);
                Logs.Add(log);
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public IDisposable BeginScope<TState>(TState state) => _provider.Push(state);
        }
    }
}
#endif
